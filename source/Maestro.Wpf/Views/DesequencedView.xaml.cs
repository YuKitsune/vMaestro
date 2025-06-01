using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class DesequencedView : UserControl
{
    public DesequencedView(DesequencedViewModel desequencedViewModel)
    {
        DataContext = desequencedViewModel;
        InitializeComponent();
    }
}