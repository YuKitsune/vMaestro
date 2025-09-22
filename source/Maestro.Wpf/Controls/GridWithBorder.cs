using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Maestro.Wpf.Controls;

public class GridWithBorder : Grid
{
    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Brush),
            typeof(GridWithBorder),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(double),
            typeof(GridWithBorder),
            new FrameworkPropertyMetadata(
                1.0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush BorderBrush
    {
        get => (Brush)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (BorderThickness <= 0)
            return;

        var pen = new Pen(BorderBrush, BorderThickness);

        var rect = new Rect(
            BorderThickness / 2,
            BorderThickness / 2,
            ActualWidth - BorderThickness,
            ActualHeight - BorderThickness);
        dc.DrawRectangle(null, pen, rect);

        // Draw vertical lines between columns
        if (ColumnDefinitions.Count > 1)
        {
            double x = 0;
            for (int i = 0; i < ColumnDefinitions.Count - 1; i++)
            {
                x += GetColumnActualWidth(i);
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
            }
        }

        // Draw horizontal lines between rows
        if (RowDefinitions.Count > 1)
        {
            double y = 0;
            for (int i = 0; i < RowDefinitions.Count - 1; i++)
            {
                y += GetRowActualHeight(i);
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }
    }

    private double GetColumnActualWidth(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= ColumnDefinitions.Count)
            return 0;

        var columnDefinition = ColumnDefinitions[columnIndex];

        // Use reflection to access the internal ActualWidth property
        var actualWidthProperty = typeof(ColumnDefinition).GetProperty("ActualWidth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (actualWidthProperty != null)
        {
            var width = (double)actualWidthProperty.GetValue(columnDefinition);
            if (width > 0) return width;
        }

        // Fallback: For fixed width columns, use the Width value
        if (columnDefinition.Width.IsAbsolute)
        {
            return columnDefinition.Width.Value;
        }

        // Final fallback: equal distribution
        return ActualWidth / ColumnDefinitions.Count;
    }

    private double GetRowActualHeight(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowDefinitions.Count)
            return 0;

        var rowDefinition = RowDefinitions[rowIndex];

        // Use reflection to access the internal ActualHeight property
        var actualHeightProperty = typeof(RowDefinition).GetProperty("ActualHeight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (actualHeightProperty != null)
        {
            var height = (double)actualHeightProperty.GetValue(rowDefinition);
            if (height > 0) return height;
        }

        // Fallback: For fixed height rows, use the Height value
        if (rowDefinition.Height.IsAbsolute)
        {
            return rowDefinition.Height.Value;
        }

        // Final fallback: equal distribution
        return ActualHeight / RowDefinitions.Count;
    }
}
