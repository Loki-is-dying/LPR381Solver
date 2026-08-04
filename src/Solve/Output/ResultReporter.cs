using Solve.Models;
using Solve.Utils;

namespace Solve.Output;

/// <summary>
/// Formats a solved model for display. Every method takes a <see cref="TextWriter"/> so the exact
/// same output can be sent to the console and to the output file — the brief requires the file to
/// mirror the console, and writing the formatting logic once is the only way to guarantee that.
/// </summary>
public static class ResultReporter
{
    public static void WriteSimplexResult(TextWriter w, string algorithmName, LPModel model, SimplexResult result)
    {
        w.WriteLine($"=== {algorithmName} ===");
        WriteModelSummary(w, model);
        w.WriteLine();

        w.WriteLine("Canonical Form:");
        WriteTableau(w, result.CanonicalForm);
        w.WriteLine();

        for (int i = 0; i < result.Iterations.Count; i++)
        {
            var rec = result.IterationRecords[i];
            w.WriteLine($"{rec.Label}  (entering: {rec.EnteringVariable}, leaving: {rec.LeavingVariable}, " +
                        $"pivot element: {Rounding.R(rec.PivotElement)})");
            WriteThetaColumn(w, rec);
            WriteTableau(w, result.Iterations[i]);
            w.WriteLine();
        }

        switch (result.Status)
        {
            case SimplexStatus.Optimal:
                w.WriteLine("Status: OPTIMAL");
                for (int j = 0; j < model.NumVars; j++)
                    w.WriteLine($"  {model.VariableNames[j]} = {Rounding.R(result.Solution[j])}");
                w.WriteLine($"  z = {Rounding.R(result.ObjectiveValue)}");
                break;

            case SimplexStatus.Infeasible:
                w.WriteLine("Status: INFEASIBLE");
                w.WriteLine($"  {result.Message}");
                break;

            case SimplexStatus.Unbounded:
                w.WriteLine("Status: UNBOUNDED");
                w.WriteLine($"  {result.Message}");
                break;
        }
    }

    public static void WriteSimplexResultToFile(string filePath, string algorithmName, LPModel model, SimplexResult result)
    {
        using var writer = new StreamWriter(filePath, append: false);
        WriteSimplexResult(writer, algorithmName, model, result);
    }

    private static void WriteModelSummary(TextWriter w, LPModel model)
    {
        w.WriteLine($"{(model.IsMaximisation ? "max" : "min")} " +
                    string.Join(" ", model.ObjectiveCoefficients.Select(FormatSigned)));
        for (int i = 0; i < model.NumConstraints; i++)
        {
            var coeffs = Enumerable.Range(0, model.NumVars)
                .Select(j => FormatSigned(model.ConstraintMatrix[i, j]));
            w.WriteLine($"  {string.Join(" ", coeffs)} {model.ConstraintRelations[i]} {Rounding.R(model.RHS[i])}");
        }
        w.WriteLine($"  {string.Join(" ", model.SignRestrictions)}");
    }

    private static void WriteThetaColumn(TextWriter w, IterationRecord rec)
    {
        var parts = rec.ThetaColumn.Select(t => double.IsNaN(t) ? "-" : Rounding.R(t).ToString("0.000"));
        w.WriteLine($"  theta: [{string.Join(", ", parts)}]");
    }

    private static void WriteTableau(TextWriter w, Tableau t)
    {
        const int colWidth = 12;
        w.Write(string.Empty.PadRight(8));
        foreach (var label in t.ColumnLabels)
            w.Write(label.PadLeft(colWidth));
        w.WriteLine();

        for (int r = 0; r < t.NumRows; r++)
        {
            w.Write(t.RowLabels[r].PadRight(8));
            for (int c = 0; c < t.NumCols; c++)
                w.Write(Rounding.R(t.Data[r, c]).ToString("0.000").PadLeft(colWidth));
            w.WriteLine();
        }
    }

    private static string FormatSigned(double value) =>
        (value >= 0 ? "+" : "") + Rounding.R(value).ToString("0.###");
}
