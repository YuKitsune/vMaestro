using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class InformationView : UserControl
{
    public InformationView(FlightViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}