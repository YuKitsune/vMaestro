using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class SlotView : UserControl
{
    public SlotView(SlotViewModel slotViewModel)
    {
        InitializeComponent();
        DataContext = slotViewModel;
    }
}

