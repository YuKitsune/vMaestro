using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record ChangePermissionsRequest(string AirportIdentifier, IDictionary<string, Role[]> Permissions) : IRequest;