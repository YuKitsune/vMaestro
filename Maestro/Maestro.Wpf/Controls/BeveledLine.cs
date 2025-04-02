using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Maestro.Wpf;

namespace Maestro.Wpf.Controls;

public class BeveledLine : FrameworkElement
{
    public static DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(BeveledLine),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOrientationChanged));

    public static DependencyProperty FlippedProperty =
        DependencyProperty.Register(
            nameof(Flipped),
            typeof(bool),
            typeof(BeveledLine),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnFlippedChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public bool Flipped
    {
        get => (bool)GetValue(FlippedProperty);
        set => SetValue(FlippedProperty, value);
    }

    static void OnOrientationChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not BeveledLine separator)
            return;

        separator.Orientation = (Orientation)args.NewValue;
        separator.InvalidateVisual();
    }

    static void OnFlippedChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not BeveledLine separator)
            return;

        separator.Flipped = (bool)args.NewValue;
        separator.InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        switch (Orientation)
        {
            case Orientation.Horizontal:
                {
                    var topBrush = Flipped ? Theme.DarkBrush : Theme.LightBrush;
                    var topPen = new Pen(topBrush, 0);
                    var topGeometry = GetGeometry(0, 0, ActualWidth, ActualHeight / 2);
                    drawingContext.DrawGeometry(topBrush, topPen, topGeometry);

                    var bottomBrush = Flipped ? Theme.LightBrush : Theme.DarkBrush;
                    var bottomPen = new Pen(bottomBrush, 0);
                    var bottomGeometry = GetGeometry(0, ActualHeight / 2, ActualWidth, ActualHeight);
                    drawingContext.DrawGeometry(bottomBrush, bottomPen, bottomGeometry);
                    break;
                }
            case Orientation.Vertical:
                {
                    var leftBrush = Flipped ? Theme.DarkBrush : Theme.LightBrush;
                    var leftPen = new Pen(leftBrush, 0);
                    var leftGeometry = GetGeometry(0, 0, ActualWidth / 2, ActualHeight);
                    drawingContext.DrawGeometry(leftBrush, leftPen, leftGeometry);

                    var bottomBrush = Flipped ? Theme.LightBrush : Theme.DarkBrush;
                    var bottomPen = new Pen(bottomBrush, 0);
                    var bottomGeometry = GetGeometry(ActualWidth / 2, 0, ActualWidth, ActualHeight);
                    drawingContext.DrawGeometry(bottomBrush, bottomPen, bottomGeometry);
                    break;
                }
        }
    }

    Geometry GetGeometry(double left, double top, double right, double bottom)
    {
        var segments = new Point[]
            {
                new Point(left, top),
                new Point(right, top),
                new Point(right, bottom),
                new Point(left, bottom),
                new Point(left, top)
            }
            .Select(p => new LineSegment(p, false));

        return new PathGeometry([new PathFigure(new Point(left, top), segments, true)]);
    }
}
