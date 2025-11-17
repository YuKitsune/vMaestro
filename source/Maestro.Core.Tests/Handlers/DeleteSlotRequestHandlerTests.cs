namespace Maestro.Core.Tests.Handlers;

public class DeleteSlotRequestHandlerTests
{
    [Fact]
    public async Task TheSlotIsDeleted()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot

        // Act
        // TODO: Delete the slot

        // Assert
        // TODO: Assert that the slot no longer exists
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot
        // TODO: Create two flights estimated to land during the slot and after the slot

        // Act
        // TODO: Delete the slot

        // Assert
        // TODO: Assert that the flights' landing times have been recalculated appropriately
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a slot
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Delete the slot

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
