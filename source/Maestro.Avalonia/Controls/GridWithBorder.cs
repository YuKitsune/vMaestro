using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Maestro.Avalonia.Controls;

public class GridWithBorder : Grid
{
    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        AvaloniaProperty.Register<GridWithBorder, IBrush>(nameof(BorderBrush), new SolidColorBrush(Colors.Black));

    public static readonly StyledProperty<double> BorderThicknessProperty =
        AvaloniaProperty.Register<GridWithBorder, double>(nameof(BorderThickness), 1.0);

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public double BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    private readonly BorderOverlay _overlay;

    public GridWithBorder()
    {
        _overlay = new BorderOverlay(this);
        _overlay.IsHitTestVisible = false;
        _overlay.ZIndex = 1000;
        Children.Add(_overlay);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Grid.SetRow(_overlay, 0);
        Grid.SetColumn(_overlay, 0);
        Grid.SetRowSpan(_overlay, Math.Max(1, RowDefinitions.Count));
        Grid.SetColumnSpan(_overlay, Math.Max(1, ColumnDefinitions.Count));

        var result = base.ArrangeOverride(finalSize);
        _overlay.InvalidateVisual();
        return result;
    }

    private double GetColumnActualWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= ColumnDefinitions.Count)
            return 0;

        var columnDefinition = ColumnDefinitions[columnIndex];

        var actualWidthProperty = typeof(ColumnDefinition).GetProperty("ActualWidth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (actualWidthProperty != null)
        {
            var width = (double)actualWidthProperty.GetValue(columnDefinition);
            if (width > 0) return width;
        }

        if (columnDefinition.Width.IsAbsolute)
            return columnDefinition.Width.Value;

        return Bounds.Width / ColumnDefinitions.Count;
    }

    private double GetRowActualHeight(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowDefinitions.Count)
            return 0;

        var rowDefinition = RowDefinitions[rowIndex];

        var actualHeightProperty = typeof(RowDefinition).GetProperty("ActualHeight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (actualHeightProperty != null)
        {
            var height = (double)actualHeightProperty.GetValue(rowDefinition);
            if (height > 0) return height;
        }

        if (rowDefinition.Height.IsAbsolute)
            return rowDefinition.Height.Value;

        return Bounds.Height / RowDefinitions.Count;
    }

    private class BorderOverlay : Control
    {
        private readonly GridWithBorder _parent;

        public BorderOverlay(GridWithBorder parent)
        {
            _parent = parent;
        }

        public override void Render(DrawingContext dc)
        {
            base.Render(dc);

            if (_parent.BorderThickness <= 0)
                return;

            var pen = new Pen(_parent.BorderBrush, _parent.BorderThickness);
            var w = Bounds.Width;
            var h = Bounds.Height;
            var t = _parent.BorderThickness;

            dc.DrawRectangle(null, pen, new Rect(t / 2, t / 2, w - t, h - t));

            if (_parent.ColumnDefinitions.Count > 1)
            {
                double x = 0;
                for (int i = 0; i < _parent.ColumnDefinitions.Count - 1; i++)
                {
                    x += _parent.GetColumnActualWidth(i);
                    dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
                }
            }

            if (_parent.RowDefinitions.Count > 1)
            {
                double y = 0;
                for (int i = 0; i < _parent.RowDefinitions.Count - 1; i++)
                {
                    y += _parent.GetRowActualHeight(i);
                    dc.DrawLine(pen, new Point(0, y), new Point(w, y));
                }
            }
        }
    }
}
