namespace Solve.Models;

/// <summary>
/// Full record of a simplex-family run: every tableau iteration, the pivot metadata for
/// each one, and the final outcome. This is what gets displayed on screen and mirrored
/// to the output file, and what Sensitivity Analysis (Member 4) reads afterwards.
/// </summary>
public class SimplexResult
{
    public SimplexStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;

    public Tableau CanonicalForm { get; set; } = new();

    /// <summary>One entry per pivot performed, in order.</summary>
    public List<Tableau> Iterations { get; set; } = new();

    /// <summary>Pivot metadata parallel to <see cref="Iterations"/>.</summary>
    public List<IterationRecord> IterationRecords { get; set; } = new();

    /// <summary>The tableau simplex finished on (optimal, or last one reached before stopping).</summary>
    public Tableau FinalTableau => Iterations.Count > 0 ? Iterations[^1] : CanonicalForm;

    /// <summary>Decision variable values, in original model order. Only meaningful when Status == Optimal.</summary>
    public double[] Solution { get; set; } = Array.Empty<double>();

    public double ObjectiveValue { get; set; }
}
