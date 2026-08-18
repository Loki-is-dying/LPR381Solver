using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solve.Output;

internal class KnapsackResult_
{
}
public class KnapsackResult
{
    public string Status { get; set; } = "";
    public double BestProfit { get; set; }
    public double BestWeight { get; set; }
    public double Capacity { get; set; }
    public int NodesCreated { get; set; }
    public int NodesPruned { get; set; }
    public int[] BestSolution { get; set; } = Array.Empty<int>();
    public List<string> IterationLogs { get; set; } = new();

    public override string ToString()
    {
        var lines = new List<string>
        {
            "Branch and Bound Knapsack Result",
            "================================",
            $"Status: {Status}",
            $"Best profit: {BestProfit:0.###}",
            $"Best weight: {BestWeight:0.###}",
            $"Capacity: {Capacity:0.###}",
            $"Nodes created: {NodesCreated}",
            $"Nodes pruned: {NodesPruned}",
            "",
            "Best solution:"
        };

        for (int i = 0; i < BestSolution.Length; i++)
        {
            lines.Add($"x{i + 1} = {BestSolution[i]}");
        }

        lines.Add("");
        lines.Add("Iteration log:");

        foreach (string log in IterationLogs)
        {
            lines.Add(log);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
