using Shouldly;

namespace Maestro.Server.Tests;

public class VersionCompatibilityTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.0.99", true)]
    [InlineData("1.2.0", "1.2.5", true)]
    [InlineData("2.3.4", "2.3.0", true)]
    public void IsCompatible_ShouldReturnTrue_WhenMajorAndMinorMatch(
        string clientVersion,
        string serverVersion,
        bool expected)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("2.0.0", "1.0.0")]
    [InlineData("1.0.0", "3.0.0")]
    public void IsCompatible_ShouldReturnFalse_WhenMajorVersionsDiffer(
        string clientVersion,
        string serverVersion)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("1.0.0", "1.1.0")]
    [InlineData("1.1.0", "1.0.0")]
    [InlineData("1.0.0", "1.2.0")]
    [InlineData("2.3.0", "2.5.0")]
    public void IsCompatible_ShouldReturnFalse_WhenMinorVersionsDiffer(
        string clientVersion,
        string serverVersion)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("1.0.0+abc123", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0+def456", true)]
    [InlineData("1.0.0+abc123", "1.0.0+def456", true)]
    [InlineData("1.2.3+metadata", "1.2.5+other", true)]
    public void IsCompatible_ShouldIgnoreMetadata(
        string clientVersion,
        string serverVersion,
        bool expected)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.0.0 abc123", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0 def456", true)]
    [InlineData("1.0.0 abc123", "1.0.0 def456", true)]
    [InlineData("1.2.3 githash", "1.2.5 otherhash", true)]
    [InlineData("1.0.0-beta.1 abc123", "1.0.0-beta.1", true)]
    [InlineData("1.0.0-beta.1 abc123", "1.0.0-beta.1 def456", true)]
    public void IsCompatible_ShouldIgnoreSpaceSeparatedMetadata(
        string clientVersion,
        string serverVersion,
        bool expected)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.0.0-beta", "1.0.0-beta", true)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1", true)]
    [InlineData("2.3.4-rc.2", "2.3.4-rc.2", true)]
    public void IsCompatible_ShouldReturnTrue_WhenPreReleaseVersionsMatch(
        string clientVersion,
        string serverVersion,
        bool expected)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.0.0-beta", "1.0.0")]
    [InlineData("1.0.0", "1.0.0-alpha")]
    [InlineData("1.0.0-beta.1", "1.0.0-beta.2")]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-rc.1", "1.0.1-rc.1")]
    public void IsCompatible_ShouldReturnFalse_WhenPreReleaseVersionsDiffer(
        string clientVersion,
        string serverVersion)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0.0.0", "0.0.0", true)]
    [InlineData("0.0.0", "0.0.1", true)]
    [InlineData("0.1.0", "0.0.0", false)]
    public void IsCompatible_ShouldHandleZeroVersions(
        string clientVersion,
        string serverVersion,
        bool expected)
    {
        var result = VersionCompatibility.IsCompatible(clientVersion, serverVersion);
        result.ShouldBe(expected);
    }
}
