using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Solve.Models;
using Solve.Output;

namespace Solve.Algorithms;

internal class CuttingPlane_
{
}


public static class CuttingPlane
{
    private const double EPSILON = 1e-6;
    private const int MAX_ITERATIONS = 50;

    public static CuttingPlanResult Solve(LPModel model)
    {
        ValidateModel(model);

        var tableau = BuildInitialTableau(model);
        var result = new CuttingPlanResult();

        int[] integerVariableIndexes = GetIntegerVariableIndexes(model);

        if (integerVariableIndexes.Length == 0)
        {
            throw new InvalidOperationException(
            "Cutting Plane requires at least one integer or binary variable. Use 'int' or 'bin' in the sign restrictions.");
        }

        for (int iteration = 1; iteration <= MAX_ITERATIONS; iteration++)
        {
            result.Iterations = iteration;

            /*
            * Step 1:
            * Solve the LP relaxation.
            *
            *
            * If the tableau is primal feasible, use primal simplex.
            * If the tableau is dual feasible, use dual simplex.
            */
            SolveCurrentTableau(tableau);

            double[] solution = GetCurrentSolution(tableau);
            double objectiveValue = GetObjectiveValue(tableau);

            if (!model.IsMaximisation)
            {
                objectiveValue *= -1;
            }

            result.IterationLogs.Add($"Iteration {iteration}: LP relaxation solved.");
            result.IterationLogs.Add($"Objective value: {objectiveValue:0.###}");

            /*
            * Step 2:
            * Check whether all integer and binary variables are whole numbers.
            */
            if (IsIntegerSolution(solution, integerVariableIndexes))
            {
                result.Status = "Optimal integer solution found";
                result.ObjectiveValue = objectiveValue;
                result.Solution = solution;
                return result;
            }

            /*
            * Step 3:
            * Choose a fractional row from the simplex tableau.
            */
            int fractionalRow = FindFractionalRow(tableau, integerVariableIndexes);

            if (fractionalRow == -1)
            {
                result.Status = "No fractional row found, but integer solution was not reached.";
                result.ObjectiveValue = objectiveValue;
                result.Solution = solution;
                return result;
            }

            /*
            * Step 4 and 5:
            * Generate Gomory fractional cut and add the cut.
            */
            string cut = AddGomoryCut(tableau, fractionalRow, model);
            result.Cuts.Add(cut);

            result.IterationLogs.Add($"Fractional row selected: row {fractionalRow + 1}");
            result.IterationLogs.Add($"Gomory cut added: {cut}");

            /*
            * Step 6 and 7:
            * Re-solve and repeat.
            *
            * After adding a Gomory cut, the tableau normally becomes primal infeasible
            * but remains dual feasible, so the next loop will usually use dual simplex.
            */
        }

        result.Status = "Maximum iterations reached before finding an integer solution.";
        result.Solution = GetCurrentSolution(tableau);

        double finalObjective = GetObjectiveValue(tableau);

        if (!model.IsMaximisation)
        {
            finalObjective *= -1;
        }

        result.ObjectiveValue = finalObjective;

        return result;
    }

    private static void ValidateModel(LPModel model)
    {
        if (model is null)
            throw new ArgumentException("Model cannot be null.");

        if (model.NumVars <= 0)
            throw new ArgumentException("Model must contain at least one variable.");

        if (model.NumConstraints <= 0)
            throw new ArgumentException("Model must contain at least one constraint.");

        if (model.ObjectiveCoefficients.Length != model.NumVars)
            throw new ArgumentException("Objective coefficient count does not match number of variables.");

        if (model.RHS.Length != model.NumConstraints)
            throw new ArgumentException("RHS count does not match number of constraints.");

        if (model.ConstraintRelations.Length != model.NumConstraints)
            throw new ArgumentException("Constraint relation count does not match number of constraints.");

        if (model.SignRestrictions.Length != model.NumVars)
            throw new ArgumentException("Sign restriction count does not match number of variables.");

        foreach (string relation in model.ConstraintRelations)
        {
            if (relation != "<=" && relation != ">=" && relation != "=")
            {
                throw new ArgumentException($"Unsupported constraint relation: {relation}");
            }
        }

        foreach (string sign in model.SignRestrictions)
        {
            string s = sign.Trim().ToLower();

            if (s != "+" && s != "int" && s != "bin")
            {
                throw new InvalidOperationException(
                $"Cutting Plane currently supports '+', 'int' and 'bin' variables only. Unsupported sign restriction: {sign}");
            }
        }

        foreach (string relation in model.ConstraintRelations)
        {
            if (relation == "=")
            {
                throw new InvalidOperationException(
                "This Cutting Plane implementation does not yet support equality constraints. Convert '=' constraints before solving.");
            }
        }
    }

    private static int[] GetIntegerVariableIndexes(LPModel model)
    {
        List<int> indexes = new();

        for (int i = 0; i < model.NumVars; i++)
        {
            string sign = model.SignRestrictions[i].Trim().ToLower();

            if (sign == "int" || sign == "bin")
            {
                indexes.Add(i);
            }
        }

        return indexes.ToArray();
    }

    private static CuttingPlaneTableau BuildInitialTableau(LPModel model)
    {
        /*
        *  build a simplex tableau.
        *
        * For <= constraints:
        * ax <= b becomes ax + s = b
        *
        * For >= constraints:
        * ax >= b becomes -ax <= -b
        * then -ax + s = -b
        *
        * This can create a negative RHS.
        * If the RHS is negative, primal simplex cannot start immediately.
        * The solver will then try dual simplex if the objective row is suitable.
        */

        int numVars = model.NumVars;
        int numConstraints = model.NumConstraints;

        /*
        * Add extra constraints for binary variables:
        * If x is binary, then x <= 1.
        */
        List<double[]> constraintRows = new();
        List<double> rhsValues = new();

        for (int i = 0; i < numConstraints; i++)
        {
            string relation = model.ConstraintRelations[i].Trim();

            double[] row = new double[numVars];

            for (int j = 0; j < numVars; j++)
            {
                row[j] = model.ConstraintMatrix[i, j];
            }

            double rhs = model.RHS[i];

            if (relation == "<=")
            {
                constraintRows.Add(row);
                rhsValues.Add(rhs);
            }
            else if (relation == ">=")
            {
                for (int j = 0; j < numVars; j++)
                {
                    row[j] *= -1;
                }

                constraintRows.Add(row);
                rhsValues.Add(-rhs);
            }
            else
            {
                throw new InvalidOperationException(
                "Equality constraints are not supported in this version of Cutting Plane.");
            }
        }

        for (int j = 0; j < numVars; j++)
        {
            string sign = model.SignRestrictions[j].Trim().ToLower();

            if (sign == "bin")
            {
                double[] binaryRow = new double[numVars];
                binaryRow[j] = 1;

                constraintRows.Add(binaryRow);
                rhsValues.Add(1);
            }
        }

        int rows = constraintRows.Count + 1;
        int cols = numVars + constraintRows.Count + 1;

        double[,] data = new double[rows, cols];

        for (int i = 0; i < constraintRows.Count; i++)
        {
            for (int j = 0; j < numVars; j++)
            {
                data[i, j] = constraintRows[i][j];
            }

            int slackCol = numVars + i;
            data[i, slackCol] = 1;

            int rhsCol = cols - 1;
            data[i, rhsCol] = rhsValues[i];
        }

        int objectiveRow = rows - 1;

        for (int j = 0; j < numVars; j++)
        {
            double coefficient = model.ObjectiveCoefficients[j];

            /*
            * For maximisation:
            * Z - cx = 0, so objective row uses -c.
            *
            * For minimisation:
            * Minimise cX is converted to Maximise -cX.
            * So objective row uses c.
            */
            data[objectiveRow, j] = model.IsMaximisation
            ? -coefficient
            : coefficient;
        }

        return new CuttingPlaneTableau
        {
            Data = data,
            OriginalVariableCount = numVars
        };
    }

    private static void SolveCurrentTableau(CuttingPlaneTableau tableau)
    {
        bool primalFeasible = IsPrimalFeasible(tableau);
        bool dualFeasible = IsDualFeasible(tableau);

        if (primalFeasible)
        {
            RunPrimalSimplex(tableau);
            return;
        }

        if (dualFeasible)
        {
            RunDualSimplex(tableau);
            return;
        }

        throw new InvalidOperationException(
        "The tableau is neither primal feasible nor dual feasible. A Phase I method is required for this model.");
    }

    private static bool IsPrimalFeasible(CuttingPlaneTableau tableau)
    {
        int rhsCol = tableau.ColCount - 1;
        int lastRow = tableau.RowCount - 1;

        for (int i = 0; i < lastRow; i++)
        {
            if (tableau.Data[i, rhsCol] < -EPSILON)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDualFeasible(CuttingPlaneTableau tableau)
    {
        int objectiveRow = tableau.RowCount - 1;
        int rhsCol = tableau.ColCount - 1;

        for (int j = 0; j < rhsCol; j++)
        {
            if (tableau.Data[objectiveRow, j] < -EPSILON)
            {
                return false;
            }
        }

        return true;
    }

    private static void RunPrimalSimplex(CuttingPlaneTableau tableau)
    {
        while (true)
        {
            int pivotCol = FindPrimalPivotColumn(tableau);

            if (pivotCol == -1)
            {
                return;
            }

            int pivotRow = FindPrimalPivotRow(tableau, pivotCol);

            if (pivotRow == -1)
            {
                throw new InvalidOperationException("The LP relaxation is unbounded.");
            }

            Pivot(tableau, pivotRow, pivotCol);
        }
    }

    private static int FindPrimalPivotColumn(CuttingPlaneTableau tableau)
    {
        int objectiveRow = tableau.RowCount - 1;
        int rhsCol = tableau.ColCount - 1;

        int pivotCol = -1;
        double mostNegative = -EPSILON;

        for (int j = 0; j < rhsCol; j++)
        {
            if (tableau.Data[objectiveRow, j] < mostNegative)
            {
                mostNegative = tableau.Data[objectiveRow, j];
                pivotCol = j;
            }
        }

        return pivotCol;
    }

    private static int FindPrimalPivotRow(CuttingPlaneTableau tableau, int pivotCol)
    {
        int rhsCol = tableau.ColCount - 1;
        int lastConstraintRow = tableau.RowCount - 1;

        int pivotRow = -1;
        double smallestRatio = double.MaxValue;

        for (int i = 0; i < lastConstraintRow; i++)
        {
            double coefficient = tableau.Data[i, pivotCol];

            if (coefficient > EPSILON)
            {
                double ratio = tableau.Data[i, rhsCol] / coefficient;

                if (ratio < smallestRatio)
                {
                    smallestRatio = ratio;
                    pivotRow = i;
                }
            }
        }

        return pivotRow;
    }

    private static void RunDualSimplex(CuttingPlaneTableau tableau)
    {
        while (true)
        {
            int pivotRow = FindDualPivotRow(tableau);

            if (pivotRow == -1)
            {
                return;
            }

            int pivotCol = FindDualPivotColumn(tableau, pivotRow);

            if (pivotCol == -1)
            {
                throw new InvalidOperationException(
                "The model is infeasible or no valid dual simplex pivot column exists.");
            }

            Pivot(tableau, pivotRow, pivotCol);
        }
    }

    private static int FindDualPivotRow(CuttingPlaneTableau tableau)
    {
        int rhsCol = tableau.ColCount - 1;
        int lastConstraintRow = tableau.RowCount - 1;

        int pivotRow = -1;
        double mostNegativeRhs = -EPSILON;

        for (int i = 0; i < lastConstraintRow; i++)
        {
            double rhs = tableau.Data[i, rhsCol];

            if (rhs < mostNegativeRhs)
            {
                mostNegativeRhs = rhs;
                pivotRow = i;
            }
        }

        return pivotRow;
    }

    private static int FindDualPivotColumn(CuttingPlaneTableau tableau, int pivotRow)
    {
        int objectiveRow = tableau.RowCount - 1;
        int rhsCol = tableau.ColCount - 1;

        int pivotCol = -1;
        double smallestRatio = double.MaxValue;

        for (int j = 0; j < rhsCol; j++)
        {
            double rowValue = tableau.Data[pivotRow, j];

            if (rowValue < -EPSILON)
            {
                double ratio = tableau.Data[objectiveRow, j] / -rowValue;

                if (ratio < smallestRatio)
                {
                    smallestRatio = ratio;
                    pivotCol = j;
                }
            }
        }

        return pivotCol;
    }

    private static void Pivot(CuttingPlaneTableau tableau, int pivotRow, int pivotCol)
    {
        int rows = tableau.RowCount;
        int cols = tableau.ColCount;

        double pivotValue = tableau.Data[pivotRow, pivotCol];

        if (Math.Abs(pivotValue) < EPSILON)
        {
            throw new InvalidOperationException("Cannot pivot on zero.");
        }

        for (int j = 0; j < cols; j++)
        {
            tableau.Data[pivotRow, j] /= pivotValue;
        }

        for (int i = 0; i < rows; i++)
        {
            if (i == pivotRow)
            {
                continue;
            }

            double factor = tableau.Data[i, pivotCol];

            for (int j = 0; j < cols; j++)
            {
                tableau.Data[i, j] -= factor * tableau.Data[pivotRow, j];
            }
        }
    }

    private static double[] GetCurrentSolution(CuttingPlaneTableau tableau)
    {
        double[] solution = new double[tableau.OriginalVariableCount];

        int rhsCol = tableau.ColCount - 1;

        for (int j = 0; j < tableau.OriginalVariableCount; j++)
        {
            int basicRow = FindBasicRow(tableau, j);

            if (basicRow != -1)
            {
                solution[j] = tableau.Data[basicRow, rhsCol];
            }
            else
            {
                solution[j] = 0;
            }
        }

        return solution;
    }

    private static int FindBasicRow(CuttingPlaneTableau tableau, int col)
    {
        int lastConstraintRow = tableau.RowCount - 1;

        int oneRow = -1;
        int oneCount = 0;

        for (int i = 0; i < lastConstraintRow; i++)
        {
            double value = tableau.Data[i, col];

            if (Math.Abs(value - 1) < EPSILON)
            {
                oneCount++;
                oneRow = i;
            }
            else if (Math.Abs(value) > EPSILON)
            {
                return -1;
            }
        }

        return oneCount == 1 ? oneRow : -1;
    }

    private static bool IsIntegerSolution(double[] solution, int[] integerVariableIndexes)
    {
        foreach (int index in integerVariableIndexes)
        {
            double value = solution[index];

            if (Math.Abs(value - Math.Round(value)) > EPSILON)
            {
                return false;
            }
        }

        return true;
    }

    private static int FindFractionalRow(CuttingPlaneTableau tableau, int[] integerVariableIndexes)
    {
        int rhsCol = tableau.ColCount - 1;
        int lastConstraintRow = tableau.RowCount - 1;

        int selectedRow = -1;
        double largestFraction = 0;

        for (int i = 0; i < lastConstraintRow; i++)
        {
            /*
            * Prefer a row where the basic variable is one of the original integer variables.
            */
            int basicVariableCol = FindBasicVariableColumn(tableau, i);

            if (basicVariableCol == -1)
            {
                continue;
            }

            if (!integerVariableIndexes.Contains(basicVariableCol))
            {
                continue;
            }

            double rhs = tableau.Data[i, rhsCol];
            double fraction = FractionalPart(rhs);

            if (fraction > largestFraction + EPSILON)
            {
                largestFraction = fraction;
                selectedRow = i;
            }
        }

        /*
        * Fallback:
        * If no integer-basic row is found, use any fractional RHS row.
        */
        if (selectedRow == -1)
        {
            for (int i = 0; i < lastConstraintRow; i++)
            {
                double rhs = tableau.Data[i, rhsCol];
                double fraction = FractionalPart(rhs);

                if (fraction > largestFraction + EPSILON)
                {
                    largestFraction = fraction;
                    selectedRow = i;
                }
            }
        }

        return selectedRow;
    }

    private static int FindBasicVariableColumn(CuttingPlaneTableau tableau, int row)
    {
        int rhsCol = tableau.ColCount - 1;

        for (int j = 0; j < rhsCol; j++)
        {
            if (Math.Abs(tableau.Data[row, j] - 1) > EPSILON)
            {
                continue;
            }

            bool isBasic = true;

            for (int i = 0; i < tableau.RowCount - 1; i++)
            {
                if (i == row)
                {
                    continue;
                }

                if (Math.Abs(tableau.Data[i, j]) > EPSILON)
                {
                    isBasic = false;
                    break;
                }
            }

            if (isBasic)
            {
                return j;
            }
        }

        return -1;
    }

    private static string AddGomoryCut(
    CuttingPlaneTableau tableau,
    int sourceRow,
    LPModel model)
    {
        int oldRows = tableau.RowCount;
        int oldCols = tableau.ColCount;

        int oldObjectiveRow = oldRows - 1;
        int oldRhsCol = oldCols - 1;

        int newRows = oldRows + 1;
        int newCols = oldCols + 1;

        int newCutRow = newRows - 2;
        int newObjectiveRow = newRows - 1;
        int newSlackCol = newCols - 2;
        int newRhsCol = newCols - 1;

        double[,] newData = new double[newRows, newCols];

        /*
        * Copy old constraint rows.
        */
        for (int i = 0; i < oldObjectiveRow; i++)
        {
            for (int j = 0; j < oldRhsCol; j++)
            {
                newData[i, j] = tableau.Data[i, j];
            }

            newData[i, newRhsCol] = tableau.Data[i, oldRhsCol];
        }

        /*
        * Copy old objective row.
        */
        for (int j = 0; j < oldRhsCol; j++)
        {
            newData[newObjectiveRow, j] = tableau.Data[oldObjectiveRow, j];
        }

        newData[newObjectiveRow, newRhsCol] = tableau.Data[oldObjectiveRow, oldRhsCol];

        /*
        * Gomory fractional cut:
        *
        * If the selected row has fractional RHS:
        *
        * xB + a1x1 + a2x2 + ... = b
        *
        * then the cut is:
        *
        * f(a1)x1 + f(a2)x2 + ... >= f(b)
        *
        * To add it to a simplex tableau, we write it as:
        *
        * -f(a1)x1 - f(a2)x2 - ... + s = -f(b)
        */
        List<string> terms = new();

        for (int j = 0; j < oldRhsCol; j++)
        {
            double fraction = FractionalPart(tableau.Data[sourceRow, j]);

            newData[newCutRow, j] = -fraction;

            if (fraction > EPSILON)
            {
                string variableName = GetVariableName(model, j);
                terms.Add($"-{fraction:0.###}{variableName}");
            }
        }

        double rhsFraction = FractionalPart(tableau.Data[sourceRow, oldRhsCol]);

        newData[newCutRow, newSlackCol] = 1;
        newData[newCutRow, newRhsCol] = -rhsFraction;

        tableau.Data = newData;

        string leftSide = terms.Count == 0
        ? "0"
        : string.Join(" ", terms);

        return $"{leftSide} <= {-rhsFraction:0.###}";
    }

    private static string GetVariableName(LPModel model, int columnIndex)
    {
        if (columnIndex < model.VariableNames.Length)
        {
            return model.VariableNames[columnIndex];
        }

        return $"s{columnIndex - model.NumVars + 1}";
    }

    private static double FractionalPart(double value)
    {
        double floor = Math.Floor(value);
        double fraction = value - floor;

        if (fraction < EPSILON)
        {
            return 0;
        }

        if (Math.Abs(fraction - 1) < EPSILON)
        {
            return 0;
        }

        return fraction;
    }

    private static double GetObjectiveValue(CuttingPlaneTableau tableau)
    {
        int objectiveRow = tableau.RowCount - 1;
        int rhsCol = tableau.ColCount - 1;

        return tableau.Data[objectiveRow, rhsCol];
    }
}

internal class CuttingPlaneTableau
{
    public double[,] Data { get; set; } = new double[0, 0];

    public int OriginalVariableCount { get; set; }

    public int RowCount => Data.GetLength(0);

    public int ColCount => Data.GetLength(1);
}