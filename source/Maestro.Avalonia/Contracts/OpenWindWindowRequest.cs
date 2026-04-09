using Maestro.Contracts.Sessions;
using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenWindWindowRequest(
    string AirportIdentifier,
    WindDto SurfaceWind,
    WindDto UpperWind,
    int UpperWindAltitude) : IRequest;
