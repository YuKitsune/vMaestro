using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record ChangePermissionsRequest(string AirportIdentifier, IReadOnlyDictionary<string, Role[]> Permissions) : IRequest;
