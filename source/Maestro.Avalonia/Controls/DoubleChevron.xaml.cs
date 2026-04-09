using Avalonia;
using Avalonia.Controls;

namespace Maestro.Avalonia.Controls;

/// <summary>
/// Interaction logic for DoubleChevron.xaml
/// </summary>
public partial class DoubleChevron : UserControl
{
    public static readonly StyledProperty<Direction> DirectionProperty =
        AvaloniaProperty.Register<DoubleChevron, Direction>(nameof(Direction), Direction.Up);

    public Direction Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public DoubleChevron()
    {
        InitializeComponent();
    }
}
