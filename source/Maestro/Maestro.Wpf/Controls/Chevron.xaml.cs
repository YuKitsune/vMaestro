using System.Windows;
using System.Windows.Controls;

namespace Maestro.Wpf.Controls;

public enum Direction
{
    Left, Up, Right, Down
};

/// <summary>
/// Interaction logic for Chevron.xaml
/// </summary>
public partial class Chevron : UserControl
{
    public static DependencyProperty DirectionProperty =
        DependencyProperty.Register(
            nameof(Direction),
            typeof(Direction),
            typeof(Chevron),
            new FrameworkPropertyMetadata(Direction.Up));

    public Direction Direction
    { 
        get => (Direction) GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public Chevron()
    {
        InitializeComponent();
    }
}
