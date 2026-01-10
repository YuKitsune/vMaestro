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
        var lineWidth = Theme.BeveledLineWidth / 2;

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

    void DrawUpChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Outer triangle: top vertex at (width/2, 0), bottom vertices at (0, height) and (width, height)
        var outerTop = new Point(width / 2, 0);
        var outerBottomLeft = new Point(0, height);
        var outerBottomRight = new Point(width, height);

        // Draw full background triangle
        var backgroundGeometry = new PathGeometry([
            new PathFigure(outerTop, [
                new LineSegment(outerBottomRight, true),
                new LineSegment(outerBottomLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, backgroundGeometry);

        var innerTop = new Point(width / 2, lineWidth);
        var innerBottomLeft = new Point(lineWidth, height - lineWidth);
        var innerBottomRight = new Point(width - lineWidth, height - lineWidth);

        // Draw beveled edges as polygons on top
        // Left edge: Light (trapezoid from outer to inner)
        var leftBevelGeometry = new PathGeometry([
            new PathFigure(outerBottomLeft, [
                new LineSegment(outerTop, true),
                new LineSegment(innerTop, true),
                new LineSegment(innerBottomLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, leftBevelGeometry);

        // Right edge: Dark
        var rightBevelGeometry = new PathGeometry([
            new PathFigure(outerTop, [
                new LineSegment(outerBottomRight, true),
                new LineSegment(innerBottomRight, true),
                new LineSegment(innerTop, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, rightBevelGeometry);

        // Bottom edge: Dark
        var bottomBevelGeometry = new PathGeometry([
            new PathFigure(outerBottomRight, [
                new LineSegment(outerBottomLeft, true),
                new LineSegment(innerBottomLeft, true),
                new LineSegment(innerBottomRight, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, bottomBevelGeometry);
    }

    void DrawDownChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Outer triangle: bottom vertex at (width/2, height), top vertices at (0, 0) and (width, 0)
        var outerBottom = new Point(width / 2, height);
        var outerTopLeft = new Point(0, 0);
        var outerTopRight = new Point(width, 0);

        // Draw full background triangle
        var backgroundGeometry = new PathGeometry([
            new PathFigure(outerBottom, [
                new LineSegment(outerTopLeft, true),
                new LineSegment(outerTopRight, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, backgroundGeometry);

        var innerBottom = new Point(width, height - lineWidth);
        var innerTopLeft = new Point(lineWidth, lineWidth);
        var innerTopRight = new Point(width - lineWidth, lineWidth);

        // Draw beveled edges as polygons on top
        // Top edge: Light
        var topBevelGeometry = new PathGeometry([
            new PathFigure(outerTopLeft, [
                new LineSegment(outerTopRight, true),
                new LineSegment(innerTopRight, true),
                new LineSegment(innerTopLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, topBevelGeometry);

        // Right edge: Dark
        var rightBevelGeometry = new PathGeometry([
            new PathFigure(outerTopRight, [
                new LineSegment(outerBottom, true),
                new LineSegment(innerBottom, true),
                new LineSegment(innerTopRight, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, rightBevelGeometry);

        // Left edge: Light
        var leftBevelGeometry = new PathGeometry([
            new PathFigure(outerBottom, [
                new LineSegment(outerTopLeft, true),
                new LineSegment(innerTopLeft, true),
                new LineSegment(innerBottom, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, leftBevelGeometry);
    }

    void DrawLeftChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Outer triangle: left vertex at (0, height/2), right vertices at (width, 0) and (width, height)
        var outerLeft = new Point(0, height / 2);
        var outerTopRight = new Point(width, 0);
        var outerBottomRight = new Point(width, height);

        // Draw full background triangle
        var backgroundGeometry = new PathGeometry([
            new PathFigure(outerLeft, [
                new LineSegment(outerTopRight, true),
                new LineSegment(outerBottomRight, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, backgroundGeometry);

        var innerLeft = new Point(lineWidth, height / 2);
        var innerTopRight = new Point(width - lineWidth, lineWidth);
        var innerBottomRight = new Point(width - lineWidth, height - lineWidth);

        // Draw beveled edges as polygons on top
        // Top edge: Light
        var topBevelGeometry = new PathGeometry([
            new PathFigure(outerTopRight, [
                new LineSegment(outerLeft, true),
                new LineSegment(innerLeft, true),
                new LineSegment(innerTopRight, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, topBevelGeometry);

        // Bottom edge: Dark
        var bottomBevelGeometry = new PathGeometry([
            new PathFigure(outerLeft, [
                new LineSegment(outerBottomRight, true),
                new LineSegment(innerBottomRight, true),
                new LineSegment(innerLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, bottomBevelGeometry);

        // Right edge: Dark
        var rightBevelGeometry = new PathGeometry([
            new PathFigure(outerBottomRight, [
                new LineSegment(outerTopRight, true),
                new LineSegment(innerTopRight, true),
                new LineSegment(innerBottomRight, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, rightBevelGeometry);
    }

    void DrawRightChevron(DrawingContext dc, double width, double height, Brush foreground, double lineWidth)
    {
        // Outer triangle: right vertex at (width, height/2), left vertices at (0, 0) and (0, height)
        var outerRight = new Point(width, height / 2);
        var outerTopLeft = new Point(0, 0);
        var outerBottomLeft = new Point(0, height);

        // Draw full background triangle
        var backgroundGeometry = new PathGeometry([
            new PathFigure(outerRight, [
                new LineSegment(outerBottomLeft, true),
                new LineSegment(outerTopLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(foreground, null, backgroundGeometry);

        var innerRight = new Point(width - lineWidth, height / 2);
        var innerTopLeft = new Point(lineWidth, lineWidth);
        var innerBottomLeft = new Point(lineWidth, height - lineWidth);

        // Draw beveled edges as polygons on top
        // Left edge: Light
        var leftBevelGeometry = new PathGeometry([
            new PathFigure(outerBottomLeft, [
                new LineSegment(outerTopLeft, true),
                new LineSegment(innerTopLeft, true),
                new LineSegment(innerBottomLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, leftBevelGeometry);

        // Top edge: Light
        var topBevelGeometry = new PathGeometry([
            new PathFigure(outerTopLeft, [
                new LineSegment(outerRight, true),
                new LineSegment(innerRight, true),
                new LineSegment(innerTopLeft, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.LightBrush, null, topBevelGeometry);

        // Bottom edge: Dark
        var bottomBevelGeometry = new PathGeometry([
            new PathFigure(outerRight, [
                new LineSegment(outerBottomLeft, true),
                new LineSegment(innerBottomLeft, true),
                new LineSegment(innerRight, true)
            ], true)
        ]);
        dc.DrawGeometry(Theme.DarkBrush, null, bottomBevelGeometry);
    }
}
