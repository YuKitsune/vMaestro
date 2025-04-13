using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public interface IFixLookup
{
    Fix? FindFix(string identifier);
}

public class Fix(string identifier, Coordinate coordinate)
{
    public string Identifier { get; } = identifier;
    public Coordinate Coordinate { get; } = coordinate;
}