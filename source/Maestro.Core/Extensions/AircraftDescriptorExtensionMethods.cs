using Maestro.Core.Configuration;
using Maestro.Core.Integration;

namespace Maestro.Core.Extensions;

public static class AircraftDescriptorExtensionMethods
{
    public static bool Matches(
        this IAircraftDescriptor aircraftDescriptor,
        AircraftPerformanceData aircraftPerformanceData)
    {
        return aircraftDescriptor switch
        {
            SpecificAircraftTypeDescriptor specificAircraftTypeDescriptor => aircraftPerformanceData.TypeCode == specificAircraftTypeDescriptor.TypeCode,
            AircraftCategoryDescriptor aircraftCategoryDescriptor => aircraftPerformanceData.AircraftCategory == aircraftCategoryDescriptor.AircraftCategory,
            WakeCategoryDescriptor wakeCategoryDescriptor => aircraftPerformanceData.WakeCategory == wakeCategoryDescriptor.WakeCategory,
            AllAircraftTypesDescriptor => true,
            _ => throw new ArgumentOutOfRangeException(nameof(aircraftDescriptor))
        };
    }

    public static bool Matches(
        this IEnumerable<IAircraftDescriptor> aircraftDescriptors,
        AircraftPerformanceData aircraftPerformanceData)
    {
        return aircraftDescriptors.Any(d => d.Matches(aircraftPerformanceData));
    }
}
