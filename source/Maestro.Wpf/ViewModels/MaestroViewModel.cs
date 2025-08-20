using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public class BeginSlotArgs
{
    public DateTimeOffset StartTime { get; set; }
    public string[] RunwayIdentifiers { get; set; } = [];
}

public partial class MaestroViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<SequenceViewModel> _sequences = [];

    [ObservableProperty]
    SequenceViewModel? _selectedSequence;

    [ObservableProperty]
    bool _isCreatingSlot = false;

    [ObservableProperty]
    string[] _slotRunwayIdentifiers = [];

    [ObservableProperty]
    DateTimeOffset? _firstSlotTime = null;

    [ObservableProperty]
    DateTimeOffset? _secondSlotTime = null;

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

    [RelayCommand]
    void InsertFlight()
    {
        // TODO: Implement insert flight functionality
    }

    [RelayCommand]
    void StartInsertingSlot(BeginSlotArgs args)
    {
        IsCreatingSlot = true;
        FirstSlotTime = args.StartTime;
        SlotRunwayIdentifiers = args.RunwayIdentifiers;
    }

    [RelayCommand]
    void EndInsertingSlot(DateTimeOffset secondSlotTime)
    {
        IsCreatingSlot = false;
        SecondSlotTime = secondSlotTime;

        var startTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? FirstSlotTime.Value : SecondSlotTime.Value;
        var endTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? SecondSlotTime.Value : FirstSlotTime.Value;

        ShowSlotWindow(startTime, endTime, SlotRunwayIdentifiers);
    }

    async void ShowSlotWindow(DateTimeOffset startTime, DateTimeOffset endTime, string[] runwayIdentifiers)
    {
        if (SelectedSequence?.AirportIdentifier == null) return;

        await mediator.Send(new Messages.OpenSlotWindowRequest(
            SelectedSequence.AirportIdentifier,
            null, // slotId is null for new slots
            startTime,
            endTime,
            runwayIdentifiers));
    }

    public async void ShowSlotWindow(SlotMessage slotMessage)
    {
        if (SelectedSequence?.AirportIdentifier == null) return;

        await mediator.Send(new Messages.OpenSlotWindowRequest(
            SelectedSequence.AirportIdentifier,
            slotMessage.SlotId,
            slotMessage.StartTime,
            slotMessage.EndTime,
            slotMessage.RunwayIdentifiers));
    }
}
