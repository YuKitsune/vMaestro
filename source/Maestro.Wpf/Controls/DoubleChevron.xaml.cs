using System.Windows;
using System.Windows.Controls;

namespace Maestro.Wpf.Controls;

/// <summary>
/// Interaction logic for DoubleChevron.xaml
/// </summary>
public partial class DoubleChevron : UserControl
{
    public static DependencyProperty DirectionProperty =
        DependencyProperty.Register(
            nameof(Direction),
            typeof(Direction),
            typeof(DoubleChevron),
            new FrameworkPropertyMetadata(Direction.Up));

    public Direction Direction
    { 
        get => (Direction) GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public DoubleChevron()
    {
        InitializeComponent();
    }
}