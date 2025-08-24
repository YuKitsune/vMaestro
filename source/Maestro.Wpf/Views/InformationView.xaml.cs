using System.Windows.Controls;
using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class InformationView : UserControl
{
    public InformationView(FlightMessage viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
