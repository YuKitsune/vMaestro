using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
    }

    public ConnectionView(ConnectionViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

