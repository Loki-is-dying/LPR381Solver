using Solve.Models;
using Solve.Utils;

namespace Solve.Algorithms;


public static class RevisedPrimalSimplex
{
    private const double Epsilon = 1e-9;
    private const int MaxIterations = 500;

    public static SimplexResult Solve(LPModel model)
    {
        var canonical = CanonicalFormBuilder.Build(model);
        var reference = canonical.Tableau; // untouched canonical tableau -- used only to read A and b

        int m = model.NumConstraints;
        int rhsCol = reference.NumCols - 1;
        int totalCols = rhsCol; // number of variable columns, excluding RHS

        var A = new double[m, totalCols];
        var b = new double[m];
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < totalCols; j++)
                A[i, j] = reference.Data[i, j];
            b[i] = reference.Data[i, rhsCol];
        }

       
        // always maximise; a "min" model runs as "max of the negated objective".
        var c = new double[totalCols];
        double sense = model.IsMaximisation ? 1d : -1d;
        for (int j = 0; j < model.NumVars; j++)
        {
            var cols = canonical.DecisionVarColumns[j];
            c[cols[0]] = sense * model.ObjectiveCoefficients[j];
            if (cols.Length == 2)
                c[cols[1]] = -sense * model.ObjectiveCoefficients[j];
        }
        foreach (int a in canonical.ArtificialColumns)
            c[a] = -CanonicalFormBuilder.BigM;

        // Initial basis: whichever column CanonicalFormBuilder marked as basic per row
        // (slack for "<=", artificial for ">=" / "=") -- always a unit column, so B = I.
        var basis = new int[m];
        for (int i = 0; i < m; i++)
            basis[i] = Array.IndexOf(reference.ColumnLabels, reference.RowLabels[i]);

        var binv = Identity(m);

        var result = new SimplexResult { CanonicalForm = reference.Clone() };

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

            var nonBasic = new List<int>();
            for (int j = 0; j < totalCols; j++)
                if (Array.IndexOf(basis, j) == -1)
                    nonBasic.Add(j);

            var cbv = new double[m];
            for (int i = 0; i < m; i++) cbv[i] = c[basis[i]];
            var cbvBinv = VecMatMultiply(cbv, binv);
            var xB = MatVecMultiply(binv, b);

            var priceOut = new Dictionary<int, double>();
            foreach (var j in nonBasic)
            {
                var Aj = GetColumn(A, j, m);
                double zj = Dot(cbvBinv, Aj);
                priceOut[j] = zj - c[j];
            }

            int enteringCol = FindEnteringColumn(priceOut);

            if (enteringCol == -1)
            {
                var optimalSnapshot = BuildTableauSnapshot(reference, basis, binv, xB, cbv, priceOut, m, totalCols);
                result.Iterations.Add(optimalSnapshot);
                result.IterationRecords.Add(new IterationRecord { Label = $"T-{iteration}" });

                if (AnyArtificialBasic(basis, canonical.ArtificialColumns, xB))
                {
                    result.Status = SimplexStatus.Infeasible;
                    result.Message = "Optimal tableau still has an artificial variable in the basis " +
                                      "at a positive value: the model has no feasible solution.";
                    return result;
                }

                result.Status = SimplexStatus.Optimal;
                (result.Solution, result.ObjectiveValue) = ExtractSolution(model, canonical, basis, xB);
                return result;
            }

            var AqStar = MatVecMultiply(binv, GetColumn(A, enteringCol, m));
            var thetas = new double[m];
            int leavingRow = -1;
            double minRatio = double.PositiveInfinity;
            for (int i = 0; i < m; i++)
            {
                if (AqStar[i] > Epsilon)
                {
                    double ratio = xB[i] / AqStar[i];
                    thetas[i] = ratio;
                    if (ratio < minRatio - Epsilon)
                    {
                        minRatio = ratio;
                        leavingRow = i;
                    }
                }
                else
                {
                    thetas[i] = double.NaN;
                }
            }

            var snapshot = BuildTableauSnapshot(reference, basis, binv, xB, cbv, priceOut, m, totalCols);

            if (leavingRow == -1)
            {
                result.Status = SimplexStatus.Unbounded;
                result.Message = $"No positive entry in the \"{reference.ColumnLabels[enteringCol]}\" column: " +
                                  "the objective can be increased without limit.";
                result.Iterations.Add(snapshot);
                result.IterationRecords.Add(new IterationRecord
                {
                    Label = $"T-{iteration}",
                    EnteringVariable = reference.ColumnLabels[enteringCol],
                    ThetaColumn = thetas,
                });
                return result;
            }

            var record = new IterationRecord
            {
                Label = $"T-{iteration}",
                PivotRow = leavingRow,
                PivotCol = enteringCol,
                PivotElement = AqStar[leavingRow],
                EnteringVariable = reference.ColumnLabels[enteringCol],
                LeavingVariable = reference.ColumnLabels[basis[leavingRow]],
                ThetaColumn = thetas,
            };
            result.Iterations.Add(snapshot);
            result.IterationRecords.Add(record);

            // Product Form of the Inverse: B^-1_new = E * B^-1, where E is the identity
            // matrix with its leavingRow-th column replaced by the eta vector built from Aq*.
            double pivot = AqStar[leavingRow];
            var eta = Identity(m);
            for (int i = 0; i < m; i++)
                eta[i, leavingRow] = (i == leavingRow) ? 1.0 / pivot : -AqStar[i] / pivot;
            binv = MatMatMultiply(eta, binv);

            basis[leavingRow] = enteringCol;
        }
    }

    private static int FindEnteringColumn(Dictionary<int, double> priceOut)
    {
        int best = -1;
        double bestValue = -Epsilon;
        foreach (var kvp in priceOut)
        {
            if (kvp.Value < bestValue)
            {
                bestValue = kvp.Value;
                best = kvp.Key;
            }
        }
        return best;
    }

    private static bool AnyArtificialBasic(int[] basis, List<int> artificialColumns, double[] xB)
    {
        for (int i = 0; i < basis.Length; i++)
            if (artificialColumns.Contains(basis[i]) && xB[i] > Epsilon)
                return true;
        return false;
    }

    private static (double[] solution, double objective) ExtractSolution(
        LPModel model, CanonicalForm canonical, int[] basis, double[] xB)
    {
        var solution = new double[model.NumVars];
        for (int j = 0; j < model.NumVars; j++)
        {
            var cols = canonical.DecisionVarColumns[j];
            double value = ValueOfColumn(basis, xB, cols[0]);
            if (cols.Length == 2)
                value -= ValueOfColumn(basis, xB, cols[1]);
            solution[j] = value;
        }

        double objective = 0;
        for (int j = 0; j < model.NumVars; j++)
            objective += model.ObjectiveCoefficients[j] * solution[j];

        return (solution, objective);
    }

    private static double ValueOfColumn(int[] basis, double[] xB, int col)
    {
        int row = Array.IndexOf(basis, col);
        return row == -1 ? 0d : xB[row];
    }

   
    private static Tableau BuildTableauSnapshot(Tableau reference, int[] basis, double[,] binv, double[] xB,
        double[] cbv, Dictionary<int, double> priceOut, int m, int totalCols)
    {
        int cols = totalCols + 1;
        var data = new double[m + 1, cols];

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < totalCols; j++)
            {
                double sum = 0;
                for (int k = 0; k < m; k++)
                    sum += binv[i, k] * reference.Data[k, j];
                data[i, j] = sum;
            }
            data[i, cols - 1] = xB[i];
        }

        for (int j = 0; j < totalCols; j++)
            data[m, j] = Array.IndexOf(basis, j) == -1 && priceOut.TryGetValue(j, out var v) ? v : 0.0;
        data[m, cols - 1] = Dot(cbv, xB);

        var rowLabels = new string[m + 1];
        for (int i = 0; i < m; i++)
            rowLabels[i] = reference.ColumnLabels[basis[i]];
        rowLabels[m] = "z";

        return new Tableau
        {
            Data = data,
            ColumnLabels = (string[])reference.ColumnLabels.Clone(),
            RowLabels = rowLabels,
            NumRows = m + 1,
            NumCols = cols,
        };
    }

    // ---- Matrix helpers ----

    private static double[,] Identity(int size)
    {
        var result = new double[size, size];
        for (int i = 0; i < size; i++) result[i, i] = 1.0;
        return result;
    }

    private static double[] GetColumn(double[,] matrix, int col, int rows)
    {
        var result = new double[rows];
        for (int i = 0; i < rows; i++) result[i] = matrix[i, col];
        return result;
    }

    private static double[] MatVecMultiply(double[,] matrix, double[] vector)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var result = new double[rows];
        for (int i = 0; i < rows; i++)
        {
            double sum = 0;
            for (int j = 0; j < cols; j++) sum += matrix[i, j] * vector[j];
            result[i] = sum;
        }
        return result;
    }

    private static double[] VecMatMultiply(double[] vector, double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var result = new double[cols];
        for (int j = 0; j < cols; j++)
        {
            double sum = 0;
            for (int i = 0; i < rows; i++) sum += vector[i] * matrix[i, j];
            result[j] = sum;
        }
        return result;
    }

    private static double[,] MatMatMultiply(double[,] a, double[,] b)
    {
        int aRows = a.GetLength(0), aCols = a.GetLength(1), bCols = b.GetLength(1);
        var result = new double[aRows, bCols];
        for (int i = 0; i < aRows; i++)
            for (int j = 0; j < bCols; j++)
            {
                double sum = 0;
                for (int k = 0; k < aCols; k++) sum += a[i, k] * b[k, j];
                result[i, j] = sum;
            }
        return result;
    }

    private static double Dot(double[] a, double[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }
}
