using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

// TODO: Reimplement this at a later date.
//  This is currently unused.

public interface IRunwayAssigner
{
    string[] FindBestRunways(
        string aircraftType,
        string feederFixIdentifier,
        RunwayAssignmentRule[] rules);
}

public class RunwayAssigner : IRunwayAssigner
{
    readonly IPerformanceLookup _performanceLookup;

    public RunwayAssigner(IPerformanceLookup performanceLookup)
    {
        _performanceLookup = performanceLookup;
    }

    public string[] FindBestRunways(
        string aircraftType,
        string feederFixIdentifier,
        RunwayAssignmentRule[] rules)
    {
        var performanceData = _performanceLookup.GetPerformanceDataFor(aircraftType);
        if (performanceData is null)
            return [];
        
        var candidates = new List<RunwayAssignmentRule>();
        foreach (var runwayAssignmentRule in rules)
        {
            var typeMatch = performanceData.IsJet == runwayAssignmentRule.Jets
                            || !performanceData.IsJet == runwayAssignmentRule.NonJets;
            
            var wakeMatch = (runwayAssignmentRule.Heavy && performanceData.WakeCategory is WakeCategory.Heavy or WakeCategory.SuperHeavy)
                || (runwayAssignmentRule.Medium && performanceData.WakeCategory is WakeCategory.Medium)
                || (runwayAssignmentRule.Light && performanceData.WakeCategory is WakeCategory.Light);

            var feederMatch = runwayAssignmentRule.FeederFixes.Any()
                              && !string.IsNullOrEmpty(feederFixIdentifier)
                              && runwayAssignmentRule.FeederFixes.Contains(feederFixIdentifier);
            
            if (typeMatch && feederMatch && wakeMatch)
                candidates.Add(runwayAssignmentRule);
        }

        if (!candidates.Any())
            return [];

        if (candidates.Count == 1)
            return [candidates.Single().RunwayIdentifier];
        
        return candidates.OrderBy(c => c.Priority)
            .Select(c => c.RunwayIdentifier)
            .Distinct()
            .ToArray();

        // TODO: Defer to runway direction
        // if (string.IsNullOrEmpty(feederFixIdentifier) && lastKnownPosition.HasValue)
        // {
        //     var destination = _fixProvider.FindFix()
        //     if (flight.Estimates.Length == 0)
        //     {
        //         return candidates.First().RunwayIdentifier;
        //     }
        //
        //     var airportCoords = flight.Estimates.Last().Coordinate;
        //     
        //     var track = Calculations.CalculateTrack(lastKnownPosition.Value.ToCoordinate(), airportCoords);
        //     
        //     var delta = double.MaxValue;
        //     var winner = candidates.First();
        //     foreach(var rule in candidates)
        //     {
        //         // Approximate the runway heading using the first two digits
        //         if (rule.RunwayIdentifier.Length < 2 ||
        //             !int.TryParse(rule.RunwayIdentifier.Substring(0, 2), out var approximateRunwayHeading))
        //             continue;
        //         
        //         approximateRunwayHeading *= 10;
        //         var test = Math.Abs(track - approximateRunwayHeading);
        //         if (test < delta)
        //         {
        //             delta = test;
        //             winner = rule;
        //         }
        //     }
        //     
        //     return winner.RunwayIdentifier;
        // }

        // TODO: Defer to track miles
        // var distanceWinner = candidates.First();
        // var shortest = int.MaxValue;
        // foreach (var rule in candidates)
        // {
        //     var trackMiles = GetTrackMilesToRunway(flight.AssignedStarIdentifier, flight.AssignedRunwayIdentifier);
        //
        //     if (trackMiles <= 0)
        //         continue;
        //     
        //     if (trackMiles < shortest)
        //     {
        //         distanceWinner = rule;
        //         shortest = trackMiles;
        //     }
        // }
        //
        // return distanceWinner.RunwayIdentifier;
    }
}