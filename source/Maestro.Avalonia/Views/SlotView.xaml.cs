using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class SlotView : UserControl
{
    public SlotView()
    {
        InitializeComponent();
    }

    public SlotView(SlotViewModel slotViewModel) : this()
    {
        DataContext = slotViewModel;
    }
}

