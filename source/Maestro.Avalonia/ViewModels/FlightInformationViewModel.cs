using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;

namespace Maestro.Avalonia.ViewModels;

public partial class FlightInformationViewModel : ObservableObject
{
    [ObservableProperty] int _numberInSequence;
    [ObservableProperty] string _callsign = string.Empty;
    [ObservableProperty] string _aircraftType = string.Empty;
    [ObservableProperty] WakeCategory _wakeCategory;
    [ObservableProperty] string? _originIdentifier;
    [ObservableProperty] string _destinationIdentifier = string.Empty;
    [ObservableProperty] string? _feederFixIdentifier;
    [ObservableProperty] DateTimeOffset? _initialFeederFixEstimate;
    [ObservableProperty] DateTimeOffset? _feederFixEstimate;
    [ObservableProperty] DateTimeOffset? _feederFixTime;
    [ObservableProperty] string? _assignedRunwayIdentifier;
    [ObservableProperty] string _approachType = string.Empty;
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
        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(
            this,
            (_, m) =>
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
        InitialDelay = flightDto.InitialDelay;
        RemainingDelay = flightDto.RemainingDelay;
    }
}
