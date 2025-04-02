using System.Windows.Media;
using System.Windows;
using System.Windows.Markup;

namespace Maestro.Wpf.Controls;

public enum BevelType
{
    Raised,
    Sunken,
    Outline
}

[ContentProperty(nameof(Child))]
public class BeveledBorder : FrameworkElement
{
    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.RegisterAttached(
            nameof(Child),
            typeof(UIElement),
            typeof(BeveledBorder),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsParentArrange |
                FrameworkPropertyMetadataOptions.AffectsArrange |
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnChildChanged));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(BeveledBorder),
            new UIPropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty BevelTypeProperty =
        DependencyProperty.Register(
            nameof(BevelType),
            typeof(BevelType),
            typeof(BeveledBorder),
            new FrameworkPropertyMetadata(
                BevelType.Raised, 
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnBevelTypeChanged));

    public UIElement? Child
    {
        get { return (UIElement)GetValue(ChildProperty); }
        set { SetValue(ChildProperty, value); }
    }

    public Thickness BorderThickness
    {
        get { return (Thickness)GetValue(BorderThicknessProperty); }
        set
        {
            SetValue(BorderThicknessProperty, value);
            InvalidateVisual();
        }
    }

    Thickness BorderThicknessDoubled => new Thickness(BorderThickness.Left * 2, BorderThickness.Top * 2, BorderThickness.Right * 2, BorderThickness.Bottom * 2);

    public BevelType BevelType
    {
        get { return (BevelType)GetValue(BevelTypeProperty); }
        set { SetValue(BevelTypeProperty, value); }
    }

    static void OnChildChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not BeveledBorder beveledBorder)
            return;

        if (args.OldValue is UIElement oldChild)
        {
            beveledBorder.RemoveVisualChild(oldChild);
            beveledBorder.RemoveLogicalChild(oldChild);
        }

        if (args.NewValue is UIElement newChild)
        {
            beveledBorder.AddVisualChild(newChild);
            beveledBorder.AddLogicalChild(newChild);
        }

        beveledBorder.InvalidateVisual();
    }

    static void OnBevelTypeChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not BeveledBorder beveledBorder)
            return;

        beveledBorder.InvalidateVisual();
    }

    protected override int VisualChildrenCount => Child is not null ? 1 : 0 ;

    protected override Visual GetVisualChild(int index) => index == 0 && Child is not null ? Child : throw new ArgumentOutOfRangeException();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child != null)
        {
            var thickness = BevelType == BevelType.Outline ? BorderThicknessDoubled : BorderThickness;

            var availableChildSize = new Size(
                Math.Max(availableSize.Width - thickness.Left - thickness.Right, 0),
                Math.Max(availableSize.Height - thickness.Top - thickness.Bottom, 0));

            Child.Measure(availableChildSize);

            return new Size(
                Child.DesiredSize.Width + thickness.Left + thickness.Right,
                Child.DesiredSize.Height + thickness.Top + thickness.Bottom);
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Arrange the child control, taking padding into account
        if (Child != null)
        {
            var thickness = BevelType == BevelType.Outline ? BorderThicknessDoubled : BorderThickness;
            var childRect = new Rect(
                thickness.Left,
                thickness.Top,
                finalSize.Width - thickness.Left - thickness.Right,
                finalSize.Height - thickness.Top - thickness.Bottom);

            Child.Arrange(childRect);
        }

        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        switch (BevelType)
        {
            case BevelType.Raised:
            case BevelType.Sunken:
                {
                    var topLeftBrush = BevelType == BevelType.Raised ? Theme.LightBrush : Theme.DarkBrush;
                    var topLeftPen = new Pen(topLeftBrush, 0);
                    var topLeftGeometry = GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(topLeftBrush, topLeftPen, topLeftGeometry);

                    var bottomRightBrush = BevelType == BevelType.Raised ? Theme.DarkBrush : Theme.LightBrush;
                    var bottomRightPen = new Pen(bottomRightBrush, 0);
                    var bottomRightGeometry = GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(bottomRightBrush, bottomRightPen, bottomRightGeometry);
                    break;
                }

            case BevelType.Outline:
                {
                    var topLeftOuterBrush = Theme.LightBrush;
                    var topLeftOuterPen = new Pen(topLeftOuterBrush, 0);
                    var topLeftOuterGeometry = GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(topLeftOuterBrush, topLeftOuterPen, topLeftOuterGeometry);

                    var bottomRightOuterBrush = Theme.DarkBrush;
                    var bottomRightOuterPen = new Pen(bottomRightOuterBrush, 0);
                    var bottomRightOuterGeometry = GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(bottomRightOuterBrush, bottomRightOuterPen, bottomRightOuterGeometry);

                    var topLeftInnerBrush = Theme.DarkBrush;
                    var topLeftInnerPen = new Pen(topLeftInnerBrush, 0);
                    var topLeftInnerGeometry = GetTopLeftGeometry(BorderThickness.Left, BorderThickness.Top, ActualWidth - BorderThickness.Right, ActualHeight - BorderThickness.Bottom, BorderThickness);
                    drawingContext.DrawGeometry(topLeftInnerBrush, topLeftInnerPen, topLeftInnerGeometry);

                    var bottomRightInnerBrush = Theme.LightBrush;
                    var bottomRightInnerPen = new Pen(bottomRightInnerBrush, 0);
                    var bottomRightInnerGeometry = GetBottomRightGeometry(BorderThickness.Left, BorderThickness.Top, ActualWidth - BorderThickness.Right, ActualHeight - BorderThickness.Bottom, BorderThickness);
                    drawingContext.DrawGeometry(bottomRightInnerBrush, bottomRightInnerPen, bottomRightInnerGeometry);
                    break;
                }

            default:
                throw new NotSupportedException();
        }
    }

    Geometry GetTopLeftGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var segments = new Point[]
            {
                new Point(left, top),
                new Point(right, top),
                new Point(right - thickness.Right, top + thickness.Top),
                new Point(left + thickness.Left, top + thickness.Top),
                new Point(left + thickness.Left, bottom - thickness.Bottom),
                new Point(left, bottom),
                new Point(left, top)
            }
            .Select(p => new LineSegment(p, false));

        return new PathGeometry([new PathFigure(new Point(left, top), segments, true)]);
    }

    Geometry GetBottomRightGeometry(double left, double top, double right, double bottom, Thickness thickness)
    {
        var segments = new Point[]
            {
                new Point(right, top),
                new Point(right, bottom),
                new Point(left, bottom),
                new Point(left + thickness.Left, bottom - thickness.Bottom),
                new Point(right - thickness.Right, bottom - thickness.Bottom),
                new Point(right - thickness.Right, top + thickness.Bottom),
                new Point(right, top)
            }
            .Select(p => new LineSegment(p, false));

        return new PathGeometry([new PathFigure(new Point(left, top), segments, true)]);
    }
}
