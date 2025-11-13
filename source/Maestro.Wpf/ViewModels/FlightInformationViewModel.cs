using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Messages;
using Maestro.Core.Model;

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
    [ObservableProperty] int _numberToLandOnRunway;
    [ObservableProperty] DateTimeOffset _initialLandingEstimate;
    [ObservableProperty] DateTimeOffset _landingEstimate;
    [ObservableProperty] DateTimeOffset _landingTime;
    [ObservableProperty] TimeSpan _initialDelay;
    [ObservableProperty] TimeSpan _remainingDelay;

    public FlightInformationViewModel(FlightMessage flightMessage)
    {
        Update(flightMessage);

        // TODO: Use flight updates instead
        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (s, m) =>
        {
            var flight = m.Session.Sequence.Flights.FirstOrDefault(f => f.Callsign == Callsign) ??
                         m.Session.DeSequencedFlights.FirstOrDefault(f => f.Callsign == Callsign) ??
                         m.Session.PendingFlights.FirstOrDefault(f => f.Callsign == Callsign);

            if (flight == null)
                return;

            Update(flight);
        });
    }

    void Update(FlightMessage flightMessage)
    {
        NumberInSequence = flightMessage.NumberInSequence;
        Callsign = flightMessage.Callsign;
        AircraftType = flightMessage.AircraftType;
        WakeCategory = flightMessage.WakeCategory;
        OriginIdentifier = flightMessage.OriginIdentifier;
        DestinationIdentifier = flightMessage.DestinationIdentifier;
        FeederFixIdentifier = flightMessage.FeederFixIdentifier;
        InitialFeederFixEstimate = flightMessage.InitialFeederFixEstimate;
        FeederFixEstimate = flightMessage.FeederFixEstimate;
        FeederFixTime = flightMessage.FeederFixTime;
        AssignedRunwayIdentifier = flightMessage.AssignedRunwayIdentifier;
        NumberToLandOnRunway = flightMessage.NumberToLandOnRunway;
        InitialLandingEstimate = flightMessage.InitialLandingEstimate;
        LandingEstimate = flightMessage.LandingEstimate;
        LandingTime = flightMessage.LandingTime;
        InitialDelay = flightMessage.InitialDelay;
        RemainingDelay = flightMessage.RemainingDelay;
    }
}
