using Solve.Algorithms;
using Solve.Models;
using Solve.Output;
using Solve.Parsing;
using Solve.SensitivityAnalysis;
using Solve.Utils;
using System.Linq.Expressions;

namespace Solve;

/// <summary>Menu-driven entry point. Loads a model, runs an algorithm, and (eventually) sensitivity
/// analysis. Every menu action is wrapped so a bad input file or an edge-case model reports a clear
/// message and returns to the menu instead of crashing.</summary>
class Program
{
    private static LPModel? _model;
    private static string? _inputFilePath;
    private static SimplexResult? _lastSimplexResult;

    static void Main(string[] args)
    {
        Console.WriteLine("LPR381 Solver");
        Console.WriteLine("=============");

        bool running = true;
        while (running)
        {
            try
            {
                running = RunMainMenu();
            }
            catch (Exception ex)
            {
                // Safety net: no unhandled exception should ever take the program down.
                Console.WriteLine();
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        Console.WriteLine("Goodbye.");
    }

    private static bool RunMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine($"Loaded model: {(_model is null ? "(none)" : _inputFilePath)}");
        Console.WriteLine("1. Load input file");
        Console.WriteLine("2. Select and run an algorithm");
        Console.WriteLine("3. Sensitivity analysis");
        Console.WriteLine("4. Exit");
        Console.Write("> ");
        string? choice = Console.ReadLine();

        switch (choice?.Trim())
        {
            case "1": LoadInputFile(); break;
            case "2": RunAlgorithmMenu(); break;
            case "3": RunSensitivityMenu(); break;
            case "4": return false;
            default: Console.WriteLine("Please choose 1-4."); break;
        }
        return true;
    }

    private static void LoadInputFile()
    {
        Console.Write("Path to input file: ");
        string? path = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("No path entered.");
            return;
        }

        try
        {
            _model = InputFileParser.Parse(path);
            _inputFilePath = path;
            _lastSimplexResult = null;
            Console.WriteLine($"Loaded: {_model.NumVars} variable(s), {_model.NumConstraints} constraint(s), " +
                               $"{(_model.IsMaximisation ? "maximise" : "minimise")}.");
        }
        catch (InputFormatException ex)
        {
            Console.WriteLine($"Could not read input file: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Could not open input file: {ex.Message}");
        }
    }

    private static void RunAlgorithmMenu()
    {
        if (_model is null)
        {
            Console.WriteLine("Load an input file first (option 1).");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("1. Primal Simplex");
        Console.WriteLine("2. Revised Primal Simplex");
        Console.WriteLine("3. Branch & Bound Simplex");
        Console.WriteLine("4. Cutting Plane ");
        Console.WriteLine("5. Branch & Bound Knapsack");
        Console.WriteLine("6. Back");
        Console.Write("> ");
        string? choice = Console.ReadLine();

        switch (choice?.Trim())
        {
            case "1": RunPrimalSimplex(); break;
            case "2": RunRevisedSimplex(); break;
            case "3": RunBranchAndBound(); break;
            case "4": RunCuttingPlane(); break;
            case "5": RunBranchAndBoundKnapsack(); break;
            case "6": break;
            default: Console.WriteLine("Please choose 1-6."); break;
        }
    }

    private static void RunPrimalSimplex()
    {
        try
        {
            var result = PrimalSimplex.Solve(_model!);
            _lastSimplexResult = result;

            Console.WriteLine();
            ResultReporter.WriteSimplexResult(Console.Out, "Primal Simplex", _model!, result);

            Console.Write("Output file path (blank to skip): ");
            string? outPath = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(outPath))
            {
                ResultReporter.WriteSimplexResultToFile(outPath, "Primal Simplex", _model!, result);
                Console.WriteLine($"Written to {outPath}");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Could not write output file: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Model could not be solved: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Model could not be solved: {ex.Message}");
        }
    }
    private static void RunRevisedSimplex()
{
    try
    {
        var result = RevisedPrimalSimplex.Solve(_model!);
        _lastSimplexResult = result;

        Console.WriteLine();
        ResultReporter.WriteSimplexResult(Console.Out, "Revised Primal Simplex", _model!, result);

        Console.Write("Output file path (blank to skip): ");
        string? outPath = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            ResultReporter.WriteSimplexResultToFile(outPath, "Revised Primal Simplex", _model!, result);
            Console.WriteLine($"Written to {outPath}");
        }
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Could not write output file: {ex.Message}");
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Model could not be solved: {ex.Message}");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Model could not be solved: {ex.Message}");
    }
}

private static void RunBranchAndBound()
{
    try
    {
        var result = BranchAndBoundSimplex.Solve(_model!);

        Console.WriteLine();
        Console.WriteLine($"Branch & Bound Simplex - status: {result.Status}");
        if (!string.IsNullOrEmpty(result.Message))
            Console.WriteLine(result.Message);
        Console.WriteLine();

        foreach (var node in result.AllNodes)
        {
            string description = result.BranchDescriptions.TryGetValue(node.Id, out var d) ? d : "";
            Console.WriteLine($"Node {node.Id} (parent {node.ParentId}) - {description}");
            Console.WriteLine($"  Bound = {Rounding.R(node.Bound)}");
            if (node.Fathomed)
                Console.WriteLine($"  Fathomed: {node.FathomReason}");
            Console.WriteLine();
        }

        if (result.Status == SimplexStatus.Optimal && result.BestNode is not null)
        {
            Console.WriteLine($"Best candidate: Node {result.BestNode.Id}");
            for (int j = 0; j < result.Solution.Length; j++)
            {
                string name = j < _model!.VariableNames.Length ? _model.VariableNames[j] : $"x{j + 1}";
                Console.WriteLine($"  {name} = {Rounding.R(result.Solution[j])}");
            }
            Console.WriteLine($"  z = {Rounding.R(result.ObjectiveValue)}");
        }

        Console.Write("Output file path (blank to skip): ");
        string? outPath = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            WriteBranchAndBoundToFile(outPath, result);
            Console.WriteLine($"Written to {outPath}");
        }
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Could not write output file: {ex.Message}");
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Model could not be solved: {ex.Message}");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Model could not be solved: {ex.Message}");
    }
}
private static void WriteBranchAndBoundToFile(string path, BranchAndBoundSimplex.Result result)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine($"Branch & Bound Simplex - status: {result.Status}");
    if (!string.IsNullOrEmpty(result.Message))
        writer.WriteLine(result.Message);
    writer.WriteLine();

    foreach (var node in result.AllNodes)
    {
        string description = result.BranchDescriptions.TryGetValue(node.Id, out var d) ? d : "";
        writer.WriteLine($"Node {node.Id} (parent {node.ParentId}) - {description}");
        writer.WriteLine($"  Bound = {Rounding.R(node.Bound)}");
        if (node.Fathomed)
            writer.WriteLine($"  Fathomed: {node.FathomReason}");
        writer.WriteLine();
    }

    if (result.Status == SimplexStatus.Optimal && result.BestNode is not null)
    {
        writer.WriteLine($"Best candidate: Node {result.BestNode.Id}");
        for (int j = 0; j < result.Solution.Length; j++)
        {
            string name = j < _model!.VariableNames.Length ? _model.VariableNames[j] : $"x{j + 1}";
            writer.WriteLine($"  {name} = {Rounding.R(result.Solution[j])}");
        }
        writer.WriteLine($"  z = {Rounding.R(result.ObjectiveValue)}");
    }
}

    private static void RunCuttingPlane()
    {
        try
        {
            var result = CuttingPlane.Solve(_model!);
            Console.WriteLine();
            Console.WriteLine(result.ToString());

            Console.WriteLine();

            Console.Write("Output file path");
            string? outPath = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(outPath))
            {
                File.WriteAllText(outPath, result.ToString());
                Console.WriteLine($"Written to {outPath}");
            }

        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Model could not be solved :{ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Model could not be solved: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Could not write output file: {ex.Message}");
        }
    }
    private static void RunBranchAndBoundKnapsack()
    {
        try
        {
            var result = BranchAndBoundKnapsack.Solve(_model!);

            Console.WriteLine();
            Console.WriteLine(result.ToString());

            Console.WriteLine();
            Console.WriteLine("Output file path");
            string? outPath = Console.ReadLine();

            

        }
        catch (IOException ex)
        {

            Console.WriteLine($"Could not write output file : {ex.Message}");



        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"model could not be solved : {ex.Message}");

        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"model could not be solved : {ex.Message}");
        }

}

    private static void RunSensitivityMenu()
    {
        if (_model is null)
        {
            Console.WriteLine("Load a model first.");
            return;
        }

        if (_lastSimplexResult is null || _lastSimplexResult.Status != SimplexStatus.Optimal)
        {
            Console.WriteLine("Solve the model with Primal Simplex or Revised Primal Simplex first.");
            return;
        }

        string[] operations =
        {
            "Range of a selected Non-Basic Variable",
            "Apply a change to a selected Non-Basic Variable",
            "Range of a selected Basic Variable",
            "Apply a change to a selected Basic Variable",
            "Range of a selected constraint RHS value",
            "Apply a change to a selected constraint RHS value",
            "Range of a variable in a Non-Basic column",
            "Apply a change to a variable in a Non-Basic column",
            "Add a new activity",
            "Add a new constraint",
            "Display shadow prices",
            "Duality (apply / solve dual / verify strong-or-weak)",
        };

        Console.WriteLine();
        for (int i = 0; i < operations.Length; i++)
            Console.WriteLine($"{i + 1}. {operations[i]}");
        Console.WriteLine($"{operations.Length + 1}. Back");
        Console.Write("> ");
        string? choice = Console.ReadLine();

        if (int.TryParse(choice, out int index) && index >= 1 && index <= operations.Length)
        {
            try
            {
                var context = SensitivityContext.From(_model, _lastSimplexResult);
                RunSensitivityOperation(index, context);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Invalid sensitivity input: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Sensitivity analysis failed: {ex.Message}");
            }
        }
        else if (choice?.Trim() != (operations.Length + 1).ToString())
            Console.WriteLine("Please choose a valid option.");
    }

    private static void RunSensitivityOperation(int index, SensitivityContext context)
    {
        switch (index)
        {
            case 1:
                PrintRange(ObjectiveRanging.RangeNonBasic(context, SelectColumn(context, false)));
                break;
            case 2:
                PrintOutcome(ObjectiveRanging.ApplyNonBasicChange(context, SelectColumn(context, false), ReadDouble("Objective coefficient change Δ: ")));
                break;
            case 3:
                PrintRange(ObjectiveRanging.RangeBasic(context, SelectColumn(context, true)));
                break;
            case 4:
                PrintOutcome(ObjectiveRanging.ApplyBasicChange(context, SelectColumn(context, true), ReadDouble("Objective coefficient change Δ: ")));
                break;
            case 5:
                PrintRange(RhsRanging.RangeRhs(context, ReadConstraintIndex(context)));
                break;
            case 6:
                PrintOutcome(RhsRanging.ApplyRhsChange(context, ReadConstraintIndex(context), ReadDouble("RHS change Δ: ")));
                break;
            case 7:
                PrintRange(ColumnRanging.RangeNonBasicColumn(context, SelectColumn(context, false), ReadConstraintIndex(context)));
                break;
            case 8:
                PrintOutcome(ColumnRanging.ApplyNonBasicColumnChange(context, SelectColumn(context, false), ReadConstraintIndex(context), ReadDouble("Column coefficient change Δ: ")));
                break;
            case 9:
                PrintOutcome(StructuralChanges.AddActivity(context, ReadText("New activity name: "),
                    ReadDouble("Objective coefficient: "), ReadCoefficients(context.M, "Constraint coefficient")));
                break;
            case 10:
                PrintOutcome(StructuralChanges.AddConstraint(context, ReadCoefficients(context.Model.NumVars, "Variable coefficient"),
                    ReadRelation(), ReadDouble("RHS: ")));
                break;
            case 11:
                var shadow = ShadowPrices.Compute(context);
                Console.WriteLine("Shadow prices:");
                for (int i = 0; i < shadow.ConstraintLabels.Length; i++)
                    Console.WriteLine($"  {shadow.ConstraintLabels[i]} = {Rounding.R(shadow.ShadowPrices[i])}");
                break;
            case 12:
                PrintDuality(Duality.Analyze(context.Model, context.Result));
                break;
        }
    }

    private static string SelectColumn(SensitivityContext context, bool basic)
    {
        var columns = Enumerable.Range(0, context.Final.NumCols - 1)
            .Where(column => context.IsBasic(column) == basic && !context.ArtificialColumns.Contains(column))
            .Select(column => context.Final.ColumnLabels[column])
            .ToArray();
        if (columns.Length == 0)
            throw new ArgumentException(basic ? "There are no basic decision/slack columns available." : "There are no non-basic columns available.");

        Console.WriteLine(basic ? "Basic columns:" : "Non-basic columns:");
        for (int i = 0; i < columns.Length; i++)
            Console.WriteLine($"{i + 1}. {columns[i]}");
        int selected = ReadInt("Select a column: ") - 1;
        if (selected < 0 || selected >= columns.Length)
            throw new ArgumentException("Column selection is out of range.");
        return columns[selected];
    }

    private static int ReadConstraintIndex(SensitivityContext context)
    {
        int selected = ReadInt($"Constraint (1-{context.M}): ") - 1;
        if (selected < 0 || selected >= context.M)
            throw new ArgumentException("Constraint selection is out of range.");
        return selected;
    }

    private static double[] ReadCoefficients(int count, string label)
    {
        var values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = ReadDouble($"{label} {i + 1}: ");
        return values;
    }

    private static string ReadRelation()
    {
        string relation = ReadText("Relation (<=, >=, =): ");
        if (relation is not "<=" and not ">=" and not "=")
            throw new ArgumentException("Relation must be <=, >=, or =.");
        return relation;
    }

    private static string ReadText(string prompt)
    {
        Console.Write(prompt);
        string? value = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A value is required.");
        return value;
    }

    private static int ReadInt(string prompt)
    {
        if (int.TryParse(ReadText(prompt), out int value))
            return value;
        throw new ArgumentException("Expected a whole number.");
    }

    private static double ReadDouble(string prompt)
    {
        if (double.TryParse(ReadText(prompt), out double value) && !double.IsNaN(value))
            return value;
        throw new ArgumentException("Expected a number.");
    }

    private static void PrintRange(SensitivityRange range)
    {
        Console.WriteLine($"{range.Label}: current={Rounding.R(range.CurrentValue)}, " +
                          $"allowable range=[{Rounding.R(range.Lower)}, {Rounding.R(range.Upper)}]");
    }

    private static void PrintOutcome(SensitivityOutcome outcome)
    {
        Console.WriteLine(outcome.Message);
        Console.WriteLine($"Status: {outcome.Status}; objective={Rounding.R(outcome.ObjectiveValue)}");
        for (int i = 0; i < outcome.Solution.Length; i++)
        {
            string name = i < outcome.SolutionLabels.Length
                ? outcome.SolutionLabels[i]
                : i < _model!.VariableNames.Length ? _model.VariableNames[i] : $"x{i + 1}";
            Console.WriteLine($"  {name} = {Rounding.R(outcome.Solution[i])}");
        }
    }

    private static void PrintDuality(DualityReport report)
    {
        Console.WriteLine($"Primal feasible: {report.PrimalFeasible}; dual feasible: {report.DualFeasible}");
        Console.WriteLine($"Primal objective: {Rounding.R(report.PrimalObjective)}");
        Console.WriteLine($"Dual status: {report.DualResult.Status}");
        Console.WriteLine($"Dual objective: {Rounding.R(report.DualObjective)}");
        Console.WriteLine($"Gap: {Rounding.R(report.Gap)}");
        Console.WriteLine(report.WeakDuality ? "Weak duality verified." : "Weak duality was not verified.");
        Console.WriteLine(report.StrongDuality ? "Strong duality verified." : "Strong duality was not verified; weak duality may still hold.");
    }
}
