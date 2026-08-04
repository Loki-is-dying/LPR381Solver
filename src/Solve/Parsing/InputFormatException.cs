namespace Solve.Parsing;

/// <summary>Thrown when the input text file does not follow the required LP/IP model format.</summary>
public class InputFormatException : Exception
{
    public InputFormatException(string message) : base(message) { }
}
