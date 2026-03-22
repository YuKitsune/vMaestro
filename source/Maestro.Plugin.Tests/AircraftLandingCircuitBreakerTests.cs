using Shouldly;

namespace Maestro.Plugin.Tests;

public class AircraftLandingCircuitBreakerTests
{
    [Fact]
    public void TrySetBreaker_FirstCall_ShouldReturnTrue()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();

        var result = circuitBoard.TrySetBreaker("QFA123");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TrySetBreaker_SecondCall_ShouldReturnFalse()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();

        circuitBoard.TrySetBreaker("QFA123");
        var result = circuitBoard.TrySetBreaker("QFA123");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TrySetBreaker_MultipleCallsSameCallsign_ShouldReturnFalse()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();

        circuitBoard.TrySetBreaker("QFA123");
        circuitBoard.TrySetBreaker("QFA123");
        var result = circuitBoard.TrySetBreaker("QFA123");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TrySetBreaker_DifferentCallsigns_ShouldHaveIndependentBreakers()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();

        var result1 = circuitBoard.TrySetBreaker("QFA123");
        var result2 = circuitBoard.TrySetBreaker("VOZ456");
        var result3 = circuitBoard.TrySetBreaker("QFA123");
        var result4 = circuitBoard.TrySetBreaker("VOZ456");

        result1.ShouldBeTrue();
        result2.ShouldBeTrue();
        result3.ShouldBeFalse();
        result4.ShouldBeFalse();
    }

    [Fact]
    public void TrySetBreaker_ConcurrentCalls_ShouldBeThreadSafe()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();
        var callsign = "QFA123";
        var results = new List<bool>();
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var result = circuitBoard.TrySetBreaker(callsign);
                lock (results)
                {
                    results.Add(result);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        results.Count(r => r == true).ShouldBe(1);
        results.Count(r => r == false).ShouldBe(9);
    }

    [Fact]
    public void TrySetBreaker_MultipleConcurrentCallsigns_ShouldMaintainIndependence()
    {
        var circuitBoard = new AircraftLandingCircuitBreaker();
        var callsigns = new[] { "QFA123", "VOZ456", "JQ789" };
        var results = new Dictionary<string, List<bool>>
        {
            ["QFA123"] = new(),
            ["VOZ456"] = new(),
            ["JQ789"] = new()
        };
        var tasks = new List<Task>();

        foreach (var callsign in callsigns)
        {
            for (var i = 0; i < 5; i++)
            {
                var cs = callsign;
                tasks.Add(Task.Run(() =>
                {
                    var result = circuitBoard.TrySetBreaker(cs);
                    lock (results[cs])
                    {
                        results[cs].Add(result);
                    }
                }));
            }
        }

        Task.WaitAll(tasks.ToArray());

        foreach (var callsign in callsigns)
        {
            results[callsign].Count(r => r == true).ShouldBe(1);
            results[callsign].Count(r => r == false).ShouldBe(4);
        }
    }
}
