using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class DialogView : UserControl
{
    public DialogView(DialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

