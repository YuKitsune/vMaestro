using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Maestro.Wpf.Controls;

public enum Direction
{
    Left, Up, Right, Down
};

/// <summary>
/// A chevron control with 3D beveled edges
/// </summary>
public class Chevron : Control
{
    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(
            nameof(Direction),
            typeof(Direction),
            typeof(Chevron),
            new FrameworkPropertyMetadata(
                Direction.Up,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Direction Direction
    {
        get => (Direction) GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public Chevron()
    {
        Width = 8;
        Height = 8;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        DrawBeveledChevron(dc, width, height, Direction);
    }

    private void DrawBeveledChevron(DrawingContext dc, double width, double height, Direction direction)
    {
        var foreground = Foreground ?? Brushes.Black;
        var lineWidth = Theme.BeveledLineWidth;

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

    private void DrawUpChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Triangle pointing up: top vertex at (width/2, 0), bottom vertices at (0, height) and (width, height)
        var top = new Point(width / 2, 0);
        var bottomLeft = new Point(0, height);
        var bottomRight = new Point(width, height);

        // Draw filled triangle
        var geometry = new PathGeometry([
            new PathFigure(top, [
                new LineSegment(bottomRight, true),
                new LineSegment(bottomLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, geometry);

        // Draw beveled edges
        // Left edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), bottomLeft, top);
        // Right edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), top, bottomRight);
        // Bottom edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), bottomRight, bottomLeft);
    }

    private void DrawDownChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Triangle pointing down: bottom vertex at (width/2, height), top vertices at (0, 0) and (width, 0)
        var bottom = new Point(width / 2, height);
        var topLeft = new Point(0, 0);
        var topRight = new Point(width, 0);

        // Draw filled triangle
        var geometry = new PathGeometry([
            new PathFigure(bottom, [
                new LineSegment(topLeft, true),
                new LineSegment(topRight, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, geometry);

        // Draw beveled edges
        // Top edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), topLeft, topRight);
        // Left edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), topRight, bottom);
        // Right edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), bottom, topLeft);
    }

    private void DrawLeftChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Triangle pointing left: left vertex at (0, height/2), right vertices at (width, 0) and (width, height)
        var left = new Point(0, height / 2);
        var topRight = new Point(width, 0);
        var bottomRight = new Point(width, height);

        // Draw filled triangle
        var geometry = new PathGeometry([
            new PathFigure(left, [
                new LineSegment(topRight, true),
                new LineSegment(bottomRight, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, geometry);

        // Draw beveled edges
        // Top edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), topRight, left);
        // Bottom edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), left, bottomRight);
        // Right edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), bottomRight, topRight);
    }

    private void DrawRightChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Triangle pointing right: right vertex at (width, height/2), left vertices at (0, 0) and (0, height)
        var right = new Point(width, height / 2);
        var topLeft = new Point(0, 0);
        var bottomLeft = new Point(0, height);

        // Draw filled triangle
        var geometry = new PathGeometry([
            new PathFigure(right, [
                new LineSegment(bottomLeft, true),
                new LineSegment(topLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, geometry);

        // Draw beveled edges
        // Left edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), bottomLeft, topLeft);
        // Top edge: Light
        dc.DrawLine(new Pen(Theme.LightBrush, lineWidth), topLeft, right);
        // Bottom edge: Dark
        dc.DrawLine(new Pen(Theme.DarkBrush, lineWidth), right, bottomLeft);
    }
}
