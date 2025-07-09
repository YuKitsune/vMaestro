using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    [ObservableProperty]
    List<FlightViewModel> _flights = [];

    public void UpdateFlight(FlightMessage flight)
    {
        var flights = Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == flight.Callsign);
        var viewModel = new FlightViewModel(flight);

        if (index != -1)
        {
            flights[index] = viewModel;
        }
        else
        {
            flights.Add(viewModel);
        }

        Flights = flights;
    }

    public void RemoveFlight(string callsign)
    {
        Flights.RemoveAll(f => f.Callsign == callsign);
    }
}
