using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class AircraftTypeReclassification
{
    public required string AircraftType { get; init; }
    public required AircraftType NewClassification { get; init; }
}