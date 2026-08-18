using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solve.Output;

internal class CuttingPlanrResult_
{
}
public class CuttingPlanResult 
{
    public string Status { get; set; } = "";
    public int Iterations { get; set; }
    public double ObjectiveValue { get; set; }
    public double[] Solution { get; set; } = Array.Empty<double>();
    public List<string> Cuts { get; set; } = new();

    public override string ToString()
    {
        var lines = new List<string>
        {
            "Cutting Plane Reslut",
            "=========================",
            $"Status: {Status}",
            $"Iterations {Iterations}",
            $"Objective value : {ObjectiveValue:0.###}",
            "",
            "Solution:"
        };
        for (int i = 0; i < Solution.Length; i++)
        {
            lines.Add($"x{i + 1} = {Solution[i]}");
        }
        lines.Add("");
        lines.Add("Cuts added");

        if (Cuts.Count == 0) // tells us how many cuts there are 
        {
            lines.Add("no cuts required");

        }
        else
        {
            for (int i = 0; i < Cuts.Count; i++)
            {
                lines.Add($"{i + 1}. {Cuts[i]}");

            }
            lines.Add("");
            lines.Add("Iteration log :");

            foreach( string log in IterationLogs)
            {
                lines.Add(log);
            }
        }
        return string.Join(Environment.NewLine, lines);
    

    }

}



