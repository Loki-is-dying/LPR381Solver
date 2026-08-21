using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Ops 1-4 of the brief: objective-function-coefficient ranging and what-if changes,
/// for both non-basic and basic variables (section 4.13).</summary>
public static class ObjectiveRanging
{
    private const double Epsilon = 1e-9;

    /// <summary>Op 1 — how far a non-basic variable's objective coefficient can move before it
    /// becomes attractive enough to enter the basis.</summary>
    public static SensitivityRange RangeNonBasic(SensitivityContext ctx, string label)
    {
        int col = ctx.ColumnIndex(label);
        if (ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently basic — use the Basic Variable range operation.");

        int objRow = ctx.Final.NumRows - 1;
        double reducedCost = ctx.Final.Data[objRow, col]; // >= 0 at optimality

        var (lower, upper) = SensitivityContext.ConvertDeltaRange(double.NegativeInfinity, reducedCost, ctx.Sense);
        double current = ctx.OriginalObjectiveCoefficient(col);
        return new SensitivityRange { Label = label, CurrentValue = current, Lower = current + lower, Upper = current + upper };
    }

    /// <summary>Op 2 — apply Δ to a non-basic variable's objective coefficient. The basis stays
    /// optimal as long as Δ stays inside the range from <see cref="RangeNonBasic"/>; otherwise
    /// the variable becomes attractive and simplex continues until a new optimum is found.</summary>
    public static SensitivityOutcome ApplyNonBasicChange(SensitivityContext ctx, string label, double delta)
    {
        int col = ctx.ColumnIndex(label);
        if (ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently basic — use the Basic Variable change operation.");

        var clone = ctx.Final.Clone();
        int objRow = clone.NumRows - 1;
        double deltaInternal = ctx.Sense * delta;
        clone.Data[objRow, col] -= deltaInternal;

        return Finish(ctx, clone, stillOptimalBefore: clone.Data[objRow, col] >= -Epsilon);
    }

    /// <summary>Op 3 — how far a basic variable's objective coefficient can move before some
    /// other (non-basic) variable becomes attractive enough to displace it.</summary>
    public static SensitivityRange RangeBasic(SensitivityContext ctx, string label)
    {
        int col = ctx.ColumnIndex(label);
        if (!ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently non-basic — use the Non-Basic Variable range operation.");

        int row = ctx.RowOf(col);
        int objRow = ctx.Final.NumRows - 1;

        double loInternal = double.NegativeInfinity;
        double hiInternal = double.PositiveInfinity;
        foreach (int j in ctx.NonBasicColumns())
        {
            double a = ctx.Final.Data[row, j];
            double d = ctx.Final.Data[objRow, j];
            if (a > Epsilon)
                loInternal = Math.Max(loInternal, -d / a);
            else if (a < -Epsilon)
                hiInternal = Math.Min(hiInternal, -d / a);
        }

        var (lower, upper) = SensitivityContext.ConvertDeltaRange(loInternal, hiInternal, ctx.Sense);
        double current = ctx.OriginalObjectiveCoefficient(col);
        return new SensitivityRange { Label = label, CurrentValue = current, Lower = current + lower, Upper = current + upper };
    }

    /// <summary>Op 4 — apply Δ to a basic variable's objective coefficient: updates CbvB⁻¹ (the
    /// whole objective row), and re-optimises if some reduced cost goes negative.</summary>
    public static SensitivityOutcome ApplyBasicChange(SensitivityContext ctx, string label, double delta)
    {
        int col = ctx.ColumnIndex(label);
        if (!ctx.IsBasic(col))
            throw new ArgumentException($"\"{label}\" is currently non-basic — use the Non-Basic Variable change operation.");

        int row = ctx.RowOf(col);
        var clone = ctx.Final.Clone();
        int objRow = clone.NumRows - 1;
        double deltaInternal = ctx.Sense * delta;

        for (int j = 0; j < clone.NumCols; j++)
            clone.Data[objRow, j] += deltaInternal * clone.Data[row, j];
        clone.Data[objRow, col] = 0d; // basic column's reduced cost is always exactly 0

        bool stillOptimal = ctx.NonBasicColumns().All(j => clone.Data[objRow, j] >= -Epsilon);
        return Finish(ctx, clone, stillOptimal);
    }

    private static SensitivityOutcome Finish(SensitivityContext ctx, Tableau clone, bool stillOptimalBefore)
    {
        var labelsBefore = (string[])clone.RowLabels.Clone();
        var status = stillOptimalBefore ? SimplexStatus.Optimal : TableauEditor.ContinueToOptimal(clone);
        bool basisChanged = !labelsBefore.SequenceEqual(clone.RowLabels);

        var (solution, objective) = ctx.ExtractSolution(clone);
        string message = status switch
        {
            SimplexStatus.Optimal when !basisChanged => "The current basis is still optimal; the solution is unchanged.",
            SimplexStatus.Optimal => "The change made the previous basis sub-optimal; re-optimised to a new basis.",
            SimplexStatus.Unbounded => "The change makes the objective unbounded.",
            _ => "Could not resolve the change to a new optimum.",
        };

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
