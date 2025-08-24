using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class InsertFlightView : UserControl
{
    public InsertFlightView(InsertFlightViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

