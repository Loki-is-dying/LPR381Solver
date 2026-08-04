namespace Solve.Utils;

/// <summary>Every value displayed or written to the output file goes through this, per the brief's
/// "round to 3 decimal places" requirement.</summary>
public static class Rounding
{
    public static double R(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
