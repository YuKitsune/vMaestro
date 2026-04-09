using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MaestroTheme = global::Maestro.Avalonia.Theme;

namespace Maestro.Avalonia.Controls;

public class BeveledLine : Control
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<BeveledLine, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<bool> FlippedProperty =
        AvaloniaProperty.Register<BeveledLine, bool>(nameof(Flipped), false);

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public bool Flipped
    {
        get => GetValue(FlippedProperty);
        set => SetValue(FlippedProperty, value);
    }

    static void OnOrientationChanged(object sender, AvaloniaPropertyChangedEventArgs<Orientation> args)
    {
        if (sender is not BeveledLine separator)
            return;

        separator.Orientation = args.NewValue.Value;
        separator.InvalidateVisual();
    }

    static void OnFlippedChanged(object sender, AvaloniaPropertyChangedEventArgs<bool> args)
    {
        if (sender is not BeveledLine separator)
            return;

        separator.Flipped = args.NewValue.Value;
        separator.InvalidateVisual();
    }

    public override void Render(DrawingContext drawingContext)
    {
        switch (Orientation)
        {
            case Orientation.Horizontal:
                {
                    var topBrush = Flipped ? MaestroTheme.DarkBrush : MaestroTheme.LightBrush;
                    var topPen = new Pen(topBrush, 0);
                    var topGeometry = GetGeometry(0, 0, Bounds.Width, Bounds.Height / 2);
                    drawingContext.DrawGeometry(topBrush, topPen, topGeometry);

                    var bottomBrush = Flipped ? MaestroTheme.LightBrush : MaestroTheme.DarkBrush;
                    var bottomPen = new Pen(bottomBrush, 0);
                    var bottomGeometry = GetGeometry(0, Bounds.Height / 2, Bounds.Width, Bounds.Height);
                    drawingContext.DrawGeometry(bottomBrush, bottomPen, bottomGeometry);
                    break;
                }
            case Orientation.Vertical:
                {
                    var leftBrush = Flipped ? MaestroTheme.DarkBrush : MaestroTheme.LightBrush;
                    var leftPen = new Pen(leftBrush, 0);
                    var leftGeometry = GetGeometry(0, 0, Bounds.Width / 2, Bounds.Height);
                    drawingContext.DrawGeometry(leftBrush, leftPen, leftGeometry);

                    var bottomBrush = Flipped ? MaestroTheme.LightBrush : MaestroTheme.DarkBrush;
                    var bottomPen = new Pen(bottomBrush, 0);
                    var bottomGeometry = GetGeometry(Bounds.Width / 2, 0, Bounds.Width, Bounds.Height);
                    drawingContext.DrawGeometry(bottomBrush, bottomPen, bottomGeometry);
                    break;
                }
        }
    }

    static Geometry GetGeometry(double left, double top, double right, double bottom)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(new Point(left, top), true);
        ctx.LineTo(new Point(right, top));
        ctx.LineTo(new Point(right, bottom));
        ctx.LineTo(new Point(left, bottom));
        ctx.EndFigure(true);
        return geom;
    }
}
