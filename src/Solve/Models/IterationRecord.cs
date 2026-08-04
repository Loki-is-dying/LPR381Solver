namespace Solve.Models;

/// <summary>
/// Metadata for a single simplex pivot: which variable entered/left the basis, the pivot
/// element, and the ratio-test (theta) column, so it can be displayed alongside its tableau.
/// </summary>
public class IterationRecord
{
    public string Label { get; set; } = string.Empty;
    public string EnteringVariable { get; set; } = string.Empty;
    public string LeavingVariable { get; set; } = string.Empty;
    public int PivotRow { get; set; }
    public int PivotCol { get; set; }
    public double PivotElement { get; set; }

    /// <summary>Ratio-test value per constraint row; double.NaN where the row was not eligible.</summary>
    public double[] ThetaColumn { get; set; } = Array.Empty<double>();
}
