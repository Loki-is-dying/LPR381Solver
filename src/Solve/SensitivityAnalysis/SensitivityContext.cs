using Solve.Algorithms;
using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>
/// Everything every Sensitivity Analysis operation needs, recovered from the already-solved
/// <see cref="SimplexResult"/> without requiring any change to the shared Models/Algorithms
/// contract: the basis inverse B⁻¹, the internal (max-sense) cost of every tableau column,
/// which tableau column(s) each original decision variable maps to, and the resulting shadow
/// prices. See docs/Santas_Workshop_Reference.md for the worked numbers this is checked against.
/// </summary>
public class SensitivityContext
{
    public LPModel Model { get; }
    public SimplexResult Result { get; }
    public Tableau Final { get; }

    /// <summary>+1 for a "max" model, -1 for "min" — matches the sign convention
    /// <see cref="CanonicalFormBuilder"/> uses internally (it always solves a maximisation).</summary>
    public int Sense { get; }

    public int M { get; }

    /// <summary>B⁻¹, recovered from the final tableau's columns at the positions that held the
    /// initial identity matrix (one slack/artificial column per constraint row).</summary>
    public double[,] BInverse { get; }

    /// <summary>Internal (max-sense) objective cost of every tableau column, indexed the same
    /// way as <see cref="Tableau.ColumnLabels"/> (RHS column excluded).</summary>
    public double[] InternalCost { get; }

    /// <summary>Tableau column index currently basic in each constraint row.</summary>
    public int[] BasicColumn { get; }

    /// <summary>Internal (max-sense) objective cost of the basic variable in each row.</summary>
    public double[] Cbv { get; }

    /// <summary>decisionVarColumns[j] = tableau column index(es) for original variable j
    /// (length 2, [positive, negative], for a "urs" variable).</summary>
    public int[][] DecisionVarColumns { get; }

    public List<int> ArtificialColumns { get; }

    /// <summary>Shadow price per constraint, internal (max-sense) units: Cbv · B⁻¹.</summary>
    public double[] ShadowPricesInternal { get; }

    /// <summary>Shadow price per constraint, in the model's original objective units.</summary>
    public double[] ShadowPricesOriginal { get; }

    private SensitivityContext(LPModel model, SimplexResult result, int sense, int m,
        double[,] bInverse, double[] internalCost, int[] basicColumn, double[] cbv,
        int[][] decisionVarColumns, List<int> artificialColumns,
        double[] shadowPricesInternal, double[] shadowPricesOriginal)
    {
        Model = model;
        Result = result;
        Final = result.FinalTableau;
        Sense = sense;
        M = m;
        BInverse = bInverse;
        InternalCost = internalCost;
        BasicColumn = basicColumn;
        Cbv = cbv;
        DecisionVarColumns = decisionVarColumns;
        ArtificialColumns = artificialColumns;
        ShadowPricesInternal = shadowPricesInternal;
        ShadowPricesOriginal = shadowPricesOriginal;
    }

    public static SensitivityContext From(LPModel model, SimplexResult result)
    {
        if (result.Status != SimplexStatus.Optimal)
            throw new InvalidOperationException("Sensitivity analysis requires an optimal solution.");

        int m = model.NumConstraints;
        int sense = model.IsMaximisation ? 1 : -1;

        var canonical = CanonicalFormBuilder.Build(model);
        var initial = canonical.Tableau;
        var final = result.FinalTableau;

        // The initial tableau's row-i basic variable is whichever slack/artificial column formed
        // the identity matrix for that row. That column, read from the final tableau, is B⁻¹'s
        // i-th column (standard revised-simplex identity).
        var identityCol = new int[m];
        for (int i = 0; i < m; i++)
            identityCol[i] = Array.IndexOf(initial.ColumnLabels, initial.RowLabels[i]);

        var bInverse = new double[m, m];
        for (int row = 0; row < m; row++)
            for (int k = 0; k < m; k++)
                bInverse[row, k] = final.Data[row, identityCol[k]];

        var basicColumn = new int[m];
        for (int row = 0; row < m; row++)
            basicColumn[row] = Array.IndexOf(final.ColumnLabels, final.RowLabels[row]);

        int numCols = final.NumCols - 1; // exclude RHS
        var internalCost = new double[numCols];
        for (int j = 0; j < model.NumVars; j++)
        {
            var cols = canonical.DecisionVarColumns[j];
            internalCost[cols[0]] = sense * model.ObjectiveCoefficients[j];
            if (cols.Length == 2)
                internalCost[cols[1]] = -sense * model.ObjectiveCoefficients[j];
        }
        foreach (int a in canonical.ArtificialColumns)
            internalCost[a] = CanonicalFormBuilder.BigM;

        var cbv = new double[m];
        for (int row = 0; row < m; row++)
            cbv[row] = internalCost[basicColumn[row]];

        var shadowInternal = new double[m];
        for (int col = 0; col < m; col++)
        {
            double sum = 0;
            for (int row = 0; row < m; row++)
                sum += cbv[row] * bInverse[row, col];
            shadowInternal[col] = sum;
        }
        var shadowOriginal = shadowInternal.Select(y => sense * y).ToArray();

        return new SensitivityContext(model, result, sense, m, bInverse, internalCost, basicColumn, cbv,
            canonical.DecisionVarColumns, canonical.ArtificialColumns, shadowInternal, shadowOriginal);
    }

    /// <summary>Every currently non-basic, non-artificial tableau column (RHS excluded).</summary>
    public IEnumerable<int> NonBasicColumns()
    {
        int numCols = Final.NumCols - 1;
        for (int c = 0; c < numCols; c++)
            if (!IsBasic(c) && !ArtificialColumns.Contains(c))
                yield return c;
    }

    /// <summary>Converts an allowable-change interval expressed in internal (max-sense) units
    /// into the equivalent interval in the model's original objective units — dividing by
    /// <see cref="Sense"/> flips both the value and, for a "min" model, the bound order.</summary>
    public static (double Lower, double Upper) ConvertDeltaRange(double loInternal, double hiInternal, int sense)
    {
        double a = loInternal / sense;
        double b = hiInternal / sense;
        return a <= b ? (a, b) : (b, a);
    }

    /// <summary>True if tableau column <paramref name="col"/> is currently basic.</summary>
    public bool IsBasic(int col) => Array.IndexOf(BasicColumn, col) != -1;

    /// <summary>Row index whose basic variable is tableau column <paramref name="col"/>, or -1 if non-basic.</summary>
    public int RowOf(int col) => Array.IndexOf(BasicColumn, col);

    public int ColumnIndex(string label)
    {
        int col = Array.IndexOf(Final.ColumnLabels, label);
        if (col == -1)
            throw new ArgumentException($"\"{label}\" is not a column in the solved tableau.");
        return col;
    }

    /// <summary>Original objective coefficient (not internal/sense-adjusted) for the decision
    /// variable whose tableau column is <paramref name="col"/>, or 0 for a slack/artificial column.</summary>
    public double OriginalObjectiveCoefficient(int col)
    {
        for (int j = 0; j < Model.NumVars; j++)
        {
            var cols = DecisionVarColumns[j];
            if (cols[0] == col) return Model.ObjectiveCoefficients[j];
            if (cols.Length == 2 && cols[1] == col) return -Model.ObjectiveCoefficients[j];
        }
        return 0d;
    }

    /// <summary>Reads decision-variable values and the objective value (original units) off a
    /// (possibly edited/re-optimised) tableau, the same way <c>PrimalSimplex.ExtractSolution</c> does.</summary>
    public (double[] Solution, double ObjectiveValue) ExtractSolution(Tableau t)
    {
        int rhsCol = t.NumCols - 1;
        int objRow = t.NumRows - 1;

        var solution = new double[Model.NumVars];
        for (int j = 0; j < Model.NumVars; j++)
        {
            var cols = DecisionVarColumns[j];
            double value = ValueOfColumn(t, cols[0], rhsCol);
            if (cols.Length == 2)
                value -= ValueOfColumn(t, cols[1], rhsCol);
            solution[j] = value;
        }

        double objective = Sense * t.Data[objRow, rhsCol];
        return (solution, objective);
    }

    private static double ValueOfColumn(Tableau t, int col, int rhsCol)
    {
        for (int r = 0; r < t.NumRows - 1; r++)
        {
            if (Array.IndexOf(t.ColumnLabels, t.RowLabels[r]) == col)
                return t.Data[r, rhsCol];
        }
        return 0d;
    }
}
