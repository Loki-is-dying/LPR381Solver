namespace Solve.Models;

/// <summary>
/// One sub-problem in a Branch &amp; Bound tree (Branch &amp; Bound Simplex, Branch &amp; Bound Knapsack).
/// Shared contract for Members 2 and 3 — LP relaxation and knapsack B&amp;B both plug into this shape.
/// </summary>
public class BranchNode
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public LPModel SubProblem { get; set; } = new();
    public Tableau? OptimalTableau { get; set; }
    public double Bound { get; set; }
    public bool Fathomed { get; set; }
    public string FathomReason { get; set; } = string.Empty;
}
