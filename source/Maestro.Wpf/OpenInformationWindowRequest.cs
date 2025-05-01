using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf;

public class OpenInformationWindowRequest(FlightViewModel viewModel) : IRequest<OpenInformationWindowResponse>
{
    public FlightViewModel ViewModel { get; } = viewModel;
}

public record OpenInformationWindowResponse;