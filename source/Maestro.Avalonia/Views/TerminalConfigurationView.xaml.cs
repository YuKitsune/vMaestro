using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class TerminalConfigurationView : UserControl
{
    public TerminalConfigurationView()
    {
        InitializeComponent();
    }

    public TerminalConfigurationView(TerminalConfigurationViewModel terminalConfigurationViewModel) : this()
    {
        DataContext = terminalConfigurationViewModel;
    }
}