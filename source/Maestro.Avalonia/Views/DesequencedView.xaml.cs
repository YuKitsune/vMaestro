using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class DesequencedView : UserControl
{
    public DesequencedView()
    {
        InitializeComponent();
    }

    public DesequencedView(DesequencedViewModel desequencedViewModel) : this()
    {
        DataContext = desequencedViewModel;
    }
}