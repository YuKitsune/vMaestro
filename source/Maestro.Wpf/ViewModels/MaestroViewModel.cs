using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<SequenceViewModel> _sequences = [];

    [ObservableProperty]
    SequenceViewModel? _selectedSequence;

    partial void OnSequencesChanged(ObservableCollection<SequenceViewModel> sequences)
    {
        // Deselect the sequence if the selected one no longer exists
        if (SelectedSequence == null || sequences.All(a => a.AirportIdentifier != SelectedSequence.AirportIdentifier))
        {
            SelectedSequence = null;
        }
    }

    [RelayCommand]
    async Task LoadConfiguration()
    {
        var response = await mediator.Send(new InitializeRequest(), CancellationToken.None);

        Sequences.Clear();
        foreach (var item in response.Sequences)
        {
            Sequences.Add(new SequenceViewModel(
                item.AirportIdentifier,
                item.Views,
                item.RunwayModes,
                item.Sequence,
                mediator));
        }
    }

    [RelayCommand]
    async Task MoveFlight(MoveFlightRequest request)
    {
        await mediator.Send(request);
    }
}
