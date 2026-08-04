namespace Solve.Models;

/// <summary>Outcome of running a simplex-family algorithm on a model.</summary>
public enum SimplexStatus
{
    Optimal,
    Infeasible,
    Unbounded,
}
