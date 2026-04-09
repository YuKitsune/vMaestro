using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class WindView : UserControl
{
    public WindView()
    {
        InitializeComponent();
    }

    public WindView(WindViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

