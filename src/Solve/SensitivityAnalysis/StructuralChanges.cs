using Solve.Algorithms;
using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Ops 9-10 of the brief: adding a new activity (decision variable) or a new
/// constraint to an already-solved model (section 4.16). Both are "what-if" operations — they
/// build an extended copy of the final tableau and don't touch the persisted model/result.</summary>
public static class StructuralChanges
{
    private const double Epsilon = 1e-9;

    /// <summary>Op 9 — price out a new decision variable (objective coefficient plus one
    /// constraint coefficient per row) against the current basis: A* = B⁻¹·aNew,
    /// reduced cost = Cbv·A* − cNew. Supports a plain "+" (&gt;=0) activity. If the reduced
    /// cost is negative the new column is appended and pivoted in.</summary>
    public static SensitivityOutcome AddActivity(SensitivityContext ctx, string name, double objectiveCoefficient, double[] constraintCoefficients)
    {
        if (constraintCoefficients.Length != ctx.M)
            throw new ArgumentException($"Expected {ctx.M} constraint coefficient(s), got {constraintCoefficients.Length}.");

        double cNewInternal = ctx.Sense * objectiveCoefficient;
        var aStar = new double[ctx.M];
        for (int r = 0; r < ctx.M; r++)
        {
            double sum = 0;
            for (int k = 0; k < ctx.M; k++)
                sum += ctx.BInverse[r, k] * constraintCoefficients[k];
            aStar[r] = sum;
        }

        double reducedCost = -cNewInternal;
        for (int r = 0; r < ctx.M; r++)
            reducedCost += ctx.Cbv[r] * aStar[r];

        var old = ctx.Final;
        int oldRhsCol = old.NumCols - 1;
        int newCol = oldRhsCol; // insert just before RHS, which shifts to the end
        var extended = new Tableau
        {
            NumRows = old.NumRows,
            NumCols = old.NumCols + 1,
            RowLabels = (string[])old.RowLabels.Clone(),
            ColumnLabels = new string[old.NumCols + 1],
            Data = new double[old.NumRows, old.NumCols + 1],
        };
        for (int c = 0; c < oldRhsCol; c++)
            extended.ColumnLabels[c] = old.ColumnLabels[c];
        extended.ColumnLabels[newCol] = name;
        extended.ColumnLabels[oldRhsCol + 1] = old.ColumnLabels[oldRhsCol];

        for (int r = 0; r < old.NumRows; r++)
        {
            for (int c = 0; c < oldRhsCol; c++)
                extended.Data[r, c] = old.Data[r, c];
            extended.Data[r, oldRhsCol + 1] = old.Data[r, oldRhsCol];
            extended.Data[r, newCol] = r < ctx.M ? aStar[r] : reducedCost;
        }

        var labelsBefore = (string[])extended.RowLabels.Clone();
        var status = reducedCost < -Epsilon ? TableauEditor.ContinueToOptimal(extended) : SimplexStatus.Optimal;
        bool basisChanged = !labelsBefore.SequenceEqual(extended.RowLabels);

        var (solution, objective) = ctx.ExtractSolution(extended);
        double newActivityValue = ValueOfColumn(extended, newCol);
        string message = reducedCost < -Epsilon
            ? $"Reduced cost {Round(reducedCost)} < 0: \"{name}\" is attractive and was pivoted in (value = {Round(newActivityValue)})."
            : $"Reduced cost {Round(reducedCost)} >= 0: \"{name}\" would not improve the solution and stays out of the basis.";

        return new SensitivityOutcome
        {
            BasisChanged = basisChanged,
            Message = message,
            Status = status,
            Solution = solution.Append(newActivityValue).ToArray(),
            SolutionLabels = ctx.Model.VariableNames.Append(name).ToArray(),
            ObjectiveValue = objective,
            FinalTableau = extended,
        };
    }

    /// <summary>Op 10 — add a new constraint row to the optimal tableau: expresses it purely in
    /// terms of currently non-basic variables by eliminating every currently-basic column, adds
    /// a slack for it, and restores feasibility with dual simplex if the resulting RHS is
    /// negative. ">=" is converted to "&lt;=" by negation first. An "=" constraint is instead
    /// added to an extended model and solved with the canonical Big-M simplex.</summary>
    public static SensitivityOutcome AddConstraint(SensitivityContext ctx, double[] originalCoefficients, string relation, double rhs)
    {
        if (originalCoefficients.Length != ctx.Model.NumVars)
            throw new ArgumentException($"Expected {ctx.Model.NumVars} coefficient(s), got {originalCoefficients.Length}.");
        if (relation is not "<=" and not ">=" and not "=")
            throw new ArgumentException("Relation must be <=, >=, or =.");

        if (relation == "=")
        {
            var extendedModel = ctx.Model.WithExtraConstraint(originalCoefficients, relation, rhs);
            var result = PrimalSimplex.Solve(extendedModel);
            string equalityMessage = result.Status == SimplexStatus.Optimal
                ? "The equality constraint was added and the extended model was re-solved."
                : $"The equality constraint was added, but the extended model is {result.Status.ToString().ToLowerInvariant()}.";
            return new SensitivityOutcome
            {
                BasisChanged = true,
                Message = equalityMessage,
                Status = result.Status,
                Solution = result.Solution,
                SolutionLabels = (string[])extendedModel.VariableNames.Clone(),
                ObjectiveValue = result.ObjectiveValue,
                FinalTableau = result.FinalTableau,
            };
        }

        var old = ctx.Final;
        int oldRhsCol = old.NumCols - 1;

        // Row in tableau-column space (RHS in the last slot), before eliminating basic columns.
        var row = new double[old.NumCols];
        for (int j = 0; j < ctx.Model.NumVars; j++)
        {
            var cols = ctx.DecisionVarColumns[j];
            row[cols[0]] = originalCoefficients[j];
            if (cols.Length == 2)
                row[cols[1]] = -originalCoefficients[j];
        }
        row[oldRhsCol] = rhs;

        if (relation == ">=")
        {
            for (int c = 0; c < row.Length; c++)
                row[c] = -row[c];
        }
        // "=" is added as-is ("<=" form) — see summary above.

        for (int k = 0; k < ctx.M; k++)
        {
            int basicCol = ctx.BasicColumn[k];
            double coeff = row[basicCol];
            if (Math.Abs(coeff) < Epsilon) continue;
            for (int c = 0; c < row.Length; c++)
                row[c] -= coeff * old.Data[k, c];
        }

        string slackLabel = $"s{ctx.M + 1}";
        int newCol = oldRhsCol;   // new slack, inserted before RHS
        int newRow = ctx.M;       // new constraint row, inserted before the objective row

        var extended = new Tableau
        {
            NumRows = old.NumRows + 1,
            NumCols = old.NumCols + 1,
            ColumnLabels = new string[old.NumCols + 1],
            RowLabels = new string[old.NumRows + 1],
            Data = new double[old.NumRows + 1, old.NumCols + 1],
        };
        for (int c = 0; c < oldRhsCol; c++)
            extended.ColumnLabels[c] = old.ColumnLabels[c];
        extended.ColumnLabels[newCol] = slackLabel;
        extended.ColumnLabels[oldRhsCol + 1] = old.ColumnLabels[oldRhsCol];

        for (int r = 0; r < ctx.M; r++)
        {
            extended.RowLabels[r] = old.RowLabels[r];
            for (int c = 0; c < oldRhsCol; c++)
                extended.Data[r, c] = old.Data[r, c];
            extended.Data[r, oldRhsCol + 1] = old.Data[r, oldRhsCol];
            extended.Data[r, newCol] = 0d;
        }

        extended.RowLabels[newRow] = slackLabel;
        for (int c = 0; c < oldRhsCol; c++)
            extended.Data[newRow, c] = row[c];
        extended.Data[newRow, oldRhsCol + 1] = row[oldRhsCol];
        extended.Data[newRow, newCol] = 1d;

        int oldObjRow = old.NumRows - 1;
        int newObjRow = extended.NumRows - 1;
        extended.RowLabels[newObjRow] = "z";
        for (int c = 0; c < oldRhsCol; c++)
            extended.Data[newObjRow, c] = old.Data[oldObjRow, c];
        extended.Data[newObjRow, oldRhsCol + 1] = old.Data[oldObjRow, oldRhsCol];
        extended.Data[newObjRow, newCol] = 0d;

        var labelsBefore = (string[])extended.RowLabels.Clone();
        double newRhs = extended.Data[newRow, oldRhsCol + 1];

        SimplexStatus status;
        string message;
        if (newRhs >= -Epsilon)
        {
            status = SimplexStatus.Optimal;
            message = "The current solution already satisfies the new constraint; no re-optimisation needed.";
        }
        else if (TableauEditor.RestoreFeasibility(extended))
        {
            status = SimplexStatus.Optimal;
            message = "The new constraint cut off the current solution; restored feasibility with dual simplex.";
        }
        else
        {
            status = SimplexStatus.Infeasible;
            message = "Adding this constraint leaves the model infeasible.";
        }
        if (relation == "=")
            message += " (Note: \"=\" was added as a \"<=\" row — a simplification; exact equality may need a full re-solve.)";

        bool basisChanged = !labelsBefore.SequenceEqual(extended.RowLabels);
        var (solution, objective) = ctx.ExtractSolution(extended);

        return new SensitivityOutcome
        {
            BasisChanged = basisChanged,
            Message = message,
            Status = status,
            Solution = solution,
            SolutionLabels = (string[])ctx.Model.VariableNames.Clone(),
            ObjectiveValue = objective,
            FinalTableau = extended,
        };
    }

    private static double ValueOfColumn(Tableau t, int col)
    {
        int rhsCol = t.NumCols - 1;
        for (int r = 0; r < t.NumRows - 1; r++)
        {
            if (Array.IndexOf(t.ColumnLabels, t.RowLabels[r]) == col)
                return t.Data[r, rhsCol];
        }
        return 0d;
    }

    private static double Round(double v) => Solve.Utils.Rounding.R(v);
}
