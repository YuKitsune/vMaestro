using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class WindView : UserControl
{
    public WindView(WindViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

