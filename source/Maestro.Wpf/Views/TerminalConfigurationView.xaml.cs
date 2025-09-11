using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class TerminalConfigurationView : UserControl
{
    public TerminalConfigurationView(TerminalConfigurationViewModel terminalConfigurationViewModel)
    {
        InitializeComponent();
        DataContext = terminalConfigurationViewModel;
    }
}