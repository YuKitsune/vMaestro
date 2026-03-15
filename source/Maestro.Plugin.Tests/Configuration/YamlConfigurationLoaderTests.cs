using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Plugin.Configuration;
using Shouldly;

namespace Maestro.Plugin.Tests.Configuration;

public class YamlConfigurationLoaderTests
{
    [Fact]
    public void LoadFromYaml_ShouldDeserializeServerConfiguration()
    {
        var yaml = """
            Logging:
              LogLevel: Information
              MaxFileAgeDays: 7

            Server:
              Uri: "http://localhost:5000"
              Partitions: ["VATSIM", "SweatBox-1"]
              TimeoutSeconds: 30

            Airports: []

            Labels:
              Colours:
                States: {}
                ControlActions: {}
                DeferredRunwayMode: "255,255,255"
              Layouts: []
            """;

        var config = YamlConfigurationLoader.LoadFromYaml(yaml);

        Assert.Fail("TODO");
        // config.Server.Uri.ShouldBe("http://localhost:5000");
        // config.Server.Partitions.ShouldBe(new[] { "VATSIM", "SweatBox-1" });
        // config.Server.TimeoutSeconds.ShouldBe(30);
        // config.Logging.LogLevel.ShouldBe("Information");
        // config.Logging.MaxFileAgeDays.ShouldBe(7);
    }

    [Fact]
    public void LoadFromYaml_ShouldDeserializeAirportWithTrajectories()
    {
        var yaml = """
            Logging:
              LogLevel: Information
              MaxFileAgeDays: 7

            Server:
              Uri: "http://localhost:5000"
              Partitions: ["VATSIM"]
              TimeoutSeconds: 30

            Airports:
              - Identifier: "YSSY"
                FeederFixes: ["RIVET", "WELSH"]
                Runways: ["34L", "34R"]
                RunwayModes:
                  - Identifier: "34IVA"
                    DependencyRateSeconds: 0
                    OffModeSeparationSeconds: 300
                    Runways:
                      - {Identifier: "34L", LandingRateSeconds: 180, FeederFixes: ["RIVET"]}
                      - {Identifier: "34R", LandingRateSeconds: 180, FeederFixes: ["WELSH"]}
                Trajectories:
                  - {FeederFix: "RIVET", Aircraft: ["JET"], RunwayIdentifier: "34L", TrackMiles: 102.5, TimeToGoMinutes: 17, PressureMinutes: 0, MaxPressureMinutes: 0, ApproachType: "", ApproachFix: ""}
                  - {FeederFix: "WELSH", Aircraft: ["NONJET", "DH8D"], RunwayIdentifier: "34R", TrackMiles: 95.3, TimeToGoMinutes: 15, PressureMinutes: 0, MaxPressureMinutes: 0, ApproachType: "", ApproachFix: ""}
                DepartureAirports:
                  - {Identifier: "YPMQ", Aircraft: ["JET"], Distance: 209.0, EstimatedFlightTimeMinutes: 44}
                Views:
                  - Identifier: "Enroute"
                    TimeWindowMinutes: 60
                    Direction: Down
                    Reference: FeederFixTime
                    Ladders:
                      - {FeederFixes: ["RIVET", "WELSH"], Runways: []}
                    LabelLayout: "Default"
                GlobalCoordinationMessages: []
                FlightCoordinationMessages: []

            Labels:
              Colours:
                States:
                  Unstable: "255,205,105"
                  Stable: "0,0,96"
                ControlActions: {}
                DeferredRunwayMode: "255,255,255"
              Layouts:
                - Identifier: "Default"
                  Items:
                    - {Type: "Callsign", Width: 7, Padding: 1, ColourSources: ["State"]}
            """;

        var config = YamlConfigurationLoader.LoadFromYaml(yaml);

        config.Airports.Length.ShouldBe(1);
        var airport = config.Airports[0];
        airport.Identifier.ShouldBe("YSSY");
        airport.FeederFixes.ShouldBe(new[] { "RIVET", "WELSH" });
        airport.Runways.ShouldBe(new[] { "34L", "34R" });

        airport.Trajectories.Length.ShouldBe(2);
        var trajectory1 = airport.Trajectories[0];
        trajectory1.FeederFix.ShouldBe("RIVET");
        trajectory1.RunwayIdentifier.ShouldBe("34L");
        trajectory1.TrackMiles.ShouldBe(102.5);
        trajectory1.TimeToGoMinutes.ShouldBe(17);
        trajectory1.Aircraft.Length.ShouldBe(1);
        trajectory1.Aircraft[0].ShouldBeOfType<AircraftCategoryDescriptor>();

        var trajectory2 = airport.Trajectories[1];
        trajectory2.Aircraft.Length.ShouldBe(2);
        trajectory2.Aircraft[0].ShouldBeOfType<AircraftCategoryDescriptor>();
        trajectory2.Aircraft[1].ShouldBeOfType<SpecificAircraftTypeDescriptor>();
    }

    [Theory]
    [InlineData("ALL", typeof(AllAircraftTypesDescriptor))]
    [InlineData("JET", typeof(AircraftCategoryDescriptor))]
    [InlineData("NONJET", typeof(AircraftCategoryDescriptor))]
    [InlineData("LIGHT", typeof(WakeCategoryDescriptor))]
    [InlineData("MEDIUM", typeof(WakeCategoryDescriptor))]
    [InlineData("HEAVY", typeof(WakeCategoryDescriptor))]
    [InlineData("SUPER", typeof(WakeCategoryDescriptor))]
    [InlineData("B738", typeof(SpecificAircraftTypeDescriptor))]
    public void AircraftDescriptorTypeConverter_ShouldParseVariousFormats(string input, Type expectedType)
    {
        var yaml = @"Logging:
  LogLevel: Information
  MaxFileAgeDays: 7

Server:
  Uri: ""http://localhost:5000""
  Partitions: [""VATSIM""]
  TimeoutSeconds: 30

Airports:
  - Identifier: ""TEST""
    FeederFixes: [""FIX""]
    Runways: [""34""]
    RunwayModes:
      - Identifier: ""34""
        Runways:
          - {Identifier: ""34"", LandingRateSeconds: 180}
    Trajectories:
      - {FeederFix: ""FIX"", Aircraft: [""" + input + @"""], RunwayIdentifier: ""34"", TrackMiles: 100, TimeToGoMinutes: 15, PressureMinutes: 0, MaxPressureMinutes: 0}
    DepartureAirports: []
    Views: []
    GlobalCoordinationMessages: []
    FlightCoordinationMessages: []

Labels:
  Colours:
    States: {}
    ControlActions: {}
    DeferredRunwayMode: ""255,255,255""
  Layouts: []
";

        var config = YamlConfigurationLoader.LoadFromYaml(yaml);
        var trajectory = config.Airports[0].Trajectories[0];
        trajectory.Aircraft[0].ShouldBeOfType(expectedType);
    }

    [Fact]
    public void LoadFromYaml_ShouldDeserializePolymorphicLabelItems()
    {
        var yaml = """
            Logging:
              LogLevel: Information
              MaxFileAgeDays: 7

            Server:
              Uri: "http://localhost:5000"
              Partitions: ["VATSIM"]
              TimeoutSeconds: 30

            Airports: []

            Labels:
              Colours:
                States: {}
                ControlActions: {}
                DeferredRunwayMode: "255,255,255"
              Layouts:
                - Identifier: "TestLayout"
                  Items:
                    - {Type: "Callsign", Width: 10, Padding: 1, ColourSources: ["State"]}
                    - {Type: "Runway", Width: 3, Padding: 1, ColourSources: ["Runway", "State"]}
                    - {Type: "ManualDelay", Width: 1, Padding: 0, ZeroDelaySymbol: "#", ManualDelaySymbol: "%", ColourSources: ["State"]}
                    - {Type: "HighSpeed", Width: 1, Padding: 0, Symbol: "+", ColourSources: ["State"]}
            """;

        var config = YamlConfigurationLoader.LoadFromYaml(yaml);

        config.Labels.Layouts.Length.ShouldBe(1);
        var layout = config.Labels.Layouts[0];
        layout.Identifier.ShouldBe("TestLayout");
        layout.Items.Length.ShouldBe(4);

        layout.Items[0].ShouldBeOfType<CallsignItemConfiguration>();
        layout.Items[0].Width.ShouldBe(10);

        layout.Items[1].ShouldBeOfType<RunwayItemConfiguration>();
        layout.Items[1].Width.ShouldBe(3);

        var manualDelay = layout.Items[2].ShouldBeOfType<ManualDelayItemConfiguration>();
        manualDelay.ZeroDelaySymbol.ShouldBe("#");
        manualDelay.ManualDelaySymbol.ShouldBe("%");

        var highSpeed = layout.Items[3].ShouldBeOfType<HighSpeedItemConfiguration>();
        highSpeed.Symbol.ShouldBe("+");
    }
}
