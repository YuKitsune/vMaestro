using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class CoordinationView : UserControl
{
    public CoordinationView(CoordinationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public CoordinationViewModel ViewModel => (CoordinationViewModel) DataContext;

    void Destination_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SendCommand.Execute(null);
    }
}
