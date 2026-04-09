using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MaestroTheme = global::Maestro.Avalonia.Theme;

namespace Maestro.Avalonia.Controls;

public enum Direction
{
    Left, Up, Right, Down
};

/// <summary>
/// A chevron control with 3D beveled edges
/// </summary>
public class Chevron : Control
{
    public static readonly StyledProperty<Direction> DirectionProperty =
        AvaloniaProperty.Register<Chevron, Direction>(nameof(Direction), Direction.Up);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<Chevron, IBrush?>(nameof(Foreground));

    public Direction Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Chevron()
    {
        Width = 8;
        Height = 8;
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0)
            return;

        DrawBeveledChevron(dc, width, height, Direction);
    }

    private void DrawBeveledChevron(DrawingContext dc, double width, double height, Direction direction)
    {
        IBrush foreground = Foreground ?? Brushes.Black;
        var lineWidth = MaestroTheme.BeveledLineWidth / 2;

        switch (direction)
        {
            case Direction.Up:
                DrawUpChevron(dc, width, height, foreground, lineWidth);
                break;
            case Direction.Down:
                DrawDownChevron(dc, width, height, foreground, lineWidth);
                break;
            case Direction.Left:
                DrawLeftChevron(dc, width, height, foreground, lineWidth);
                break;
            case Direction.Right:
                DrawRightChevron(dc, width, height, foreground, lineWidth);
                break;
        }
    }

    private static Geometry Polygon(params Point[] points)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(points[0], true);
        for (int i = 1; i < points.Length; i++)
            ctx.LineTo(points[i]);
        ctx.EndFigure(true);
        return geom;
    }

    void DrawUpChevron(DrawingContext dc, double width, double height, IBrush foreground, double lineWidth)
    {
        var outerTop = new Point(width / 2, 0);
        var outerBottomLeft = new Point(0, height);
        var outerBottomRight = new Point(width, height);

        dc.DrawGeometry(foreground, null, Polygon(outerTop, outerBottomRight, outerBottomLeft));

        var innerTop = new Point(width / 2, lineWidth);
        var innerBottomLeft = new Point(lineWidth, height - lineWidth);
        var innerBottomRight = new Point(width - lineWidth, height - lineWidth);

        // Left edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerBottomLeft, outerTop, innerTop, innerBottomLeft));

        // Right edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerTop, outerBottomRight, innerBottomRight, innerTop));

        // Bottom edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerBottomRight, outerBottomLeft, innerBottomLeft, innerBottomRight));
    }

    void DrawDownChevron(DrawingContext dc, double width, double height, IBrush foreground, double lineWidth)
    {
        var outerBottom = new Point(width / 2, height);
        var outerTopLeft = new Point(0, 0);
        var outerTopRight = new Point(width, 0);

        dc.DrawGeometry(foreground, null, Polygon(outerBottom, outerTopLeft, outerTopRight));

        var innerBottom = new Point(width / 2, height - lineWidth);
        var innerTopLeft = new Point(lineWidth, lineWidth);
        var innerTopRight = new Point(width - lineWidth, lineWidth);

        // Top edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerTopLeft, outerTopRight, innerTopRight, innerTopLeft));

        // Right edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerTopRight, outerBottom, innerBottom, innerTopRight));

        // Left edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerBottom, outerTopLeft, innerTopLeft, innerBottom));
    }

    void DrawLeftChevron(DrawingContext dc, double width, double height, IBrush foreground, double lineWidth)
    {
        var outerLeft = new Point(0, height / 2);
        var outerTopRight = new Point(width, 0);
        var outerBottomRight = new Point(width, height);

        dc.DrawGeometry(foreground, null, Polygon(outerLeft, outerTopRight, outerBottomRight));

        var innerLeft = new Point(lineWidth, height / 2);
        var innerTopRight = new Point(width - lineWidth, lineWidth);
        var innerBottomRight = new Point(width - lineWidth, height - lineWidth);

        // Top edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerTopRight, outerLeft, innerLeft, innerTopRight));

        // Bottom edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerLeft, outerBottomRight, innerBottomRight, innerLeft));

        // Right edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerBottomRight, outerTopRight, innerTopRight, innerBottomRight));
    }

    void DrawRightChevron(DrawingContext dc, double width, double height, IBrush foreground, double lineWidth)
    {
        var outerRight = new Point(width, height / 2);
        var outerTopLeft = new Point(0, 0);
        var outerBottomLeft = new Point(0, height);

        dc.DrawGeometry(foreground, null, Polygon(outerRight, outerBottomLeft, outerTopLeft));

        var innerRight = new Point(width - lineWidth, height / 2);
        var innerTopLeft = new Point(lineWidth, lineWidth);
        var innerBottomLeft = new Point(lineWidth, height - lineWidth);

        // Left edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerBottomLeft, outerTopLeft, innerTopLeft, innerBottomLeft));

        // Top edge: Light
        dc.DrawGeometry(MaestroTheme.LightBrush, null, Polygon(outerTopLeft, outerRight, innerRight, innerTopLeft));

        // Bottom edge: Dark
        dc.DrawGeometry(MaestroTheme.DarkBrush, null, Polygon(outerRight, outerBottomLeft, innerBottomLeft, innerRight));
    }
}
