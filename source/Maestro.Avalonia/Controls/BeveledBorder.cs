using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MaestroTheme = global::Maestro.Avalonia.Theme;

namespace Maestro.Avalonia.Controls;

public enum BevelType
{
    Raised,
    Sunken,
    Outline
}

public class BeveledBorder : Decorator
{
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<BeveledBorder, Thickness>(nameof(BorderThickness), new Thickness(0));

    public static readonly StyledProperty<BevelType> BevelTypeProperty =
        AvaloniaProperty.Register<BeveledBorder, BevelType>(nameof(BevelType), BevelType.Raised);

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public BevelType BevelType
    {
        get => GetValue(BevelTypeProperty);
        set => SetValue(BevelTypeProperty, value);
    }

    private Thickness BorderThicknessDoubled => new(
        BorderThickness.Left * 2,
        BorderThickness.Top * 2,
        BorderThickness.Right * 2,
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
                Math.Max(0, arrangeSize.Width - thickness.Left - thickness.Right),
                Math.Max(0, arrangeSize.Height - thickness.Top - thickness.Bottom));

            Child.Arrange(childRect);
        }

        return arrangeSize;
    }

    public override void Render(DrawingContext dc)
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
        var light = isRaised ? MaestroTheme.LightBrush : MaestroTheme.DarkBrush;
        var dark = isRaised ? MaestroTheme.DarkBrush : MaestroTheme.LightBrush;

        dc.DrawGeometry(light, new Pen(light, 0), GetTopLeftGeometry(0, 0, Bounds.Width, Bounds.Height, BorderThickness));
        dc.DrawGeometry(dark, new Pen(dark, 0), GetBottomRightGeometry(0, 0, Bounds.Width, Bounds.Height, BorderThickness));
    }

    private void DrawOutline(DrawingContext dc)
    {
        var outerLight = MaestroTheme.LightBrush;
        var outerDark = MaestroTheme.DarkBrush;
        var innerDark = MaestroTheme.DarkBrush;
        var innerLight = MaestroTheme.LightBrush;

        dc.DrawGeometry(outerLight, new Pen(outerLight, 0),
            GetTopLeftGeometry(0, 0, Bounds.Width, Bounds.Height, BorderThickness));
        dc.DrawGeometry(outerDark, new Pen(outerDark, 0),
            GetBottomRightGeometry(0, 0, Bounds.Width, Bounds.Height, BorderThickness));

        dc.DrawGeometry(innerDark, new Pen(innerDark, 0),
            GetTopLeftGeometry(BorderThickness.Left, BorderThickness.Top, Bounds.Width - BorderThickness.Right,
                Bounds.Height - BorderThickness.Bottom, BorderThickness));
        dc.DrawGeometry(innerLight, new Pen(innerLight, 0),
            GetBottomRightGeometry(BorderThickness.Left, BorderThickness.Top, Bounds.Width - BorderThickness.Right,
                Bounds.Height - BorderThickness.Bottom, BorderThickness));
    }

    private static Geometry GetTopLeftGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(new Point(left, top), true);
        ctx.LineTo(new Point(right, top));
        ctx.LineTo(new Point(right - thickness.Right, top + thickness.Top));
        ctx.LineTo(new Point(left + thickness.Left, top + thickness.Top));
        ctx.LineTo(new Point(left + thickness.Left, bottom - thickness.Bottom));
        ctx.LineTo(new Point(left, bottom));
        ctx.EndFigure(true);
        return geom;
    }

    private static Geometry GetBottomRightGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(new Point(right, top), true);
        ctx.LineTo(new Point(right, bottom));
        ctx.LineTo(new Point(left, bottom));
        ctx.LineTo(new Point(left + thickness.Left, bottom - thickness.Bottom));
        ctx.LineTo(new Point(right - thickness.Right, bottom - thickness.Bottom));
        ctx.LineTo(new Point(right - thickness.Right, top + thickness.Bottom));
        ctx.EndFigure(true);
        return geom;
    }
}
