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

    public event EventHandler<double>? DragEnded;
    public event EventHandler<double>? DragStarted;

    bool _isDragging;
    Point _dragStartPoint;
    double _originalTop;

    public FlightLabelView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        };
    }

    void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsDraggable || _isDragging)
            return;

        _isDragging = true;
        DragStarted?.Invoke(this, Canvas.GetTop(this));
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalTop = Canvas.GetTop(this);
        CaptureMouse();
        e.Handled = true;
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDraggable || !_isDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(Parent as IInputElement);
        var deltaY = currentPoint.Y - _dragStartPoint.Y;
        Canvas.SetTop(this, _originalTop + deltaY);
    }

    void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDraggable || !_isDragging)
            return;

        _isDragging = false;
        ReleaseMouseCapture();
        var finalTop = Canvas.GetTop(this);
        DragEnded?.Invoke(this, finalTop);
        e.Handled = true;
    }
}
