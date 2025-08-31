using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class ChangeFeederFixEstimateView : UserControl
{
    public ChangeFeederFixEstimateView(ChangeFeederFixEstimateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

