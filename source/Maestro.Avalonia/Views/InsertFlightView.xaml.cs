using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class InsertFlightView : UserControl
{
    public InsertFlightView()
    {
        InitializeComponent();
    }

    public InsertFlightView(InsertFlightViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

