using Maestro.Core.Model;

namespace Maestro.Core.Tests.Handlers;

public class MakeStableRequestHandlerTests
{
    [Fact]
    public async Task TheFlightIsMarkedAsStable()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create an unstable flight

        // Act
        // TODO: Make the flight stable

        // Assert
        // TODO: Assert that the flight is marked as stable
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task StablisedFlightsAreUnaffected(State state)
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight with the provided state

        // Act
        // TODO: Make the flight stable

        // Assert
        // TODO: Assert that the state is unchanged
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Make the flight stable

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
