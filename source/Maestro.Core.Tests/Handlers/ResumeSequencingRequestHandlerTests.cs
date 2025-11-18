namespace Maestro.Core.Tests.Handlers;

// TODO: Test cases:
// - When a flight is resumed, it is inserted based on its ETA
// - When a flight is resumed, it becomes stable
// - When multiple flights are resumed, they are inserted in the same order they were before they were desequenced

public class ResumeSequencingRequestHandlerTests
{
    [Fact]
    public async Task FlightIsInsertedIntoTheSequenceAndRemovedFromTheDeSequencedList()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight and add it to the desequenced list

        // Act
        // TODO: Resume sequencing for the flight

        // Assert
        // TODO: Assert that the flight is added to the sequence
        // TODO: Assert that the flight has been removed from the DesequencedFlights list
    }

    [Fact]
    public async Task FlightIsInsertedByLandingEstimate()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create two flights in the sequence, one after the other
        // TODO: Create a third flight with an ETA between the first two flights, add it to the desequenced list

        // Act
        // TODO: Resume sequencing for the third flight

        // Assert
        // TODO: Assert that the landing order is first, third, second
        // TODO: Assert the third flight is delayed behind the first flight based on separation rules
        // TODO: Assert the second flight is delayed behind the third flight based on separation rules
    }

    [Fact]
    public async Task WhenEstimateIsBetweenTwoFrozenFlightsWithInsufficientSpace_ItIsMovedBackUntilThereIsSpace()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create two frozen flights in the sequence with insufficient space between them for the required separation
        // TODO: Create a third flight with an ETA between the two frozen flights, add it to the desequenced list

        // Act
        // TODO: Resume sequencing for the third flight

        // Assert
        // TODO: Assert that the third flight is placed after the second frozen flight
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Resume sequencing for the third flight

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
