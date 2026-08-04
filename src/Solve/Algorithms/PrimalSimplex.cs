using Solve.Models;

namespace Solve.Algorithms;

/// <summary>
/// Primal Simplex (Big-M variant, so it also handles the ">=" and "=" constraints that
/// Branch &amp; Bound's ceiling branches add later, not just the "&lt;=" Knapsack case).
/// Displays the canonical form and every tableau iteration (entering/leaving variable,
/// pivot element, theta ratio-test column) via the returned <see cref="SimplexResult"/>.
/// </summary>
public static class PrimalSimplex
{
    private const double Epsilon = 1e-9;
    private const int MaxIterations = 500;

    public static SimplexResult Solve(LPModel model)
    {
        var canonical = CanonicalFormBuilder.Build(model);
        var result = new SimplexResult { CanonicalForm = canonical.Tableau.Clone() };

        var current = canonical.Tableau.Clone();
        int rhsCol = current.NumCols - 1;
        int objRow = current.NumRows - 1;

        int iteration = 0;
        while (true)
        {
            iteration++;
            if (iteration > MaxIterations)
            {
                result.Status = SimplexStatus.Infeasible;
                result.Message = $"Stopped after {MaxIterations} iterations without reaching optimality " +
                                  "(likely cycling on a degenerate tableau).";
                return result;
            }

            int pivotCol = FindEnteringColumn(current, objRow);
            if (pivotCol == -1)
            {
                if (AnyArtificialBasic(current, canonical.ArtificialColumns, rhsCol))
                {
                    result.Status = SimplexStatus.Infeasible;
                    result.Message = "Optimal tableau still has an artificial variable in the basis " +
                                      "at a positive value: the model has no feasible solution.";
                    return result;
                }

                result.Status = SimplexStatus.Optimal;
                (result.Solution, result.ObjectiveValue) = ExtractSolution(current, canonical, model, objRow, rhsCol);
                return result;
            }

            var thetas = ComputeThetas(current, pivotCol, rhsCol, objRow);
            int pivotRow = ChooseLeavingRow(thetas);
            if (pivotRow == -1)
            {
                result.Status = SimplexStatus.Unbounded;
                result.Message = $"No positive entry in the \"{current.ColumnLabels[pivotCol]}\" column: " +
                                  "the objective can be increased without limit.";
                return result;
            }

            var record = new IterationRecord
            {
                Label = $"T-{iteration}",
                PivotRow = pivotRow,
                PivotCol = pivotCol,
                PivotElement = current.Data[pivotRow, pivotCol],
                EnteringVariable = current.ColumnLabels[pivotCol],
                LeavingVariable = current.RowLabels[pivotRow],
                ThetaColumn = thetas,
            };

            Pivot(current, pivotRow, pivotCol);
            current.RowLabels[pivotRow] = current.ColumnLabels[pivotCol];

            result.Iterations.Add(current.Clone());
            result.IterationRecords.Add(record);
        }
    }

    private static int FindEnteringColumn(Tableau t, int objRow)
    {
        int best = -1;
        double bestValue = -Epsilon;
        for (int c = 0; c < t.NumCols - 1; c++)
        {
            if (t.Data[objRow, c] < bestValue)
            {
                bestValue = t.Data[objRow, c];
                best = c;
            }
        }
        return best;
    }

    private static double[] ComputeThetas(Tableau t, int pivotCol, int rhsCol, int objRow)
    {
        var thetas = new double[objRow]; // one per constraint row
        for (int r = 0; r < objRow; r++)
        {
            double a = t.Data[r, pivotCol];
            thetas[r] = a > Epsilon ? t.Data[r, rhsCol] / a : double.NaN;
        }
        return thetas;
    }

    private static int ChooseLeavingRow(double[] thetas)
    {
        int best = -1;
        double bestValue = double.PositiveInfinity;
        for (int r = 0; r < thetas.Length; r++)
        {
            if (double.IsNaN(thetas[r]))
                continue;
            if (thetas[r] < bestValue - Epsilon)
            {
                bestValue = thetas[r];
                best = r;
            }
        }
        return best;
    }

    private static void Pivot(Tableau t, int pivotRow, int pivotCol)
    {
        double pivotValue = t.Data[pivotRow, pivotCol];
        for (int c = 0; c < t.NumCols; c++)
            t.Data[pivotRow, c] /= pivotValue;

        for (int r = 0; r < t.NumRows; r++)
        {
            if (r == pivotRow) continue;
            double factor = t.Data[r, pivotCol];
            if (Math.Abs(factor) < Epsilon) continue;
            for (int c = 0; c < t.NumCols; c++)
                t.Data[r, c] -= factor * t.Data[pivotRow, c];
        }
    }

    private static bool AnyArtificialBasic(Tableau t, List<int> artificialColumns, int rhsCol)
    {
        for (int r = 0; r < t.NumRows - 1; r++)
        {
            int basicCol = Array.IndexOf(t.ColumnLabels, t.RowLabels[r]);
            if (artificialColumns.Contains(basicCol) && t.Data[r, rhsCol] > Epsilon)
                return true;
        }
        return false;
    }

    private static (double[] solution, double objective) ExtractSolution(
        Tableau t, CanonicalForm canonical, LPModel model, int objRow, int rhsCol)
    {
        var solution = new double[model.NumVars];
        for (int j = 0; j < model.NumVars; j++)
        {
            var cols = canonical.DecisionVarColumns[j];
            double value = ValueOfColumn(t, cols[0], rhsCol);
            if (cols.Length == 2)
                value -= ValueOfColumn(t, cols[1], rhsCol);
            solution[j] = value;
        }

        double internalObjective = t.Data[objRow, rhsCol];
        double objective = model.IsMaximisation ? internalObjective : -internalObjective;
        return (solution, objective);
    }

    private static double ValueOfColumn(Tableau t, int col, int rhsCol)
    {
        int row = -1;
        for (int r = 0; r < t.NumRows - 1; r++)
        {
            if (Array.IndexOf(t.ColumnLabels, t.RowLabels[r]) == col)
            {
                row = r;
                break;
            }
        }
        return row == -1 ? 0d : t.Data[row, rhsCol];
    }
}
