using Solve.Models;
using Solve.Utils;

namespace Solve.Algorithms;

/// <summary>
/// Branch &amp; Bound Simplex Algorithm. Member 2 responsibility -- the single
/// highest-weighted algorithm in the project.
///
/// Solves an Integer/Binary Programming model by:
///   1. Solving each node's LP relaxation via <see cref="PrimalSimplex"/>.
///   2. If a node's relaxation is already integer, it is a candidate solution.
///   3. Otherwise branching on a fractional integer/binary variable into a floor ("&lt;=")
///      and a ceiling ("&gt;=") sub-problem, adding both to the open list.
///   4. Repeatedly selecting the best open candidate (best-bound-first: highest LP bound
///      for maximisation), solving it, and fathoming it when it is infeasible, cannot beat
///      the current best integer solution, or is itself integer-feasible.
///   5. Backtracking to the next open node once a branch is exhausted.
///

public static class BranchAndBoundSimplex
{
    public class Result
    {
        public SimplexStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public BranchNode? BestNode { get; set; }
        public double[] Solution { get; set; } = Array.Empty<double>();
        public double ObjectiveValue { get; set; }
        public List<BranchNode> AllNodes { get; } = new();
        public Dictionary<int, string> BranchDescriptions { get; } = new();

        /// <summary>Every LP-relaxation tableau iteration from every node, labelled by node Id,
        /// for the output file's "Node N - T-k" style listing.</summary>
        public List<(int NodeId, Tableau Tableau, IterationRecord Record)> AllIterations { get; } = new();
    }

    private const double IntegerTolerance = 1e-6;

    public static Result Solve(LPModel rootModel)
    {
        var result = new Result();
        var openNodes = new List<BranchNode>();
        var nodeSolutions = new Dictionary<int, double[]>();

        bool isMax = rootModel.IsMaximisation;
        double bestObjective = isMax ? double.NegativeInfinity : double.PositiveInfinity;
        BranchNode? bestNode = null;
        int nextId = 1;

        // "bin" variables imply 0 <= x <= 1; the input format carries no explicit upper
        // bound for them, so add it before the root relaxation is ever solved -- otherwise
        // the LP relaxation could return e.g. x = 3.4 for a binary variable and floor/ceil
        // branching would never converge to {0, 1}.
        LPModel augmentedRoot = rootModel;
        for (int j = 0; j < rootModel.NumVars; j++)
        {
            if (rootModel.SignRestrictions[j] == "bin")
            {
                var coeffs = new double[rootModel.NumVars];
                coeffs[j] = 1.0;
                augmentedRoot = augmentedRoot.WithExtraConstraint(coeffs, "<=", 1.0);
            }
        }

        var root = new BranchNode
        {
            Id = nextId++,
            ParentId = 0,
            SubProblem = augmentedRoot,
            Bound = isMax ? double.PositiveInfinity : double.NegativeInfinity, // provisional
        };
        result.BranchDescriptions[root.Id] = "Root (LP relaxation)";
        openNodes.Add(root);

        while (openNodes.Count > 0)
        {
            // Best-bound-first: every open node's Bound is a valid upper bound (for max) on
            // what that branch can achieve -- either its own solved LP relaxation value, or
            // (for a not-yet-solved child) its parent's solved value, since adding a
            // constraint can only tighten the feasible region and reduce or maintain the
            // achievable objective.
            var current = isMax
                ? openNodes.OrderByDescending(n => n.Bound).First()
                : openNodes.OrderBy(n => n.Bound).First();
            openNodes.Remove(current);
            result.AllNodes.Add(current);

            var simplexResult = PrimalSimplex.Solve(current.SubProblem);

            for (int i = 0; i < simplexResult.Iterations.Count; i++)
            {
                var record = i < simplexResult.IterationRecords.Count
                    ? simplexResult.IterationRecords[i]
                    : new IterationRecord();
                result.AllIterations.Add((current.Id, simplexResult.Iterations[i], record));
            }

            if (simplexResult.Status == SimplexStatus.Infeasible)
            {
                current.Fathomed = true;
                current.FathomReason = "LP relaxation infeasible.";
                continue; // backtrack to the next open node
            }

            if (simplexResult.Status == SimplexStatus.Unbounded)
            {
                current.Fathomed = true;
                current.FathomReason = "LP relaxation unbounded -- this branch has no finite optimum.";
                continue;
            }

            current.OptimalTableau = simplexResult.FinalTableau;
            current.Bound = simplexResult.ObjectiveValue;
            nodeSolutions[current.Id] = simplexResult.Solution;

            // Bound fathom -- can this node possibly beat the current best?
            bool cannotImprove = bestNode != null && (isMax
                ? current.Bound <= bestObjective + IntegerTolerance
                : current.Bound >= bestObjective - IntegerTolerance);

            if (cannotImprove)
            {
                current.Fathomed = true;
                current.FathomReason = $"Bound {Rounding.R(current.Bound)} cannot improve on current best {Rounding.R(bestObjective)}.";
                continue;
            }

            int fractionalVarIndex = FindFractionalIntegerVariable(current.SubProblem, simplexResult.Solution);

            if (fractionalVarIndex == -1)
            {
                // Integrality fathom -- this node's relaxation is already integer.
                current.Fathomed = true;
                current.FathomReason = "Integer-feasible solution found.";

                bool better = bestNode == null || (isMax ? current.Bound > bestObjective : current.Bound < bestObjective);
                if (better)
                {
                    bestObjective = current.Bound;
                    bestNode = current;
                }
                continue;
            }

          
            double fractionalValue = simplexResult.Solution[fractionalVarIndex];
            double floorBound = Math.Floor(fractionalValue);
            double ceilBound = Math.Ceiling(fractionalValue);

            var branchCoeffs = new double[current.SubProblem.NumVars];
            branchCoeffs[fractionalVarIndex] = 1.0;

            var floorModel = current.SubProblem.WithExtraConstraint(branchCoeffs, "<=", floorBound);
            var ceilModel = current.SubProblem.WithExtraConstraint(branchCoeffs, ">=", ceilBound);

            string varName = fractionalVarIndex < rootModel.VariableNames.Length
                ? rootModel.VariableNames[fractionalVarIndex]
                : $"x{fractionalVarIndex + 1}";

            var floorNode = new BranchNode
            {
                Id = nextId++,
                ParentId = current.Id,
                SubProblem = floorModel,
                Bound = current.Bound, // provisional upper bound, tightened once solved
            };
            var ceilNode = new BranchNode
            {
                Id = nextId++,
                ParentId = current.Id,
                SubProblem = ceilModel,
                Bound = current.Bound,
            };

            result.BranchDescriptions[floorNode.Id] =
                $"{varName} <= {floorBound} (from Node {current.Id}, {varName} = {Rounding.R(fractionalValue)})";
            result.BranchDescriptions[ceilNode.Id] =
                $"{varName} >= {ceilBound} (from Node {current.Id}, {varName} = {Rounding.R(fractionalValue)})";

            openNodes.Add(floorNode);
            openNodes.Add(ceilNode);
            // Backtracking is implicit: once every descendant of `current` is eventually
            // fathomed, the OrderByDescending/OrderBy selection above moves on to whichever
            // open sibling/uncle node is next-best.
        }

        if (bestNode == null)
        {
            result.Status = SimplexStatus.Infeasible;
            result.Message = "No integer-feasible solution found.";
            return result;
        }

        result.Status = SimplexStatus.Optimal;
        result.BestNode = bestNode;
        result.Solution = nodeSolutions[bestNode.Id];
        result.ObjectiveValue = bestNode.Bound;
        return result;
    }

    private static int FindFractionalIntegerVariable(LPModel model, double[] solution)
    {
        for (int j = 0; j < model.NumVars; j++)
        {
            string restriction = model.SignRestrictions[j];
            bool mustBeInteger = restriction == "int" || restriction == "bin";
            if (mustBeInteger && Math.Abs(solution[j] - Math.Round(solution[j])) > IntegerTolerance)
                return j;
        }
        return -1;
    }
}
