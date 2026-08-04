using System.Globalization;
using System.Text.RegularExpressions;
using Solve.Models;

namespace Solve.Parsing;

/// <summary>
/// Reads the plain-text LP/IP model format defined in the project brief:
///   line 1   -> max|min, then a signed coefficient per decision variable
///   lines    -> one per constraint: a signed coefficient per variable, a relation, an RHS
///   last line-> one sign restriction per variable (+, -, urs, int, bin)
/// Accepts any number of decision variables and any number of constraints.
/// </summary>
public static class InputFileParser
{
    private static readonly Regex RelationRhsPattern =
        new(@"^(<=|>=|=)\s*([+-]?\d+(\.\d+)?)$", RegexOptions.Compiled);

    public static LPModel Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InputFormatException($"Input file not found: \"{filePath}\".");

        var lines = File.ReadAllLines(filePath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2)
            throw new InputFormatException(
                "Input file must have at least an objective line and a sign-restriction line.");

        var objectiveTokens = Split(lines[0]);
        if (objectiveTokens.Length < 2)
            throw new InputFormatException(
                "Line 1 must contain 'max' or 'min' followed by at least one objective coefficient.");

        bool isMax = objectiveTokens[0].ToLowerInvariant() switch
        {
            "max" => true,
            "min" => false,
            _ => throw new InputFormatException(
                $"Line 1 must start with 'max' or 'min', found \"{objectiveTokens[0]}\"."),
        };

        int numVars = objectiveTokens.Length - 1;
        var objectiveCoefficients = ParseCoefficients(objectiveTokens.Skip(1).ToArray(), 1, "objective");

        int numConstraints = lines.Length - 2;
        if (numConstraints < 1)
            throw new InputFormatException(
                "Input file must contain at least one constraint line between the objective and the sign restrictions.");

        var constraintMatrix = new double[numConstraints, numVars];
        var relations = new string[numConstraints];
        var rhs = new double[numConstraints];

        for (int i = 0; i < numConstraints; i++)
        {
            int lineNumber = i + 2; // 1-based, line 1 is the objective
            var tokens = Split(lines[1 + i]);
            if (tokens.Length < numVars + 1)
                throw new InputFormatException(
                    $"Line {lineNumber}: expected {numVars} coefficients plus a relation and RHS, " +
                    $"found only {tokens.Length} token(s).");

            var coeffTokens = tokens.Take(numVars).ToArray();
            var coefficients = ParseCoefficients(coeffTokens, lineNumber, "constraint");
            for (int j = 0; j < numVars; j++)
                constraintMatrix[i, j] = coefficients[j];

            // Everything after the coefficients is the relation + RHS, whether fused
            // ("<=40") or given as separate tokens ("<=" "40").
            string tail = string.Concat(tokens.Skip(numVars));
            var match = RelationRhsPattern.Match(tail);
            if (!match.Success)
                throw new InputFormatException(
                    $"Line {lineNumber}: could not read a relation (<=, >=, =) and RHS value from \"{tail}\".");

            relations[i] = match.Groups[1].Value;
            rhs[i] = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        var signTokens = Split(lines[^1]);
        if (signTokens.Length != numVars)
            throw new InputFormatException(
                $"Sign-restriction line must contain exactly {numVars} entries (one per variable), " +
                $"found {signTokens.Length}.");

        var signRestrictions = new string[numVars];
        for (int j = 0; j < numVars; j++)
        {
            string token = signTokens[j];
            string normalized = token.ToLowerInvariant() switch
            {
                "+" => "+",
                "-" => "-",
                "urs" => "urs",
                "int" => "int",
                "bin" => "bin",
                _ => throw new InputFormatException(
                    $"Sign restriction {j + 1} must be one of +, -, urs, int, bin — found \"{token}\"."),
            };
            signRestrictions[j] = normalized;
        }

        var variableNames = Enumerable.Range(1, numVars).Select(k => $"x{k}").ToArray();

        return new LPModel
        {
            NumVars = numVars,
            NumConstraints = numConstraints,
            IsMaximisation = isMax,
            ObjectiveCoefficients = objectiveCoefficients,
            ConstraintMatrix = constraintMatrix,
            ConstraintRelations = relations,
            RHS = rhs,
            SignRestrictions = signRestrictions,
            VariableNames = variableNames,
        };
    }

    private static double[] ParseCoefficients(string[] tokens, int lineNumber, string context)
    {
        var result = new double[tokens.Length];
        for (int j = 0; j < tokens.Length; j++)
        {
            if (!double.TryParse(tokens[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new InputFormatException(
                    $"Line {lineNumber}: could not read {context} coefficient {j + 1} (\"{tokens[j]}\") as a signed number.");
            result[j] = value;
        }
        return result;
    }

    private static string[] Split(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
