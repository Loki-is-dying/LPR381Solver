using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>
/// Re-optimises a tableau in place after a Sensitivity Analysis "what-if" edit. Mirrors the
/// pivoting logic in <see cref="Solve.Algorithms.PrimalSimplex"/> (whose pivot helpers are
/// private to that file, so can't be called directly), minus the Big-M/artificial-variable
/// bookkeeping — every SA edit starts from an already-solved, artificial-free tableau.
/// </summary>
public static class TableauEditor
{
    private const double Epsilon = 1e-9;
    private const int MaxIterations = 500;

    /// <summary>Primal-simplex pivoting from a feasible tableau (RHS all >= 0) whose objective
    /// row may have gone negative after an edit, back to optimality.</summary>
    public static SimplexStatus ContinueToOptimal(Tableau t)
    {
        int rhsCol = t.NumCols - 1;
        int objRow = t.NumRows - 1;

        for (int iteration = 0; iteration <= MaxIterations; iteration++)
        {
            int pivotCol = -1;
            double best = -Epsilon;
            for (int c = 0; c < rhsCol; c++)
            {
                if (t.Data[objRow, c] < best)
                {
                    best = t.Data[objRow, c];
                    pivotCol = c;
                }
            }
            if (pivotCol == -1)
                return SimplexStatus.Optimal;

            int pivotRow = -1;
            double bestTheta = double.PositiveInfinity;
            for (int r = 0; r < objRow; r++)
            {
                double a = t.Data[r, pivotCol];
                if (a > Epsilon)
                {
                    double theta = t.Data[r, rhsCol] / a;
                    if (theta < bestTheta - Epsilon)
                    {
                        bestTheta = theta;
                        pivotRow = r;
                    }
                }
            }
            if (pivotRow == -1)
                return SimplexStatus.Unbounded;

            Pivot(t, pivotRow, pivotCol);
            t.RowLabels[pivotRow] = t.ColumnLabels[pivotCol];
        }

        return SimplexStatus.Infeasible; // cycling guard
    }

    /// <summary>Dual-simplex pivoting from an optimal-but-infeasible tableau (some RHS &lt; 0
    /// after an edit) back to feasibility. Returns false if no feasible solution exists.</summary>
    public static bool RestoreFeasibility(Tableau t)
    {
        int rhsCol = t.NumCols - 1;
        int objRow = t.NumRows - 1;

        for (int iteration = 0; iteration <= MaxIterations; iteration++)
        {
            int pivotRow = -1;
            double mostNegative = -Epsilon;
            for (int r = 0; r < objRow; r++)
            {
                if (t.Data[r, rhsCol] < mostNegative)
                {
                    mostNegative = t.Data[r, rhsCol];
                    pivotRow = r;
                }
            }
            if (pivotRow == -1)
                return true;

            int pivotCol = -1;
            double bestRatio = double.PositiveInfinity;
            for (int c = 0; c < rhsCol; c++)
            {
                double a = t.Data[pivotRow, c];
                if (a < -Epsilon)
                {
                    double ratio = Math.Abs(t.Data[objRow, c] / a);
                    if (ratio < bestRatio - Epsilon)
                    {
                        bestRatio = ratio;
                        pivotCol = c;
                    }
                }
            }
            if (pivotCol == -1)
                return false; // row can never become non-negative: infeasible

            Pivot(t, pivotRow, pivotCol);
            t.RowLabels[pivotRow] = t.ColumnLabels[pivotCol];
        }

        return false; // cycling guard
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
}
