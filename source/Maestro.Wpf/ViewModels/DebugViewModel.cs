using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    [ObservableProperty]
    List<FlightViewModel> _flights = [];

    public void UpdateFrom(SequenceMessage sequenceMessage)
    {
        Flights = sequenceMessage.Flights.Select(flight => new FlightViewModel(flight)).ToList();
    }
}
