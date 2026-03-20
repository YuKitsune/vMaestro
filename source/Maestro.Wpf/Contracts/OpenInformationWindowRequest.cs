using Maestro.Contracts.Flights;
using MediatR;

namespace Maestro.Wpf.Contracts;

public class OpenInformationWindowRequest(FlightDto flight) : IRequest<OpenInformationWindowResponse>
{
    public FlightDto Flight { get; } = flight;
}

public record OpenInformationWindowResponse;
