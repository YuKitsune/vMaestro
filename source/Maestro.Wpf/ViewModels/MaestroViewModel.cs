using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    
    [ObservableProperty]
    ObservableCollection<SequenceViewModel> _sequences = [];
    
    [ObservableProperty]
    SequenceViewModel? _selectedSequence;

    public MaestroViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

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
        var response = await _mediator.Send(new InitializeRequest(), CancellationToken.None);

        Sequences.Clear();
        foreach (var item in response.Sequences)
        {
            Sequences.Add(new SequenceViewModel(
                item.AirportIdentifier,
                item.Views,
                item.RunwayModes,
                item.Sequence,
                _mediator));
        }
    }
}
