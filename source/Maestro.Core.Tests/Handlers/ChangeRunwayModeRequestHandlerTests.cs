namespace Maestro.Core.Tests.Handlers;

public class ChangeRunwayModeRequestHandlerTests
{
    [Fact]
    public async Task WhenStartTimeIsNowOrEarlier_CurrentRunwayModeIsChanged()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Fact]
    public async Task WhenStartTimeIsInTheFuture_NextRunwayModeAndStartTimesAreSet()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_ReAssignedToNewRunways()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create four flights

        // Act
        // TODO: Change the runway mode so that the second two flights need to be reassigned

        // Assert
        // TODO: Assert that the first two flights remain on their original runways
        // TODO: Assert that the second two flights have been reassigned to the correct runways based on the new mode
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_AreRescheduled()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create four flights

        // Act
        // TODO: Change the runway mode so that the second two flights need to be reassigned. Use a different acceptance rate too.

        // Assert
        // TODO: Assert that the order and landing times of the first two flights remain unchanged
        // TODO: Assert that the order and landing times of the second two flights have been rescheduled such that:
        // - They are delayed until after the FirstLandingTimeInNewMode time, and;
        // - They maintain proper separation based on the new acceptance rate
    }

    [Fact]
    public async Task FrozenFlights_AreNotAffected()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create four flights, where the first two are frozen

        // Act
        // TODO: Change the runway mode so that all flights would need to be reassigned

        // Assert
        // TODO: Assert that the frozen flights remain on their original runways with unchanged landing times
        // TODO: Assert that the non-frozen flights have been reassigned and rescheduled according to the new runway mode
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Change the runway mode

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
