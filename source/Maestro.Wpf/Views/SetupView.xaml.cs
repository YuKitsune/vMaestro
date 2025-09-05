using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class SetupView : UserControl
{
    public SetupView(SetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

