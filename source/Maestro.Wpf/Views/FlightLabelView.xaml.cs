using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maestro.Core.Configuration;

namespace Maestro.Wpf.Views;

/// <summary>
/// Interaction logic for AircraftView.xaml
/// </summary>
public partial class FlightLabelView : UserControl
{
    public static readonly DependencyProperty LadderPositionProperty = DependencyProperty.Register(
        nameof(LadderPosition),
        typeof(LadderPosition),
        typeof(FlightLabelView));

    public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
        nameof(ViewMode),
        typeof(ViewMode),
        typeof(FlightLabelView));

    public static readonly DependencyProperty IsDraggableProperty = DependencyProperty.Register(
        nameof(IsDraggable), typeof(bool), typeof(FlightLabelView), new PropertyMetadata(true));

    public static readonly DependencyProperty IsDraggingProperty = DependencyProperty.Register(
        nameof(IsDragging), typeof(bool), typeof(FlightLabelView), new PropertyMetadata(false));


    public LadderPosition LadderPosition
    {
        get => (LadderPosition)GetValue(LadderPositionProperty);
        set => SetValue(LadderPositionProperty, value);
    }

    public ViewMode ViewMode
    {
        get => (ViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public bool IsDraggable
    {
        get => (bool)GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    public bool IsDragging
    {
        get => (bool)GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    public FlightLabelView()
    {
        InitializeComponent();
    }
}
