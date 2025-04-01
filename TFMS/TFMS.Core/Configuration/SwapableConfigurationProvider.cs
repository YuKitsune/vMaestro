using MediatR;

namespace TFMS.Core.Configuration;

public class SwapableConfigurationProvider(IMediator mediator, IConfigurationProvider inner) : IConfigurationProvider
{
    readonly IMediator mediator = mediator;
    IConfigurationProvider inner = inner;

    public MaestroConfiguration GetConfiguration() => inner.GetConfiguration();
    public void SwapProvider(IConfigurationProvider newConfigurationProvider)
    {
        inner = newConfigurationProvider;
        mediator.Publish(new ConfigurationChangedNotification());
    }
}
