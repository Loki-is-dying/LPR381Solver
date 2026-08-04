using Solve.Models;

namespace Solve.Algorithms;

/// <summary>Everything Primal Simplex needs beyond the raw Tableau: which columns are artificial
/// (for infeasibility detection) and which tableau column(s) each original decision variable maps to
/// (a variable marked "urs" is split into a positive and a negative part).</summary>
public class CanonicalForm
{
    public Tableau Tableau { get; init; } = new();
    public List<int> ArtificialColumns { get; init; } = new();

    /// <summary>decisionVarColumns[j] = tableau column index(es) for original variable j.
    /// Length 1 normally; length 2 ([positivePart, negativePart]) for a "urs" variable.</summary>
    public int[][] DecisionVarColumns { get; init; } = Array.Empty<int[]>();
}

/// <summary>
/// Converts a parsed <see cref="LPModel"/> into an initial simplex tableau (the "canonical form"
/// the brief asks to be displayed before any algorithm runs): adds slack/surplus/artificial
/// variables per constraint, splits "urs" variables into a positive and negative part, and — because
/// artificial variables are penalised at Big-M in the objective row — reduces that row so basic
/// artificial columns start at zero, as the Big-M method requires.
/// </summary>
public static class CanonicalFormBuilder
{
    /// <summary>Penalty cost for artificial variables. Large relative to realistic objective coefficients.</summary>
    public const double BigM = 1_000_000d;

    public static CanonicalForm Build(LPModel model)
    {
        int m = model.NumConstraints;

        // Work out how many tableau columns each decision variable needs (2 for urs, else 1),
        // and how many slack/surplus/artificial columns each constraint needs.
        var decisionVarColumns = new int[model.NumVars][];
        int col = 0;
        var varStartCol = new int[model.NumVars];
        for (int j = 0; j < model.NumVars; j++)
        {
            varStartCol[j] = col;
            col += model.SignRestrictions[j] == "urs" ? 2 : 1;
        }
        int decisionCols = col;

        int extraCols = 0;
        foreach (var relation in model.ConstraintRelations)
            extraCols += relation switch { "<=" => 1, ">=" => 2, "=" => 1, _ => 0 };

        int totalCols = decisionCols + extraCols + 1; // +1 for RHS
        int totalRows = m + 1;                        // +1 for objective row

        var data = new double[totalRows, totalCols];
        var columnLabels = new string[totalCols];
        var rowLabels = new string[totalRows];
        var artificialColumns = new List<int>();

        for (int j = 0; j < model.NumVars; j++)
        {
            if (model.SignRestrictions[j] == "urs")
            {
                columnLabels[varStartCol[j]] = model.VariableNames[j] + "+";
                columnLabels[varStartCol[j] + 1] = model.VariableNames[j] + "-";
                decisionVarColumns[j] = new[] { varStartCol[j], varStartCol[j] + 1 };
            }
            else
            {
                columnLabels[varStartCol[j]] = model.VariableNames[j];
                decisionVarColumns[j] = new[] { varStartCol[j] };
            }
        }

        int rhsCol = totalCols - 1;
        columnLabels[rhsCol] = "RHS";

        col = decisionCols;
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < model.NumVars; j++)
            {
                double a = model.ConstraintMatrix[i, j];
                if (model.SignRestrictions[j] == "urs")
                {
                    data[i, decisionVarColumns[j][0]] = a;
                    data[i, decisionVarColumns[j][1]] = -a;
                }
                else
                {
                    data[i, decisionVarColumns[j][0]] = a;
                }
            }
            data[i, rhsCol] = model.RHS[i];

            switch (model.ConstraintRelations[i])
            {
                case "<=":
                    data[i, col] = 1;
                    columnLabels[col] = $"s{i + 1}";
                    rowLabels[i] = columnLabels[col];
                    col++;
                    break;

                case ">=":
                    data[i, col] = -1;
                    columnLabels[col] = $"s{i + 1}";
                    col++;
                    data[i, col] = 1;
                    columnLabels[col] = $"a{i + 1}";
                    artificialColumns.Add(col);
                    rowLabels[i] = columnLabels[col];
                    col++;
                    break;

                case "=":
                    data[i, col] = 1;
                    columnLabels[col] = $"a{i + 1}";
                    artificialColumns.Add(col);
                    rowLabels[i] = columnLabels[col];
                    col++;
                    break;

                default:
                    throw new ArgumentException($"Unsupported constraint relation \"{model.ConstraintRelations[i]}\".");
            }
        }

        // Objective row. Internally we always maximise; a "min" model is solved as
        // "max of the negated objective" and the sign is flipped back when the result is read.
        int objRow = m;
        rowLabels[objRow] = "z";
        double sense = model.IsMaximisation ? 1d : -1d;
        for (int j = 0; j < model.NumVars; j++)
        {
            double c = sense * model.ObjectiveCoefficients[j];
            data[objRow, decisionVarColumns[j][0]] = -c;
            if (model.SignRestrictions[j] == "urs")
                data[objRow, decisionVarColumns[j][1]] = c;
        }
        foreach (int a in artificialColumns)
            data[objRow, a] = BigM;

        // Big-M initialisation: basic artificial columns must read 0 in the objective row,
        // so cancel each one by subtracting BigM times its own constraint row.
        for (int i = 0; i < m; i++)
        {
            if (artificialColumns.Contains(ColumnIndexOfBasic(rowLabels[i], columnLabels)))
            {
                for (int c = 0; c < totalCols; c++)
                    data[objRow, c] -= BigM * data[i, c];
            }
        }

        var tableau = new Tableau
        {
            Data = data,
            ColumnLabels = columnLabels,
            RowLabels = rowLabels,
            NumRows = totalRows,
            NumCols = totalCols,
        };

        return new CanonicalForm
        {
            Tableau = tableau,
            ArtificialColumns = artificialColumns,
            DecisionVarColumns = decisionVarColumns,
        };
    }

    private static int ColumnIndexOfBasic(string rowLabel, string[] columnLabels) =>
        Array.IndexOf(columnLabels, rowLabel);
}
