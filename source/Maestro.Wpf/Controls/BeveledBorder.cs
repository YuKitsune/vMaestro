using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Maestro.Wpf.Controls;

public enum BevelType
{
    Raised,
    Sunken,
    Outline
}

public class BeveledBorder : Decorator
{
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(BeveledBorder),
            new FrameworkPropertyMetadata(
                new Thickness(0),
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty BevelTypeProperty =
        DependencyProperty.Register(
            nameof(BevelType),
            typeof(BevelType),
            typeof(BeveledBorder),
            new FrameworkPropertyMetadata(
                BevelType.Raised,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public BevelType BevelType
    {
        get => (BevelType)GetValue(BevelTypeProperty);
        set => SetValue(BevelTypeProperty, value);
    }

    private Thickness BorderThicknessDoubled =>
        new Thickness(BorderThickness.Left * 2, BorderThickness.Top * 2, BorderThickness.Right * 2,
            BorderThickness.Bottom * 2);

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child != null)
        {
            var thickness = BevelType == BevelType.Outline ? BorderThicknessDoubled : BorderThickness;

            var availableChildSize = new Size(
                Math.Max(constraint.Width - thickness.Left - thickness.Right, 0),
                Math.Max(constraint.Height - thickness.Top - thickness.Bottom, 0));

            Child.Measure(availableChildSize);

            return new Size(
                Child.DesiredSize.Width + thickness.Left + thickness.Right,
                Child.DesiredSize.Height + thickness.Top + thickness.Bottom);
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child != null)
        {
            var thickness = BevelType == BevelType.Outline ? BorderThicknessDoubled : BorderThickness;
            var childRect = new Rect(
                thickness.Left,
                thickness.Top,
                arrangeSize.Width - thickness.Left - thickness.Right,
                arrangeSize.Height - thickness.Top - thickness.Bottom);

            Child.Arrange(childRect);
        }

        return arrangeSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        switch (BevelType)
        {
            case BevelType.Raised:
            case BevelType.Sunken:
                DrawBevel(dc, BevelType == BevelType.Raised);
                break;

            case BevelType.Outline:
                DrawOutline(dc);
                break;

            default:
                throw new NotSupportedException();
        }
    }

    private void DrawBevel(DrawingContext dc, bool isRaised)
    {
        var light = isRaised ? Theme.LightBrush : Theme.DarkBrush;
        var dark = isRaised ? Theme.DarkBrush : Theme.LightBrush;

        var topLeftGeometry = GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
        dc.DrawGeometry(light, new Pen(light, 0), topLeftGeometry);

        var bottomRightGeometry = GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
        dc.DrawGeometry(dark, new Pen(dark, 0), bottomRightGeometry);
    }

    private void DrawOutline(DrawingContext dc)
    {
        var outerLight = Theme.LightBrush;
        var outerDark = Theme.DarkBrush;
        var innerDark = Theme.DarkBrush;
        var innerLight = Theme.LightBrush;

        dc.DrawGeometry(outerLight, new Pen(outerLight, 0),
            GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness));
        dc.DrawGeometry(outerDark, new Pen(outerDark, 0),
            GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness));

        dc.DrawGeometry(innerDark, new Pen(innerDark, 0),
            GetTopLeftGeometry(BorderThickness.Left, BorderThickness.Top, ActualWidth - BorderThickness.Right,
                ActualHeight - BorderThickness.Bottom, BorderThickness));
        dc.DrawGeometry(innerLight, new Pen(innerLight, 0),
            GetBottomRightGeometry(BorderThickness.Left, BorderThickness.Top, ActualWidth - BorderThickness.Right,
                ActualHeight - BorderThickness.Bottom, BorderThickness));
    }

    private Geometry GetTopLeftGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var segments = new[]
        {
            new Point(left, top),
            new Point(right, top),
            new Point(right - thickness.Right, top + thickness.Top),
            new Point(left + thickness.Left, top + thickness.Top),
            new Point(left + thickness.Left, bottom - thickness.Bottom),
            new Point(left, bottom),
            new Point(left, top)
        }.Select(p => new LineSegment(p, false));

        return new PathGeometry([new PathFigure(new Point(left, top), segments, true)]);
    }

    private Geometry GetBottomRightGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var segments = new[]
        {
            new Point(right, top),
            new Point(right, bottom),
            new Point(left, bottom),
            new Point(left + thickness.Left, bottom - thickness.Bottom),
            new Point(right - thickness.Right, bottom - thickness.Bottom),
            new Point(right - thickness.Right, top + thickness.Bottom),
            new Point(right, top)
        }.Select(p => new LineSegment(p, false));

        return new PathGeometry([new PathFigure(new Point(left, top), segments, true)]);
    }
}