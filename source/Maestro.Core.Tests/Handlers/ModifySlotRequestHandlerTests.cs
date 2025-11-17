namespace Maestro.Core.Tests.Handlers;

public class ModifySlotRequestHandlerTests
{
    [Fact]
    public async Task TheSlotIsModified()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot

        // Act
        // TODO: Modify the start and end times of the slot

        // Assert
        // TODO: Assert that the slot's start and end times have been updated
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot
        // TODO: Create two flights, one estimated to land during the slot, one after the slot

        // Act
        // TODO: Modify the slot such that the first flight's landing estimate is now before the slot, and the second flight's landing estimate is now during the slot

        // Assert
        // TODO: Assert that the first flight's landing time is it's landing estimate
        // TODO: Assert that the second flight's landing time is after the slot
    }

    [Fact]
    public async Task FrozenFlightsAreUnaffected()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot
        // TODO: Create two frozen flights, one scheduled to land during the slot, one after the slot

        // Act
        // TODO: Modify the slot such that it covers the landing time of the second flight but not the first one

        // Assert
        // TODO: Assert that the landing times of both frozen flights are unchanged
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance
        // TODO: Create a slot

        // Act
        // TODO: Modify a slot

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
