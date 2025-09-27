using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class ConnectionView : UserControl
{
    public ConnectionView(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

