using Solve.Algorithms;
using Solve.Models;
using Solve.Parsing;
using Solve.SensitivityAnalysis;

string samplePath = args.Length > 0
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "samples", "santas_workshop.txt");

int passed = 0;
Check("Santa optimal solution", () =>
{
    var model = InputFileParser.Parse(samplePath);
    var result = PrimalSimplex.Solve(model);
    Assert(result.Status == SimplexStatus.Optimal, "Santa model is not optimal.");
    AssertNear(result.Solution[0], 20, "x1");
    AssertNear(result.Solution[1], 60, "x2");
    AssertNear(result.ObjectiveValue, 180, "objective");
    return true;
});

Check("Santa sensitivity reference", () =>
{
    var model = InputFileParser.Parse(samplePath);
    var result = PrimalSimplex.Solve(model);
    var context = SensitivityContext.From(model, result);
    var x1 = ObjectiveRanging.RangeBasic(context, "x1");
    var x2 = ObjectiveRanging.RangeBasic(context, "x2");
    var b1 = RhsRanging.RangeRhs(context, 0);
    var b2 = RhsRanging.RangeRhs(context, 1);
    var b3 = RhsRanging.RangeRhs(context, 2);
    AssertRange(x1, 2, 4);
    AssertRange(x2, 1.5, 3);
    AssertRange(b1, 80, 120);
    AssertRange(b2, 60, 100);
    AssertNear(b3.Lower, 20, "b3 lower");
    Assert(double.IsPositiveInfinity(b3.Upper), "b3 upper is not infinity.");
    var shadow = ShadowPrices.Compute(context).ShadowPrices;
    AssertNear(shadow[0], 1, "y1");
    AssertNear(shadow[1], 1, "y2");
    AssertNear(shadow[2], 0, "y3");
    var dual = Duality.Analyze(model, result);
    Assert(dual.PrimalFeasible && dual.DualFeasible, "primal/dual feasibility failed.");
    Assert(dual.WeakDuality && dual.StrongDuality, "duality verification failed.");
    AssertNear(dual.DualObjective, 180, "dual objective");
    return true;
});

Check("Added activity is returned", () =>
{
    var model = InputFileParser.Parse(samplePath);
    var result = PrimalSimplex.Solve(model);
    var context = SensitivityContext.From(model, result);
    var outcome = StructuralChanges.AddActivity(context, "x3", 1, new[] { 1d, 1d, 1d });
    Assert(outcome.Solution.Length == 3, "added activity missing from solution.");
    Assert(outcome.SolutionLabels[^1] == "x3", "added activity label missing.");
    return true;
});

Check("Added equality is enforced", () =>
{
    var model = new LPModel
    {
        NumVars = 1,
        NumConstraints = 1,
        IsMaximisation = true,
        ObjectiveCoefficients = new[] { 1d },
        ConstraintMatrix = new double[,] { { 1d } },
        ConstraintRelations = new[] { "<=" },
        RHS = new[] { 10d },
        SignRestrictions = new[] { "+" },
        VariableNames = new[] { "x1" },
    };
    var result = PrimalSimplex.Solve(model);
    var context = SensitivityContext.From(model, result);
    var outcome = StructuralChanges.AddConstraint(context, new[] { 1d }, "=", 3);
    Assert(outcome.Status == SimplexStatus.Optimal, "equality extension did not solve.");
    AssertNear(outcome.Solution[0], 3, "equality solution");
    return true;
});

Console.WriteLine($"{passed} sensitivity checks passed.");
return 0;

void Check(string name, Func<bool> test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL: {name}: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

void AssertNear(double actual, double expected, string label)
{
    if (double.IsNaN(actual) || Math.Abs(actual - expected) > 1e-6)
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
}

void AssertRange(SensitivityRange range, double lower, double upper)
{
    AssertNear(range.Lower, lower, $"{range.Label} lower");
    AssertNear(range.Upper, upper, $"{range.Label} upper");
}
