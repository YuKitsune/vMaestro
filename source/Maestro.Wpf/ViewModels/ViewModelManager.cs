using Maestro.Core;
using Maestro.Core.Extensions;

namespace Maestro.Wpf.ViewModels;

public class ViewModelManager
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly Dictionary<string, MaestroViewModel> _viewModels = new();

    public async Task Register(MaestroViewModel viewModel)
    {
        using var _ = await _semaphore.LockAsync();
        if (_viewModels.ContainsKey(viewModel.AirportIdentifier))
            throw new MaestroException("A view model for this airport has already been registered.");

        _viewModels[viewModel.AirportIdentifier] = viewModel;
    }

    public MaestroViewModel? TryGet(string airportIdentifier)
    {
        _viewModels.TryGetValue(airportIdentifier, out var viewModel);
        return viewModel;
    }

    public async Task Unregister(string airportIdentifier)
    {
        using var _ = await _semaphore.LockAsync();
        _viewModels.Remove(airportIdentifier);
    }
}
