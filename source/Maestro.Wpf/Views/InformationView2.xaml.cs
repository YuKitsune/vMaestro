using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class InformationView2 : UserControl
{
    public InformationView2(InformationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

