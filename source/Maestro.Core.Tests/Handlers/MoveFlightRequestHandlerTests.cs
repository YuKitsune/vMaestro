using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task FlightIsPositionedBasedOnTargetTime()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights, one after the other

        // Act
        // TODO: Move the last flight to a time between the first two flights

        // Assert
        // TODO: Assert the landing order is first, third, second
    }

    [Fact]
    public async Task RunwayIsChanged()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight assigned to a specific runway

        // Act
        // TODO: Move the flight to a new time and assign it to a different runway

        // Assert
        // TODO: Assert the flight's runway has been updated to the new runway
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task TheSequenceIsRecalculated(State state)
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights, one after the other, with the specified state, and no conflicts

        // Act
        // TODO: Move the last flight to a time between the first two flights

        // Assert
        // TODO: Assert the first flight remains unchanged
        // TODO: Assert the moved flight has no delay (STA = ETA)
        // TODO: Assert the third flight has been delayed to maintain separation from the moved flight
    }

    [Fact]
    public async Task WhenEstimateIsAheadOfFrozenFlights_FrozenFlightsAreNotMoved()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights, one after the other, where the first two are frozen, and the last one is stable with an estimate ahead of the first frozen flight

        // Act
        // TODO: Move the last flight to a time between the first two frozen flights

        // Assert
        // TODO: Assert the frozen flights remain unchanged
        // TODO: Assert the moved flight is between the two frozen flights
        // TODO: Assert the moved flight is delayed behind the first frozen flight
    }

    // TODO: What if ETA is behind frozen flight? Exception? Move backwards?

    [Fact]
    public async Task UnstableFlightsBecomeStable()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create an unstable flight

        // Act
        // TODO: Move the flight to a new time

        // Assert
        // TODO: Assert the flight is now stable
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task StableFlightsDoNotChangeState(State state)
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight with the specified state

        // Act
        // TODO: Move the flight to a new time

        // Assert
        // TODO: Assert the state is unchanged
    }

    [Fact]
    public async Task InsufficientSpaceBetweenFrozenFlights_ThrowsException()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights, where the first two are frozen and are have 5 minutes between them, and the third is stable

        // Act
        // TODO: Move the stable flight to a time between the two frozen flights

        // Assert
        // TODO: Assert that an exception is thrown indicating insufficient space
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Move a flight

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

    // TODO: Delete these


    MoveFlightRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence)
    {
        var mediator = Substitute.For<IMediator>();
        var clock = clockFixture.Instance;
        return new MoveFlightRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
