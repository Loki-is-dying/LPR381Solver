using Solve.Models;

namespace Solve.SensitivityAnalysis;

public class SensitivityRange
{
    public string Label { get; set; } = "";
    public double CurrentValue { get; set; }
    public double Lower { get; set; }
    public double Upper { get; set; }
}

public class SensitivityOutcome
{
    public bool BasisChanged { get; set; }
    public string Message { get; set; } = "";
    public SimplexStatus Status { get; set; }
    public double[] Solution { get; set; } = System.Array.Empty<double>();
    public string[] SolutionLabels { get; set; } = System.Array.Empty<string>();
    public double ObjectiveValue { get; set; }
    public Tableau FinalTableau { get; set; } = null!;
}

public class DualityReport
{
    public LPModel DualModel { get; set; } = null!;
    public SimplexResult DualResult { get; set; } = null!;
    public double PrimalObjective { get; set; }
    public double DualObjective { get; set; }
    public double Gap { get; set; }
    public bool PrimalFeasible { get; set; }
    public bool DualFeasible { get; set; }
    public bool WeakDuality { get; set; }
    public bool StrongDuality { get; set; }
}

public class ShadowPriceReport
{
    public string[] ConstraintLabels { get; set; } = System.Array.Empty<string>();
    public double[] ShadowPrices { get; set; } = System.Array.Empty<double>();
}
