using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class CoordinationView : UserControl
{
    public CoordinationView()
    {
        InitializeComponent();
    }

    public CoordinationView(CoordinationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public CoordinationViewModel ViewModel => (CoordinationViewModel) DataContext;

    void Destination_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SendCommand.Execute(null);
    }
}
