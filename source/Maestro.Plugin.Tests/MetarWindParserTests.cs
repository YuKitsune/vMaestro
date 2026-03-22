using Maestro.Contracts.Sessions;
using Shouldly;

namespace Maestro.Plugin.Tests;

public class MetarWindParserTests
{
    [Theory]
    [MemberData(nameof(ValidMetarTestCases))]
    public void Parse_ValidMetar_ReturnsExpectedWind(string metarString, WindDto? expectedWind)
    {
        // Act
        var result = MetarWindParser.Parse(metarString);

        // Assert
        if (expectedWind == null)
        {
            result.ShouldBeNull();
        }
        else
        {
            result.ShouldNotBeNull();
            result.Direction.ShouldBe(expectedWind.Direction);
            result.Speed.ShouldBe(expectedWind.Speed);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidMetarTestCases))]
    public void Parse_InvalidMetar_ReturnsNull(string metarString)
    {
        // Act
        var result = MetarWindParser.Parse(metarString);

        // Assert
        result.ShouldBeNull();
    }

    public static IEnumerable<object?[]> ValidMetarTestCases()
    {
        // Full METAR strings
        yield return ["YSSY 121200Z 34016KT 9999 FEW030 BKN250 22/12 Q1013", new WindDto(340, 16)];
        yield return ["METAR YSSY 121200Z AUTO 09008KT CAVOK 25/15 Q1015", new WindDto(90, 8)];
        yield return ["KJFK 121251Z 27020G35KT 10SM FEW250 18/M02 A3012", new WindDto(270, 20)];
        yield return ["EGLL 121220Z VRB05KT 9999 SCT025 15/12 Q1020", new WindDto(0, 5)];
        yield return ["SPECI LFPG 121200Z 340V25016KT 9999 BKN020 17/14 Q1018", new WindDto(295, 16)];

        // MPS format (meters per second, converted to knots: 1 m/s = 1.94384 kt)
        yield return ["34008MPS", new WindDto(340, 16)]; // 8 m/s = ~16 kt
        yield return ["09005MPS", new WindDto(90, 10)]; // 5 m/s = ~10 kt
        yield return ["UUEE 121200Z 34008MPS 9999 SCT020 12/08 Q1015", new WindDto(340, 16)]; // 8 m/s = ~16 kt
        yield return ["27010MPS", new WindDto(270, 19)]; // 10 m/s = ~19 kt
    }

    public static IEnumerable<object?[]> InvalidMetarTestCases()
    {
        // Null and empty
        yield return [null];
        yield return [""];
        yield return ["   "];

        // No wind component
        yield return ["YSSY 121200Z CAVOK 25/15 Q1015"];
        yield return ["METAR WITHOUT WIND"];

        // Malformed wind strings (too short)
        yield return ["34KT"];
        yield return ["12KT"];

        // Missing KT/MPS suffix
        yield return ["34016"];
        yield return ["340160000"];

        // Non-numeric direction
        yield return ["ABC16KT"];
        yield return ["ABCDEFKT"];

        // Invalid direction (out of range 0-360)
        yield return ["99916KT"];
        yield return ["36116KT"];
        yield return ["50010KT"];

        // Non-numeric speed
        yield return ["340ABKT"];
        yield return ["340XYZKT"];

        // Missing speed component
        yield return ["340KT"];

        // Malformed variable direction (invalid format)
        yield return ["340V16KT"];
        yield return ["340VAB16KT"];
        yield return ["AB0V25016KT"];
        yield return ["340V2ABKT"];

        // Variable direction with out-of-range values
        yield return ["999V25016KT"];
        yield return ["340V99916KT"];
        yield return ["361V36016KT"];

        // Malformed VRB (missing speed)
        yield return ["VRBKT"];
        yield return ["VRBABKT"];

        // Missing wind
        yield return ["/////KT"];
        yield return ["/////MPS"];
        yield return ["/////"];
    }
}
