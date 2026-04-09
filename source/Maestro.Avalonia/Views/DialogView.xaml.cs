using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    public DialogView(DialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

