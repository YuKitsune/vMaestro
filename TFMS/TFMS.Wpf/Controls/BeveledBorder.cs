using System.Windows.Media;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;

namespace TFMS.Wpf.Controls;

[ContentProperty(nameof(Child))]
class BeveledBorder : FrameworkElement
{
    static readonly float Alpha = 0.375f;
    static readonly Brush LightBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 255, 255, 255));
    static readonly Brush DarkBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 0, 0, 0));

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
                FrameworkPropertyMetadataOptions.AffectsRender));

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
            new UIPropertyMetadata(BevelType.Raised));

    public UIElement? Child
    {
        get { return (UIElement)GetValue(ChildProperty); }
        set
        {
            var old = Child;
            if (old is not null)
            {
                RemoveVisualChild(old);
                RemoveLogicalChild(old);
            }

            SetValue(ChildProperty, value);

            if (value is not null)
            {
                RemoveVisualChild(value);
                RemoveLogicalChild(value);
            }

            InvalidateVisual();
        }
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

    Thickness BorderThicknessHalved => new Thickness(BorderThickness.Left / 2, BorderThickness.Top / 2, BorderThickness.Right / 2, BorderThickness.Bottom / 2);

    public BevelType BevelType
    {
        get { return (BevelType)GetValue(BevelTypeProperty); }
        set
        { 
            SetValue(BevelTypeProperty, value);
            InvalidateVisual();
        }
    }

    protected override int VisualChildrenCount => Child is not null ? 1 : 0 ;

    protected override Visual GetVisualChild(int index) => index == 0 && Child is not null ? Child : throw new ArgumentOutOfRangeException();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child != null)
        {
            var availableChildSize = new Size(
                availableSize.Width - BorderThickness.Left - BorderThickness.Right,
                availableSize.Height - BorderThickness.Top - BorderThickness.Bottom);

            Child.Measure(availableChildSize);

            return new Size(
                Child.DesiredSize.Width + BorderThickness.Left + BorderThickness.Right,
                Child.DesiredSize.Height + BorderThickness.Top + BorderThickness.Bottom);
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Arrange the child control, taking padding into account
        if (Child != null)
        {
            var childRect = new Rect(
                BorderThickness.Left,
                BorderThickness.Top,
                finalSize.Width - BorderThickness.Left - BorderThickness.Right,
                finalSize.Height - BorderThickness.Top - BorderThickness.Bottom);

            Child.Arrange(childRect);
        }

        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        switch (BevelType)
        {
            case Wpf.BevelType.Raised:
            case Wpf.BevelType.Sunken:
                {
                    var topLeftBrush = BevelType == Wpf.BevelType.Raised ? LightBrush : DarkBrush;
                    var topLeftPen = new Pen(topLeftBrush, 0);
                    var topLeftGeometry = GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(topLeftBrush, topLeftPen, topLeftGeometry);

                    var bottomRightBrush = BevelType == Wpf.BevelType.Raised ? DarkBrush : LightBrush;
                    var bottomRightPen = new Pen(bottomRightBrush, 0);
                    var bottomRightGeometry = GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThickness);
                    drawingContext.DrawGeometry(bottomRightBrush, bottomRightPen, bottomRightGeometry);
                    break;
                }

            case Wpf.BevelType.Outline:
                {
                    var topLeftOuterBrush = LightBrush;
                    var topLeftOuterPen = new Pen(topLeftOuterBrush, 0);
                    var topLeftOuterGeometry = GetTopLeftGeometry(0, 0, ActualWidth, ActualHeight, BorderThicknessHalved);
                    drawingContext.DrawGeometry(topLeftOuterBrush, topLeftOuterPen, topLeftOuterGeometry);

                    var bottomRightOuterBrush = DarkBrush;
                    var bottomRightOuterPen = new Pen(bottomRightOuterBrush, 0);
                    var bottomRightOuterGeometry = GetBottomRightGeometry(0, 0, ActualWidth, ActualHeight, BorderThicknessHalved);
                    drawingContext.DrawGeometry(bottomRightOuterBrush, bottomRightOuterPen, bottomRightOuterGeometry);

                    var topLeftInnerBrush = DarkBrush;
                    var topLeftInnerPen = new Pen(topLeftInnerBrush, 0);
                    var topLeftInnerGeometry = GetTopLeftGeometry(BorderThicknessHalved.Left, BorderThicknessHalved.Top, ActualWidth - BorderThicknessHalved.Right, ActualHeight - BorderThicknessHalved.Bottom, BorderThicknessHalved);
                    drawingContext.DrawGeometry(topLeftInnerBrush, topLeftInnerPen, topLeftInnerGeometry);

                    var bottomRightInnerBrush = LightBrush;
                    var bottomRightInnerPen = new Pen(bottomRightInnerBrush, 0);
                    var bottomRightInnerGeometry = GetBottomRightGeometry(BorderThicknessHalved.Left, BorderThicknessHalved.Top, ActualWidth - BorderThicknessHalved.Right, ActualHeight - BorderThicknessHalved.Bottom, BorderThicknessHalved);
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
