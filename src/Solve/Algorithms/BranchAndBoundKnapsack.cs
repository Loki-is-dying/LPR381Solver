using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Solve.Models;
using Solve.Output;

namespace Solve.Algorithms;

internal class BranchAndBoundKnapsack_
{
}
public static class BranchAndBoundKnapsack
{
    private const double EPSILON = 1e-6;

    public static KnapsackResult Solve(LPModel model)
    {
        ValidateModel(model);

        int n = model.NumVars;

        List<KnapsackItem> items = BuildItems(model);

        items = items
            .OrderByDescending(item => item.Ratio)
            .ToList();

        double capacity = model.RHS[0];

        var result = new KnapsackResult
        {
            Status = "Solving",
            Capacity = capacity,
            BestProfit = 0,
            BestWeight = 0,
            BestSolution = new int[n]
        };

        List<KnapsackNode> liveNodes = new();

        var root = new KnapsackNode
        {
            Level = 0,
            Profit = 0,
            Weight = 0,
            Bound = 0,
            Selected = new int[n]
        };

        root.Bound = CalculateBound(root, items, capacity);

        liveNodes.Add(root);
        result.NodesCreated++;

        result.IterationLogs.Add($"Root node created with bound {root.Bound:0.###}");

        while (liveNodes.Count > 0)
        {
            KnapsackNode current = SelectNodeWithHighestBound(liveNodes);
            liveNodes.Remove(current);

            result.IterationLogs.Add(
                $"Processing node: Level={current.Level}, Profit={current.Profit:0.###}, Weight={current.Weight:0.###}, Bound={current.Bound:0.###}");

            if (current.Bound <= result.BestProfit + EPSILON)
            {
                result.NodesPruned++;
                result.IterationLogs.Add("Node pruned by bound.");
                continue;
            }

            if (current.Level >= n)
            {
                continue;
            }

            KnapsackItem nextItem = items[current.Level];

            /*
            * Branch 1:
            * Include the next item.
            */
            KnapsackNode includeNode = CreateChildNode(current);
            includeNode.Level = current.Level + 1;
            includeNode.Weight = current.Weight + nextItem.Weight;
            includeNode.Profit = current.Profit + nextItem.Profit;
            includeNode.Selected[nextItem.OriginalIndex] = 1;

            result.NodesCreated++;

            if (includeNode.Weight <= capacity + EPSILON)
            {
                if (includeNode.Profit > result.BestProfit + EPSILON)
                {
                    result.BestProfit = includeNode.Profit;
                    result.BestWeight = includeNode.Weight;
                    result.BestSolution = (int[])includeNode.Selected.Clone();

                    result.IterationLogs.Add(
                        $"New best solution found: Profit={result.BestProfit:0.###}, Weight={result.BestWeight:0.###}");
                }

                includeNode.Bound = CalculateBound(includeNode, items, capacity);

                if (includeNode.Bound > result.BestProfit + EPSILON)
                {
                    liveNodes.Add(includeNode);
                    result.IterationLogs.Add(
                        $"Include branch added: x{nextItem.OriginalIndex + 1}=1, Bound={includeNode.Bound:0.###}");
                }
                else
                {
                    result.NodesPruned++;
                    result.IterationLogs.Add(
                        $"Include branch pruned by bound: Bound={includeNode.Bound:0.###}");
                }
            }
            else
            {
                result.NodesPruned++;
                result.IterationLogs.Add(
                    $"Include branch pruned by infeasibility: Weight={includeNode.Weight:0.###}");
            }

            /*
            * Branch 2:
            * Exclude the next item.
            */
            KnapsackNode excludeNode = CreateChildNode(current);
            excludeNode.Level = current.Level + 1;
            excludeNode.Selected[nextItem.OriginalIndex] = 0;
            excludeNode.Bound = CalculateBound(excludeNode, items, capacity);

            result.NodesCreated++;

            if (excludeNode.Bound > result.BestProfit + EPSILON)
            {
                liveNodes.Add(excludeNode);
                result.IterationLogs.Add(
                    $"Exclude branch added: x{nextItem.OriginalIndex + 1}=0, Bound={excludeNode.Bound:0.###}");
            }
            else
            {
                result.NodesPruned++;
                result.IterationLogs.Add(
                    $"Exclude branch pruned by bound: Bound={excludeNode.Bound:0.###}");
            }
        }

        result.Status = "Optimal knapsack solution found";
        return result;
    }

    private static void ValidateModel(LPModel model)
    {
        if (model is null)
        {
            throw new ArgumentException("Model cannot be null.");
        }

        if (!model.IsMaximisation)
        {
            throw new InvalidOperationException(
                "Branch and Bound Knapsack currently supports maximisation problems only.");
        }

        if (model.NumConstraints != 1)
        {
            throw new InvalidOperationException(
                "Branch and Bound Knapsack requires exactly one constraint.");
        }

        if (model.ConstraintRelations[0].Trim() != "<=")
        {
            throw new InvalidOperationException(
                "Branch and Bound Knapsack requires the constraint relation to be '<='.");
        }

        if (model.RHS[0] < 0)
        {
            throw new InvalidOperationException(
                "Knapsack capacity cannot be negative.");
        }

        if (model.ObjectiveCoefficients.Length != model.NumVars)
        {
            throw new ArgumentException(
                "Objective coefficient count does not match number of variables.");
        }

        if (model.SignRestrictions.Length != model.NumVars)
        {
            throw new ArgumentException(
                "Sign restriction count does not match number of variables.");
        }

        for (int j = 0; j < model.NumVars; j++)
        {
            string sign = model.SignRestrictions[j].Trim().ToLower();

            if (sign != "bin")
            {
                throw new InvalidOperationException(
                    $"Knapsack requires all variables to be binary. Variable x{j + 1} is '{model.SignRestrictions[j]}'.");
            }

            double profit = model.ObjectiveCoefficients[j];
            double weight = model.ConstraintMatrix[0, j];

            if (profit < 0)
            {
                throw new InvalidOperationException(
                    $"Knapsack item x{j + 1} has negative profit. This implementation expects non-negative profits.");
            }

            if (weight <= 0)
            {
                throw new InvalidOperationException(
                    $"Knapsack item x{j + 1} must have a positive weight.");
            }
        }
    }

    private static List<KnapsackItem> BuildItems(LPModel model)
    {
        List<KnapsackItem> items = new();

        for (int j = 0; j < model.NumVars; j++)
        {
            double profit = model.ObjectiveCoefficients[j];
            double weight = model.ConstraintMatrix[0, j];

            items.Add(new KnapsackItem
            {
                OriginalIndex = j,
                Name = GetVariableName(model, j),
                Profit = profit,
                Weight = weight,
                Ratio = profit / weight
            });
        }

        return items;
    }

    private static string GetVariableName(LPModel model, int index)
    {
        if (model.VariableNames.Length > index && !string.IsNullOrWhiteSpace(model.VariableNames[index]))
        {
            return model.VariableNames[index];
        }

        return $"x{index + 1}";
    }

    private static KnapsackNode SelectNodeWithHighestBound(List<KnapsackNode> liveNodes)
    {
        int bestIndex = 0;
        double bestBound = liveNodes[0].Bound;

        for (int i = 1; i < liveNodes.Count; i++)
        {
            if (liveNodes[i].Bound > bestBound)
            {
                bestBound = liveNodes[i].Bound;
                bestIndex = i;
            }
        }

        return liveNodes[bestIndex];
    }

    private static KnapsackNode CreateChildNode(KnapsackNode parent)
    {
        return new KnapsackNode
        {
            Level = parent.Level,
            Profit = parent.Profit,
            Weight = parent.Weight,
            Bound = parent.Bound,
            Selected = (int[])parent.Selected.Clone()
        };
    }

    private static double CalculateBound(
        KnapsackNode node,
        List<KnapsackItem> items,
        double capacity)
    {
        if (node.Weight >= capacity + EPSILON)
        {
            return 0;
        }

        double bound = node.Profit;
        double totalWeight = node.Weight;

        int level = node.Level;

        /*
        * Add full items while they fit.
        */
        while (level < items.Count &&
            totalWeight + items[level].Weight <= capacity + EPSILON)
        {
            totalWeight += items[level].Weight;
            bound += items[level].Profit;
            level++;
        }

        /*
        * Add fractional part of the next item for the upper bound only.
        */
        if (level < items.Count)
        {
            double remainingCapacity = capacity - totalWeight;
            bound += items[level].Profit * (remainingCapacity / items[level].Weight);
        }

        return bound;
    }

    private class KnapsackItem
    {
        public int OriginalIndex { get; set; }
        public string Name { get; set; } = "";
        public double Profit { get; set; }
        public double Weight { get; set; }
        public double Ratio { get; set; }
    }

    private class KnapsackNode
    {
        public int Level { get; set; }
        public double Profit { get; set; }
        public double Weight { get; set; }
        public double Bound { get; set; }
        public int[] Selected { get; set; } = Array.Empty<int>();
    }
}

