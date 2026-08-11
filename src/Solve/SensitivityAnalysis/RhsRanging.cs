using Solve.Models;

namespace Solve.SensitivityAnalysis;

/// <summary>Ops 5-6 of the brief: constraint RHS ranging and what-if changes (section 4.14).
/// Ranges are in the RHS's own units directly — no internal max/min sense conversion is needed
/// here, since only the objective row (not the constraint rows) is sign-flipped for "min" models.</summary>
public static class RhsRanging
{
    private const double Epsilon = 1e-9;

    /// <summary>Op 5 — how far constraint <paramref name="constraintIndex"/>'s RHS (0-based) can
    /// move while the current basis stays feasible, using β' = β + Δ·B⁻¹[:,i].</summary>
    public static SensitivityRange RangeRhs(SensitivityContext ctx, int constraintIndex)
    {
        int rhsCol = ctx.Final.NumCols - 1;
        double loDelta = double.NegativeInfinity;
        double hiDelta = double.PositiveInfinity;

        for (int r = 0; r < ctx.M; r++)
        {
            double coeff = ctx.BInverse[r, constraintIndex];
            double beta = ctx.Final.Data[r, rhsCol];
            if (coeff > Epsilon)
                loDelta = Math.Max(loDelta, -beta / coeff);
            else if (coeff < -Epsilon)
                hiDelta = Math.Min(hiDelta, -beta / coeff);
        }

        double current = ctx.Model.RHS[constraintIndex];
        return new SensitivityRange
        {
            Label = $"b{constraintIndex + 1}",
            CurrentValue = current,
            Lower = current + loDelta,
            Upper = current + hiDelta,
        };
    }

    /// <summary>Op 6 — apply Δ to constraint <paramref name="constraintIndex"/>'s RHS: updates
    /// every basic value and the objective value directly from B⁻¹, then restores feasibility
    /// with a dual-simplex pass if a basic value went negative.</summary>
    public static SensitivityOutcome ApplyRhsChange(SensitivityContext ctx, int constraintIndex, double delta)
    {
        var clone = ctx.Final.Clone();
        int rhsCol = clone.NumCols - 1;
        int objRow = clone.NumRows - 1;

        for (int r = 0; r < ctx.M; r++)
            clone.Data[r, rhsCol] += delta * ctx.BInverse[r, constraintIndex];
        clone.Data[objRow, rhsCol] += delta * ctx.ShadowPricesInternal[constraintIndex];

        var labelsBefore = (string[])clone.RowLabels.Clone();
        bool feasible = true;
        for (int r = 0; r < ctx.M; r++)
        {
            if (clone.Data[r, rhsCol] < -Epsilon) { feasible = false; break; }
        }

        SimplexStatus status;
        string message;
        if (feasible)
        {
            status = SimplexStatus.Optimal;
            message = "The current basis stayed feasible; values updated directly.";
        }
        else if (TableauEditor.RestoreFeasibility(clone))
        {
            status = SimplexStatus.Optimal;
            message = "The change made the previous basis infeasible; restored feasibility with dual simplex.";
        }
        else
        {
            status = SimplexStatus.Infeasible;
            message = "The change leaves the model infeasible.";
        }

        bool basisChanged = !labelsBefore.SequenceEqual(clone.RowLabels);
        var (solution, objective) = ctx.ExtractSolution(clone);

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
