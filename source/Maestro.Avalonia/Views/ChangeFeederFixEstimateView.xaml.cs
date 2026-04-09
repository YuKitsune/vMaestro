using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class ChangeFeederFixEstimateView : UserControl
{
    public ChangeFeederFixEstimateView()
    {
        InitializeComponent();
    }

    public ChangeFeederFixEstimateView(ChangeFeederFixEstimateViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

