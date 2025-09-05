using MediatR;

namespace Maestro.Core.Messages;

public class SequenceTerminatedNotification(string airportIdentifier) : INotification;
