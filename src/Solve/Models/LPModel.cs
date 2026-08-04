namespace Solve.Models;

/// <summary>
/// A parsed Linear or Integer Programming model, exactly as entered in the input file
/// (not yet converted to canonical/standard form).
/// </summary>
public class LPModel
{
    public int NumVars { get; set; }
    public int NumConstraints { get; set; }
    public bool IsMaximisation { get; set; }

    /// <summary>Objective function coefficients, in variable order (x1, x2, ...).</summary>
    public double[] ObjectiveCoefficients { get; set; } = Array.Empty<double>();

    /// <summary>Technological coefficients: rows are constraints, columns are variables.</summary>
    public double[,] ConstraintMatrix { get; set; } = new double[0, 0];

    /// <summary>Relation for each constraint: "&lt;=", "&gt;=" or "=".</summary>
    public string[] ConstraintRelations { get; set; } = Array.Empty<string>();

    /// <summary>Right-hand-side value for each constraint.</summary>
    public double[] RHS { get; set; } = Array.Empty<double>();

    /// <summary>Sign restriction per variable: "+", "-", "urs", "int" or "bin".</summary>
    public string[] SignRestrictions { get; set; } = Array.Empty<string>();

    /// <summary>Display names for each variable ("x1", "x2", ...).</summary>
    public string[] VariableNames { get; set; } = Array.Empty<string>();

    /// <summary>Deep copy, used by algorithms that need to branch on a modified copy of the model.</summary>
    public LPModel Clone()
    {
        var clone = new LPModel
        {
            NumVars = NumVars,
            NumConstraints = NumConstraints,
            IsMaximisation = IsMaximisation,
            ObjectiveCoefficients = (double[])ObjectiveCoefficients.Clone(),
            ConstraintMatrix = (double[,])ConstraintMatrix.Clone(),
            ConstraintRelations = (string[])ConstraintRelations.Clone(),
            RHS = (double[])RHS.Clone(),
            SignRestrictions = (string[])SignRestrictions.Clone(),
            VariableNames = (string[])VariableNames.Clone(),
        };
        return clone;
    }

    /// <summary>Returns a new model with one extra constraint row appended (e.g. a branching bound or a Gomory cut).</summary>
    public LPModel WithExtraConstraint(double[] coefficients, string relation, double rhs)
    {
        var clone = Clone();
        int m = NumConstraints;
        var newMatrix = new double[m + 1, NumVars];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < NumVars; j++)
                newMatrix[i, j] = ConstraintMatrix[i, j];
        for (int j = 0; j < NumVars; j++)
            newMatrix[m, j] = coefficients[j];

        clone.ConstraintMatrix = newMatrix;
        clone.ConstraintRelations = ConstraintRelations.Append(relation).ToArray();
        clone.RHS = RHS.Append(rhs).ToArray();
        clone.NumConstraints = m + 1;
        return clone;
    }
}
