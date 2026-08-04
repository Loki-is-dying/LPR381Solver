namespace Solve.Models;

/// <summary>
/// A single simplex tableau: the coefficient matrix (including RHS column and the objective row),
/// plus the row/column labels needed to display and pivot it.
/// </summary>
public class Tableau
{
    /// <summary>Matrix data. Rows 0..NumRows-2 are constraints, the last row is the objective (z) row.
    /// Columns 0..NumCols-2 are variables, the last column is RHS.</summary>
    public double[,] Data { get; set; } = new double[0, 0];

    /// <summary>Column headers, e.g. "x1", "x2", "s1", "a1", "RHS".</summary>
    public string[] ColumnLabels { get; set; } = Array.Empty<string>();

    /// <summary>Current basic variable name for each constraint row (last entry is "z").</summary>
    public string[] RowLabels { get; set; } = Array.Empty<string>();

    public int NumRows { get; set; }
    public int NumCols { get; set; }

    public Tableau Clone()
    {
        return new Tableau
        {
            Data = (double[,])Data.Clone(),
            ColumnLabels = (string[])ColumnLabels.Clone(),
            RowLabels = (string[])RowLabels.Clone(),
            NumRows = NumRows,
            NumCols = NumCols,
        };
    }
}
