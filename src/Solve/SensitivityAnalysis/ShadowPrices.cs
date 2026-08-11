using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Op 11 of the brief: shadow price (dual value) yᵢ = CbvB⁻¹ for every constraint,
/// in the model's original objective units (section 4.17).</summary>
public static class ShadowPrices
{
    public static ShadowPriceReport Compute(SensitivityContext ctx)
    {
        var labels = Enumerable.Range(1, ctx.M).Select(i => $"b{i}").ToArray();
        return new ShadowPriceReport
        {
            ConstraintLabels = labels,
            ShadowPrices = (double[])ctx.ShadowPricesOriginal.Clone(),
        };
    }
}
