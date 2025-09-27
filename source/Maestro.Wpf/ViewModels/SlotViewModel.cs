using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class SlotViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IWindowHandle _windowHandle;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;
    readonly Guid? _slotId;

    [ObservableProperty]
    DateTimeOffset _startTime;

    [ObservableProperty]
    DateTimeOffset _endTime;

    readonly string[] _runwayIdentifiers;

    public SlotViewModel(
        string airportIdentifier,
        Guid? slotId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string[] runwayIdentifiers,
        IMediator mediator,
        IWindowHandle windowHandle,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _slotId = slotId;
        StartTime = startTime;
        EndTime = endTime;
        _runwayIdentifiers = runwayIdentifiers;

        _mediator = mediator;
        _windowHandle = windowHandle;
        _errorReporter = errorReporter;
    }

    [RelayCommand]
    public async Task CreateOrModifySlot()
    {
        try
        {
            if (_slotId is null)
            {
                await _mediator.Send(
                    new CreateSlotRequest(_airportIdentifier, StartTime, EndTime, _runwayIdentifiers),
                    CancellationToken.None);
            }
            else
            {
                await _mediator.Send(
                    new ModifySlotRequest(_airportIdentifier, _slotId.Value, StartTime, EndTime),
                    CancellationToken.None);
            }

            _windowHandle.Close();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    public async Task DeleteSlot()
    {
        try
        {
            if (_slotId is not null)
            {
                await _mediator.Send(
                    new DeleteSlotRequest(_airportIdentifier, _slotId.Value),
                    CancellationToken.None);
            }

            _windowHandle.Close();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
