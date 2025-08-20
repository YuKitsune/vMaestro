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

    readonly string _airportIdentifier;
    readonly Guid? _slotId;

    [ObservableProperty]
    DateTimeOffset _startTime;

    [ObservableProperty]
    DateTimeOffset _endTime;

    readonly string[] _runwayIdentifiers;

    public SlotViewModel(string airportIdentifier, Guid? slotId, DateTimeOffset startTime, DateTimeOffset endTime, string[] runwayIdentifiers, IMediator mediator, IWindowHandle windowHandle)
    {
        _airportIdentifier = airportIdentifier;
        _slotId = slotId;
        StartTime = startTime;
        EndTime = endTime;
        _runwayIdentifiers = runwayIdentifiers;

        _mediator = mediator;
        _windowHandle = windowHandle;
    }

    [RelayCommand]
    public async Task CreateOrModifySlot()
    {
        if (_slotId is null)
        {
            await _mediator.Send(new CreateSlotRequest(_airportIdentifier, StartTime, EndTime, _runwayIdentifiers));
        }
        else
        {
            await _mediator.Send(new ModifySlotRequest(_airportIdentifier, _slotId.Value, StartTime, EndTime));
        }

        _windowHandle.Close();
    }

    [RelayCommand]
    public async Task DeleteSlot()
    {
        if (_slotId is not null)
        {
            await _mediator.Send(new DeleteSlotRequest(_airportIdentifier, _slotId.Value));
        }

        _windowHandle.Close();
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
