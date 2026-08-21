using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Ops 7-8 of the brief: ranging and applying a change to a single technological
/// coefficient a(i,j) in a non-basic variable's column (section 4.15). "Current value" is read
/// straight off the initial (pre-pivot) tableau, since that's exactly where
/// <see cref="Solve.Algorithms.CanonicalFormBuilder"/> wrote each column's raw coefficients —
/// this also makes the operation work uniformly for plain, slack and "urs"-split columns.</summary>
public static class ColumnRanging
{
    private const double Epsilon = 1e-9;

    /// <summary>Op 7 — how far a(i,j) can move, for non-basic column j and constraint row i
    /// (0-based), before that column's reduced cost goes negative: d'_j = d_j + Δ·yᵢ.</summary>
    public static SensitivityRange RangeNonBasicColumn(SensitivityContext ctx, string label, int constraintIndex)
    {
        int col = ctx.ColumnIndex(label);
        if (ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently basic — column ranging applies to non-basic variables only.");

        int objRow = ctx.Final.NumRows - 1;
        double reducedCost = ctx.Final.Data[objRow, col];
        double y = ctx.ShadowPricesInternal[constraintIndex];

        double lower = double.NegativeInfinity, upper = double.PositiveInfinity;
        if (y > Epsilon)
            lower = -reducedCost / y;
        else if (y < -Epsilon)
            upper = -reducedCost / y;
        // y == 0: constraint isn't binding on this column's price-out, so any change keeps optimality.

        double current = ctx.Result.CanonicalForm.Data[constraintIndex, col];
        return new SensitivityRange
        {
            Label = $"{label} (row b{constraintIndex + 1})",
            CurrentValue = current,
            Lower = current + lower,
            Upper = current + upper,
        };
    }

    /// <summary>Op 8 — apply Δ to a(i,j): updates that column's tableau entries via
    /// Δ·B⁻¹[:,i] and re-optimises if the reduced cost goes negative.</summary>
    public static SensitivityOutcome ApplyNonBasicColumnChange(SensitivityContext ctx, string label, int constraintIndex, double delta)
    {
        int col = ctx.ColumnIndex(label);
        if (ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently basic — column ranging applies to non-basic variables only.");

        var clone = ctx.Final.Clone();
        int objRow = clone.NumRows - 1;

        for (int r = 0; r < ctx.M; r++)
            clone.Data[r, col] += delta * ctx.BInverse[r, constraintIndex];
        clone.Data[objRow, col] += delta * ctx.ShadowPricesInternal[constraintIndex];

        var labelsBefore = (string[])clone.RowLabels.Clone();
        bool stillOptimal = clone.Data[objRow, col] >= -Epsilon;
        var status = stillOptimal ? SimplexStatus.Optimal : TableauEditor.ContinueToOptimal(clone);
        bool basisChanged = !labelsBefore.SequenceEqual(clone.RowLabels);

        var (solution, objective) = ctx.ExtractSolution(clone);
        string message = basisChanged
            ? "The change made the column attractive enough to enter the basis; re-optimised to a new basis."
            : "The current basis is still optimal; the solution is unchanged.";

        return new SensitivityOutcome
        {
            BasisChanged = basisChanged,
            Message = message,
            Status = status,
            Solution = solution,
            ObjectiveValue = objective,
            FinalTableau = clone,
        };
    }
}
