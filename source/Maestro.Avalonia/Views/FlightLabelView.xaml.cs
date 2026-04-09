using Avalonia;
using Avalonia.Controls;

namespace Maestro.Avalonia.Views;

/// <summary>
/// Interaction logic for AircraftView.xaml
/// </summary>
public partial class FlightLabelView : UserControl
{
    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<FlightLabelView, bool>(nameof(IsDraggable), defaultValue: true);

    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<FlightLabelView, bool>(nameof(IsDragging), defaultValue: false);

    public bool IsDraggable
    {
        get => GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    public FlightLabelView()
    {
        InitializeComponent();
    }
}
