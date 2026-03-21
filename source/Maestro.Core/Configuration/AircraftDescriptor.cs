using System.Diagnostics;
using Maestro.Contracts.Shared;

namespace Maestro.Core.Configuration;

public interface IAircraftDescriptor;

[DebuggerDisplay("All")]
public record AllAircraftTypesDescriptor : IAircraftDescriptor;

[DebuggerDisplay("{TypeCode}")]
public record SpecificAircraftTypeDescriptor(string TypeCode) : IAircraftDescriptor;

[DebuggerDisplay("{AircraftCategory}")]
public record AircraftCategoryDescriptor(AircraftCategory AircraftCategory) : IAircraftDescriptor;

[DebuggerDisplay("{WakeCategory}")]
public record WakeCategoryDescriptor(WakeCategory WakeCategory) : IAircraftDescriptor;
