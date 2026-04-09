using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class InformationView : UserControl
{
    public InformationView()
    {
        InitializeComponent();
    }

    public InformationView(FlightInformationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
