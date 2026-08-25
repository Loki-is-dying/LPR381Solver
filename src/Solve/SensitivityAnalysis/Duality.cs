using Solve.Algorithms;
using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Op 12 of the brief: build the dual LP from the primal model, solve it, and verify
/// strong/weak duality (section 4.18). Uses the standard "SOB" symmetric-duality
/// correspondence, hand-verified against the reference dual for Santa's Workshop
/// (max / all "&lt;=" / all "+" → min / all "&gt;=" / all y&gt;=0, see docs/Santas_Workshop_Reference.md).</summary>
public static class Duality
{
    private const double Epsilon = 1e-6;

    public static LPModel BuildDualModel(LPModel primal, out bool[] negatedForSign)
    {
        int m = primal.NumConstraints, n = primal.NumVars;
        bool primalMax = primal.IsMaximisation;

        var dualObjective = (double[])primal.RHS.Clone();
        var dualMatrix = new double[n, m];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                dualMatrix[j, i] = primal.ConstraintMatrix[i, j];
        var dualRhs = (double[])primal.ObjectiveCoefficients.Clone();

        // Primal variable sign -> dual constraint relation ("SOB": Standard/Opposite/Bizarre).
        var dualRelations = new string[n];
        for (int j = 0; j < n; j++)
        {
            string sign = primal.SignRestrictions[j] is "int" or "bin" ? "+" : primal.SignRestrictions[j];
            dualRelations[j] = (primalMax, sign) switch
            {
                (true, "+") => ">=",
                (true, "-") => "<=",
                (true, "urs") => "=",
                (false, "+") => "<=",
                (false, "-") => ">=",
                (false, "urs") => "=",
                _ => throw new ArgumentException($"Unsupported sign restriction \"{sign}\"."),
            };
        }

        // Primal constraint relation -> dual variable sign.
        var dualSign = new string[m];
        for (int i = 0; i < m; i++)
        {
            string relation = primal.ConstraintRelations[i];
            dualSign[i] = (primalMax, relation) switch
            {
                (true, "<=") => "+",
                (true, ">=") => "-",
                (true, "=") => "urs",
                (false, "<=") => "-",
                (false, ">=") => "+",
                (false, "=") => "urs",
                _ => throw new ArgumentException($"Unsupported constraint relation \"{relation}\"."),
            };
        }

        // CanonicalFormBuilder only special-cases "urs" (splits into +/- parts); a "-"
        // (non-positive) sign restriction isn't substituted there. Work around it by negating
        // that dual variable's column so it can be declared "+" instead, and remembering which
        // ones were flipped so the solved values can be negated back for display.
        negatedForSign = new bool[m];
        for (int i = 0; i < m; i++)
        {
            if (dualSign[i] != "-") continue;
            negatedForSign[i] = true;
            dualSign[i] = "+";
            dualObjective[i] = -dualObjective[i];
            for (int j = 0; j < n; j++)
                dualMatrix[j, i] = -dualMatrix[j, i];
        }

        return new LPModel
        {
            NumVars = m,
            NumConstraints = n,
            IsMaximisation = !primalMax,
            ObjectiveCoefficients = dualObjective,
            ConstraintMatrix = dualMatrix,
            ConstraintRelations = dualRelations,
            RHS = dualRhs,
            SignRestrictions = dualSign,
            VariableNames = Enumerable.Range(1, m).Select(k => $"y{k}").ToArray(),
        };
    }

    public static DualityReport Analyze(LPModel primal, SimplexResult primalResult)
    {
        var dualModel = BuildDualModel(primal, out var negated);
        var dualResult = PrimalSimplex.Solve(dualModel);
        bool primalFeasible = IsFeasible(primal, primalResult.Solution);
        bool dualFeasible = dualResult.Status == SimplexStatus.Optimal && IsFeasible(dualModel, dualResult.Solution);

        if (dualResult.Status == SimplexStatus.Optimal)
        {
            for (int i = 0; i < negated.Length; i++)
                if (negated[i]) dualResult.Solution[i] = -dualResult.Solution[i];
        }

        double primalObjective = primalResult.ObjectiveValue;
        double dualObjective = dualResult.Status == SimplexStatus.Optimal ? dualResult.ObjectiveValue : double.NaN;
        double gap = double.IsNaN(dualObjective) ? double.NaN : Math.Abs(primalObjective - dualObjective);
        bool weakDuality = primalFeasible && dualFeasible &&
            (primal.IsMaximisation ? primalObjective <= dualObjective + Epsilon : primalObjective >= dualObjective - Epsilon);

        return new DualityReport
        {
            DualModel = dualModel,
            DualResult = dualResult,
            PrimalObjective = primalObjective,
            DualObjective = dualObjective,
            Gap = gap,
            PrimalFeasible = primalFeasible,
            DualFeasible = dualFeasible,
            WeakDuality = weakDuality,
            StrongDuality = weakDuality && gap < Epsilon,
        };
    }

    private static bool IsFeasible(LPModel model, double[] solution)
    {
        if (solution.Length != model.NumVars)
            return false;

        for (int j = 0; j < model.NumVars; j++)
        {
            double value = solution[j];
            string sign = model.SignRestrictions[j];
            if (sign is "+" or "int" or "bin" && value < -Epsilon)
                return false;
            if (sign == "-" && value > Epsilon)
                return false;
            if (sign == "bin" && value > 1 + Epsilon)
                return false;
        }

        for (int i = 0; i < model.NumConstraints; i++)
        {
            double lhs = 0;
            for (int j = 0; j < model.NumVars; j++)
                lhs += model.ConstraintMatrix[i, j] * solution[j];
            double difference = lhs - model.RHS[i];
            if (model.ConstraintRelations[i] == "<=" && difference > Epsilon)
                return false;
            if (model.ConstraintRelations[i] == ">=" && difference < -Epsilon)
                return false;
            if (model.ConstraintRelations[i] == "=" && Math.Abs(difference) > Epsilon)
                return false;
        }
        return true;
    }
}
