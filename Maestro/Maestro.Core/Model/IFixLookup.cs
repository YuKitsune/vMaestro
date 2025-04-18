using System.Diagnostics;
using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public interface IFixLookup
{
    Fix? FindFix(string identifier);
}

[DebuggerDisplay("{Identifier}")]
public class Fix(string identifier, Coordinate coordinate)
{
    public string Identifier { get; } = identifier;
    public Coordinate Coordinate { get; } = coordinate;
}