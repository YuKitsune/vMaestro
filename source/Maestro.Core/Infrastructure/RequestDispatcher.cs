using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Infrastructure;

public interface IMessageDispatcher
{
    Task Send<T>(T message, CancellationToken cancellationToken);
}

public class MessageDispatcher(IMediator mediator, MaestroConnectionManager connectionManager)
    : IMessageDispatcher
{
    public Task Send<T>(T message, CancellationToken cancellationToken)
    {
        if (message is ISynchronizedMessage synchronizedMessage &&
            connectionManager.TryGetConnection(synchronizedMessage.AirportIdentifier, out var connection))
        {
            return connection.Send(message, cancellationToken);
        }

        return message switch
        {
            INotification _ => mediator.Publish(message, cancellationToken),
            IRequest _ => mediator.Send(message, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }
}
