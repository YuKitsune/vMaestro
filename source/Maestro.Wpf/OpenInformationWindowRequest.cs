using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf;

public class OpenInformationWindowRequest(FlightMessage flight) : IRequest<OpenInformationWindowResponse>
{
    public FlightMessage Flight { get; } = flight;
}

public record OpenInformationWindowResponse;
