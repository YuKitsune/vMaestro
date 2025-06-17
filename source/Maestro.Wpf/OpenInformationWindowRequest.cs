using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf;

public class OpenInformationWindowRequest(FlightViewModel flight) : IRequest<OpenInformationWindowResponse>
{
    public FlightViewModel Flight { get; } = flight;
}

public record OpenInformationWindowResponse;