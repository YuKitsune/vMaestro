using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;

namespace Maestro.Wpf.ViewModels;

public partial class FlightInformationViewModel : ObservableObject
{
    [ObservableProperty] int _numberInSequence;
    [ObservableProperty] string _callsign;
    [ObservableProperty] string _aircraftType;
    [ObservableProperty] WakeCategory _wakeCategory;
    [ObservableProperty] string? _originIdentifier;
    [ObservableProperty] string _destinationIdentifier;
    [ObservableProperty] string? _feederFixIdentifier;
    [ObservableProperty] DateTimeOffset? _initialFeederFixEstimate;
    [ObservableProperty] DateTimeOffset? _feederFixEstimate;
    [ObservableProperty] DateTimeOffset? _feederFixTime;
    [ObservableProperty] string? _assignedRunwayIdentifier;
    [ObservableProperty] string _approachType;
    [ObservableProperty] int _numberToLandOnRunway;
    [ObservableProperty] DateTimeOffset _initialLandingEstimate;
    [ObservableProperty] DateTimeOffset _landingEstimate;
    [ObservableProperty] DateTimeOffset _landingTime;
    [ObservableProperty] TimeSpan _initialDelay;
    [ObservableProperty] TimeSpan _remainingDelay;

    public FlightInformationViewModel(FlightDto flightDto)
    {
        Update(flightDto);

        // TODO: Use flight updates instead
        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (s, m) =>
        {
            var flight = m.Session.Sequence.Flights.FirstOrDefault(f => f.Callsign == Callsign) ??
                         m.Session.DeSequencedFlights.FirstOrDefault(f => f.Callsign == Callsign);

            if (flight == null)
                return;

            Update(flight);
        });
    }

    void Update(FlightDto flightDto)
    {
        NumberInSequence = flightDto.NumberInSequence;
        Callsign = flightDto.Callsign;
        AircraftType = flightDto.AircraftType;
        WakeCategory = flightDto.WakeCategory;
        OriginIdentifier = flightDto.OriginIdentifier;
        DestinationIdentifier = flightDto.DestinationIdentifier;
        FeederFixIdentifier = flightDto.FeederFixIdentifier;
        InitialFeederFixEstimate = flightDto.InitialFeederFixEstimate;
        FeederFixEstimate = flightDto.FeederFixEstimate;
        FeederFixTime = flightDto.FeederFixTime;
        AssignedRunwayIdentifier = flightDto.AssignedRunwayIdentifier;
        ApproachType = flightDto.ApproachType;
        NumberToLandOnRunway = flightDto.NumberToLandOnRunway;
        InitialLandingEstimate = flightDto.InitialLandingEstimate;
        LandingEstimate = flightDto.LandingEstimate;
        LandingTime = flightDto.LandingTime;
        InitialDelay = flightDto.RequiredEnrouteDelay + flightDto.RequiredTerminalDelay;
        RemainingDelay = flightDto.RemainingEnrouteDelay + flightDto.RemainingTerminalDelay;
    }
}
