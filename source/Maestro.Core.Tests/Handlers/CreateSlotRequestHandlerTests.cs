namespace Maestro.Core.Tests.Handlers;

public class CreateSlotRequestHandlerTests
{
    [Fact]
    public async Task WhenCreatingSlot_FlightsLandingAfterTheStartTimeAreRescheduled()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights one after the other

        // Act
        // TODO: Create a slot starting after the first flight's landing time but before the second flight's

        // Assert
        // TODO: Assert that the first flight's landing time is unchanged
        // TODO: Assert that the second and third flights' landing times have been adjusted to maintain proper separation after the slot
    }

    [Fact]
    public async Task WhenCreatingSlot_FrozenFlightsAreUnaffected()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create two flights, one after the other, where the first one is frozen

        // Act
        // TODO: Create a slot starting before the first flights landing time, and ending after the second flight's landing time

        // Assert
        // TODO: Assert that the first flight's landing time is unchanged
        // TODO: Assert that the second flights' landing time is after the slot
    }

    [Fact]
    public async Task WhenCreatingSlot_SeparationIsMaintainedWithFrozenFlights()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create two flights, one after the other, where the first one is frozen

        // Act
        // TODO: Create a slot starting before the first flights landing time, and ending 1 minute after the first flight's landing time

        // Assert
        // TODO: Assert that the first flight's landing time is unchanged
        // TODO: Assert that the second flights' landing time is after the slot plus the required separation from the frozen flight within the slot
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Create a slot

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

}
