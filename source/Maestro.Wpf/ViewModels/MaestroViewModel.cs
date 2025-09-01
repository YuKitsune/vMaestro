using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel(IMediator mediator, IErrorReporter errorReporter) : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<SequenceViewModel> _sequences = [];

    [ObservableProperty]
    SequenceViewModel? _selectedSequence;

    [ObservableProperty]
    bool _isCreatingSlot = false;

    [ObservableProperty]
    SlotCreationReferencePoint _slotCreationReferencePoint = SlotCreationReferencePoint.Before;

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
        try
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
                    mediator,
                    errorReporter));
            }
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task MoveFlight(MoveFlightRequest request)
    {
        try
        {
            await mediator.Send(request);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    public void BeginSlotCreation(DateTimeOffset firstSlotTime, SlotCreationReferencePoint slotCreationReferencePoint, string[] runwayIdentifiers)
    {
        IsCreatingSlot = true;
        SlotCreationReferencePoint = slotCreationReferencePoint;

        // Round time based on reference point:
        // Before: round down to the previous minute
        // After: round up to the next minute
        FirstSlotTime = slotCreationReferencePoint == SlotCreationReferencePoint.Before
            ? new DateTimeOffset(firstSlotTime.Year, firstSlotTime.Month, firstSlotTime.Day,
                                firstSlotTime.Hour, firstSlotTime.Minute, 0, firstSlotTime.Offset)
            : new DateTimeOffset(firstSlotTime.Year, firstSlotTime.Month, firstSlotTime.Day,
                                firstSlotTime.Hour, firstSlotTime.Minute, 0, firstSlotTime.Offset).AddMinutes(1);
        SlotRunwayIdentifiers = runwayIdentifiers;
    }

    public void EndSlotCreation(DateTimeOffset secondSlotTime)
    {
        IsCreatingSlot = false;
        SecondSlotTime = secondSlotTime.Rounded();

        var startTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? FirstSlotTime.Value : SecondSlotTime.Value;
        var endTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? SecondSlotTime.Value : FirstSlotTime.Value;

        ShowSlotWindow(startTime, endTime, SlotRunwayIdentifiers);
    }

    async void ShowSlotWindow(DateTimeOffset startTime, DateTimeOffset endTime, string[] runwayIdentifiers)
    {
        try
        {
            if (SelectedSequence?.AirportIdentifier == null) return;

            await mediator.Send(new OpenSlotWindowRequest(
                SelectedSequence.AirportIdentifier,
                null, // slotId is null for new slots
                startTime,
                endTime,
                runwayIdentifiers));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    public async void ShowSlotWindow(SlotMessage slotMessage)
    {
        try
        {
            if (SelectedSequence?.AirportIdentifier == null)
                return;

            await mediator.Send(new OpenSlotWindowRequest(
                SelectedSequence.AirportIdentifier,
                slotMessage.SlotId,
                slotMessage.StartTime,
                slotMessage.EndTime,
                slotMessage.RunwayIdentifiers));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }

    public void ShowInsertFlightWindow(IInsertFlightOptions options)
    {
        try
        {
            if (SelectedSequence?.AirportIdentifier == null)
                return;

            mediator.Send(
                new OpenInsertFlightWindowRequest(
                    SelectedSequence.AirportIdentifier,
                    options,
                    SelectedSequence.Flights.Where(f => f.State is State.Landed).ToArray(),
                    SelectedSequence.Flights.Where(f => f.State is State.Pending).ToArray()));
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex);
        }
    }
}
