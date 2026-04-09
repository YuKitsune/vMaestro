using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class InformationView2 : UserControl
{
    public InformationView2()
    {
        InitializeComponent();
    }

    public InformationView2(InformationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

