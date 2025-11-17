namespace Maestro.Core.Tests.Handlers;

public class DesequenceRequestHandlerTests
{
    [Fact]
    public async Task TheFlightIsRemovedFromTheSequenceAndAddedToTheDesequencedList()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight and add it to the sequence

        // Act
        // TODO: Desequence the flight

        // Assert
        // TODO: Assert that the flight is removed from the sequence
        // TODO: Assert that the flight has been added to the DesequenecdFlights list
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights and add them to the sequence

        // Act
        // TODO: Desequence the second flight

        // Assert
        // TODO: Assert that the first flight remains unchanged
        // TODO: Assert that the third flight's landing time has been recalculated
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance
        // TODO: Create a flight

        // Act
        // TODO: Desequence the flight

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
