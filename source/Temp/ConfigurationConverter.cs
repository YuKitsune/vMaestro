using Maestro.Core.Configuration;

namespace Temp;

public static class ConfigurationConverter
{
    public static PluginConfigurationV2 ConvertPluginConfiguration(PluginConfigurationV1 v1)
    {
        return new PluginConfigurationV2
        {
            Server = v1.Server,
            Logging = v1.Logging,
            Airports = v1.Airports.Select(a => ConvertAirport(
                a,
                v1.CoordinationMessages.Templates.Where(s => !s.Contains("{Callsign}")).ToArray(),
                v1.CoordinationMessages.Templates.Where(s => s.Contains("{Callsign}")).ToArray())).ToArray(),
            Colours = ColourDefaults.Default(),
            Labels = CreateDefaultLabels()
        };
    }

    public static AirportConfigurationV2 ConvertAirport(
        AirportConfiguration v1,
        string[] globalCoordinationMessages,
        string[] flightCoordinationMessages)
    {
        return new AirportConfigurationV2
        {
            Identifier = v1.Identifier,
            FeederFixes = v1.FeederFixes,
            Runways = v1.Runways,
            DefaultAircraftType = v1.DefaultInsertedFlightAircraftType,
            DefaultPendingFlightState = v1.ManuallyInsertedFlightState,
            DefaultDepartureFlightState = v1.InitialDepartureFlightState,
            DefaultDummyFlightState = v1.DummyFlightState,
            DefaultOffModeSeparationSeconds = v1.DefaultOffModeSeparationSeconds,
            MinimumUnstableMinutes = 5,
            StabilityThresholdMinutes = 25,
            FrozenThresholdMinutes = 15,
            MaxLandedFlights = v1.MaxLandedFlights,
            LandedFlightTimeoutMinutes = v1.LandedFlightTimeoutMinutes,
            LostFlightTimeoutMinutes = 10,
            RunwayModes = v1.RunwayModes.Select(ConvertRunwayMode).ToArray(),
            Trajectories = ConvertArrivalsToTrajectories(v1.Arrivals),
            DepartureAirports = ConvertDepartureAirports(v1.DepartureAirports),
            Colours = null,
            Views = v1.Views.Select(ConvertView).ToArray(),
            GlobalCoordinationMessages = globalCoordinationMessages,
            FlightCoordinationMessages = flightCoordinationMessages
        };
    }

    public static RunwayModeConfigurationV2 ConvertRunwayMode(RunwayModeConfiguration v1)
    {
        return new RunwayModeConfigurationV2
        {
            Identifier = v1.Identifier,
            DependencyRateSeconds = v1.DependencyRateSeconds,
            OffModeSeparationSeconds = v1.OffModeSeparationSeconds,
            Runways = v1.Runways.Select(ConvertRunway).ToArray()
        };
    }

    public static RunwayConfigurationV2 ConvertRunway(RunwayConfiguration v1)
    {
        return new RunwayConfigurationV2
        {
            Identifier = v1.Identifier,
            ApproachType = v1.ApproachType,
            LandingRateSeconds = v1.LandingRateSeconds,
            FeederFixes = v1.FeederFixes
        };
    }

    public static TrajectoryConfigurationV2[] ConvertArrivalsToTrajectories(ArrivalConfiguration[] arrivals)
    {
        var trajectories = new List<TrajectoryConfigurationV2>();

        foreach (var arrival in arrivals)
        {
            var aircraftDescriptors = ConvertArrivalToAircraftDescriptors(arrival);

            foreach (var kvp in arrival.RunwayIntervals)
            {
                var runway = kvp.Key;
                var intervalMinutes = kvp.Value;
                trajectories.Add(new TrajectoryConfigurationV2
                {
                    FeederFix = arrival.FeederFix,
                    Aircraft = aircraftDescriptors,
                    ApproachType = arrival.ApproachType,
                    ApproachFix = arrival.ApproachFix,
                    RunwayIdentifier = runway,
                    TrackMiles = 0, // TODO: No V1 equivalent - must be configured manually
                    TimeToGoMinutes = intervalMinutes,
                    PressureMinutes = 0,
                    MaxPressureMinutes = 0
                });
            }
        }

        return trajectories.ToArray();
    }

    private static IAircraftDescriptor[] ConvertArrivalToAircraftDescriptors(ArrivalConfiguration arrival)
    {
        var descriptors = new List<IAircraftDescriptor>();

        if (arrival.Category.HasValue)
        {
            descriptors.Add(new AircraftCategoryDescriptor(arrival.Category.Value));
        }

        if (!string.IsNullOrEmpty(arrival.AircraftType))
        {
            descriptors.Add(new SpecificAircraftTypeDescriptor(arrival.AircraftType));
        }

        if (arrival.AdditionalAircraftTypes.Length > 0)
        {
            foreach (var type in arrival.AdditionalAircraftTypes)
            {
                descriptors.Add(new SpecificAircraftTypeDescriptor(type));
            }
        }

        // If no descriptors were found, default to all aircraft
        if (descriptors.Count == 0)
        {
            descriptors.Add(new AllAircraftTypesDescriptor());
        }

        return descriptors.ToArray();
    }

    public static DepartureConfigurationV2[] ConvertDepartureAirports(
        DepartureAirportConfiguration[] departureAirports)
    {
        var departures = new List<DepartureConfigurationV2>();

        foreach (var airport in departureAirports)
        {
            foreach (var flightTime in airport.FlightTimes)
            {
                departures.Add(new DepartureConfigurationV2
                {
                    Identifier = airport.Identifier,
                    Aircraft = [flightTime.AircraftType],
                    Distance = airport.Distance,
                    EstimatedFlightTimeMinutes = (int)flightTime.AverageFlightTime.TotalMinutes
                });
            }
        }

        return departures.ToArray();
    }

    public static ViewConfigurationV2 ConvertView(ViewConfiguration v1)
    {
        var direction = v1.ViewMode == ViewMode.Enroute ? LadderDirection.Down : LadderDirection.Up;
        var reference = v1.ViewMode == ViewMode.Enroute
            ? LadderReference.FeederFixTime
            : LadderReference.LandingTime;
        var labelLayout = v1.ViewMode == ViewMode.Enroute ? "Enroute" : "TMA";

        string[][] v1Filters = [v1.LeftLadder, v1.RightLadder];

        var ladders = new List<LadderConfigurationV2>();

        foreach (var filters in v1Filters)
        {
            if (v1.ViewMode == ViewMode.Enroute)
            {
                ladders.Add(new LadderConfigurationV2
                {
                    FeederFixes = filters,
                    Runways = []
                });
            }
            else if (v1.ViewMode == ViewMode.Approach)
            {
                ladders.Add(new LadderConfigurationV2
                {
                    FeederFixes = [],
                    Runways = filters
                });
            }
        }

        return new ViewConfigurationV2
        {
            Identifier = v1.Identifier,
            TimeWindowMinutes = v1.TimeHorizonMinutes,
            Direction = direction,
            Reference = reference,
            Ladders = ladders.ToArray(),
            LabelLayout = labelLayout
        };
    }

    public static LabelsConfigurationV2 CreateDefaultLabels()
    {
        return new LabelsConfigurationV2
        {
            Colours = ColourDefaults.Default(),
            Layouts =
            [
                new LabelLayoutConfigurationV2
                {
                    Identifier = "Enroute",
                    Items = LabelItemDefaults.DefaultEnrouteLabelItem()
                },
                new LabelLayoutConfigurationV2
                {
                    Identifier = "TMA",
                    Items = LabelItemDefaults.DefaultTmaLabelItem()
                }
            ]
        };
    }
}
