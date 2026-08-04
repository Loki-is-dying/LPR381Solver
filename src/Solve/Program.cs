using Solve.Algorithms;
using Solve.Models;
using Solve.Output;
using Solve.Parsing;

namespace Solve;

/// <summary>Menu-driven entry point. Loads a model, runs an algorithm, and (eventually) sensitivity
/// analysis. Every menu action is wrapped so a bad input file or an edge-case model reports a clear
/// message and returns to the menu instead of crashing.</summary>
class Program
{
    private static LPModel? _model;
    private static string? _inputFilePath;

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
        string? path = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("No path entered.");
            return;
        }

        try
        {
            _model = InputFileParser.Parse(path);
            _inputFilePath = path;
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
        Console.WriteLine("2. Revised Primal Simplex   (not yet implemented - Member 2)");
        Console.WriteLine("3. Branch & Bound Simplex   (not yet implemented - Member 2)");
        Console.WriteLine("4. Cutting Plane            (not yet implemented - Member 3)");
        Console.WriteLine("5. Branch & Bound Knapsack  (not yet implemented - Member 3)");
        Console.WriteLine("6. Back");
        Console.Write("> ");
        string? choice = Console.ReadLine();

        switch (choice?.Trim())
        {
            case "1": RunPrimalSimplex(); break;
            case "2":
            case "3":
            case "4":
            case "5":
                Console.WriteLine("Not yet implemented in this build.");
                break;
            case "6": break;
            default: Console.WriteLine("Please choose 1-6."); break;
        }
    }

    private static void RunPrimalSimplex()
    {
        try
        {
            var result = PrimalSimplex.Solve(_model!);

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

    private static void RunSensitivityMenu()
    {
        if (_model is null)
        {
            Console.WriteLine("Load and solve a model first.");
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
            Console.WriteLine("Not yet implemented in this build - Member 4 (Sensitivity Analysis).");
        else if (choice?.Trim() != (operations.Length + 1).ToString())
            Console.WriteLine("Please choose a valid option.");
    }
}
