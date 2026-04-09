using Avalonia;
using Avalonia.Markup.Xaml;

namespace Maestro.Avalonia.DesignHost;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
