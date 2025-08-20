using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Maestro.Wpf.Controls;

public partial class CustomScrollBar : UserControl
{
    private bool _isDragging;
    private Point _lastMousePosition;

    public static readonly DependencyProperty ScrollViewerProperty =
        DependencyProperty.Register(
            nameof(ScrollViewer),
            typeof(ScrollViewer),
            typeof(CustomScrollBar),
            new PropertyMetadata(null, OnScrollViewerChanged));

    public ScrollViewer? ScrollViewer
    {
        get => (ScrollViewer?)GetValue(ScrollViewerProperty);
        set => SetValue(ScrollViewerProperty, value);
    }

    public CustomScrollBar()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (PART_ScrollUpButton != null)
            PART_ScrollUpButton.MouseLeftButtonDown += OnUpButtonClick;

        if (PART_ScrollDownButton != null)
            PART_ScrollDownButton.MouseLeftButtonDown += OnDownButtonClick;

        if (PART_Thumb != null)
        {
            PART_Thumb.MouseLeftButtonDown += OnThumbMouseDown;
            PART_Thumb.MouseLeftButtonUp += OnThumbMouseUp;
            PART_Thumb.MouseMove += OnThumbMouseMove;
        }

        // Force initial update
        Dispatcher.BeginInvoke(new Action(() => UpdateScrollBar()));
        UpdateScrollBar();
    }

    private static void OnScrollViewerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomScrollBar scrollBar)
        {
            if (e.OldValue is ScrollViewer oldScrollViewer)
            {
                oldScrollViewer.ScrollChanged -= scrollBar.OnScrollChanged;
            }

            if (e.NewValue is ScrollViewer newScrollViewer)
            {
                newScrollViewer.ScrollChanged += scrollBar.OnScrollChanged;
            }

            scrollBar.UpdateScrollBar();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateScrollBar();
    }

    private void OnUpButtonClick(object sender, MouseButtonEventArgs e)
    {
        ScrollViewer?.LineUp();
    }

    private void OnDownButtonClick(object sender, MouseButtonEventArgs e)
    {
        ScrollViewer?.LineDown();
    }

    private void OnThumbMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (PART_Thumb != null && PART_Track != null)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(PART_Thumb);
            PART_Thumb.CaptureMouse();
        }
    }

    private void OnThumbMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (PART_Thumb != null && _isDragging)
        {
            _isDragging = false;
            PART_Thumb.ReleaseMouseCapture();
        }
    }

    private void OnThumbMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && PART_Track != null && ScrollViewer != null && ScrollViewer.ScrollableHeight > 0)
        {
            var currentTrackPosition = e.GetPosition(PART_Track);
            var trackHeight = PART_Track.ActualHeight;
            var thumbHeight = PART_Thumb?.ActualHeight ?? 20;
            var scrollableTrackHeight = trackHeight - thumbHeight;

            if (scrollableTrackHeight > 0)
            {
                // Calculate where the thumb should be positioned
                // currentTrackPosition.Y is where the mouse is relative to track
                // _lastMousePosition.Y is the offset within the thumb where the mouse was clicked
                var desiredThumbTop = currentTrackPosition.Y - _lastMousePosition.Y;

                // Clamp to valid range
                var clampedThumbTop = Math.Max(0, Math.Min(scrollableTrackHeight, desiredThumbTop));

                // Convert thumb position to scroll offset
                var scrollRatio = clampedThumbTop / scrollableTrackHeight;
                var newScrollOffset = scrollRatio * ScrollViewer.ScrollableHeight;

                ScrollViewer.ScrollToVerticalOffset(newScrollOffset);
            }
        }
    }

    private void UpdateScrollBar()
    {
        if (PART_Thumb == null || PART_Track == null)
            return;

        // Always show the thumb for this UI style
        PART_Thumb.Visibility = Visibility.Visible;

        if (ScrollViewer == null)
        {
            // Default thumb when no ScrollViewer
            PART_Thumb.Height = 20;
            PART_Thumb.Margin = new Thickness(1, 0, 1, 0);
            return;
        }

        var viewportHeight = ScrollViewer.ViewportHeight;
        var contentHeight = ScrollViewer.ExtentHeight;
        var scrollOffset = ScrollViewer.VerticalOffset;

        // Calculate thumb size and position
        var trackHeight = PART_Track.ActualHeight;

        if (trackHeight <= 0)
        {
            // Track not ready yet
            return;
        }

        double thumbHeight;
        double thumbPosition;

        if (contentHeight <= viewportHeight || ScrollViewer.ScrollableHeight <= 0)
        {
            // No scrolling needed - thumb takes full track height
            thumbHeight = trackHeight;
            thumbPosition = 0;
        }
        else
        {
            // Calculate proportional thumb based on viewport to content ratio
            var ratio = viewportHeight / contentHeight;
            thumbHeight = Math.Max(20, ratio * trackHeight); // Minimum 20px height
            var scrollableHeight = trackHeight - thumbHeight;
            thumbPosition = scrollableHeight * (scrollOffset / ScrollViewer.ScrollableHeight);
        }

        PART_Thumb.Height = thumbHeight;
        PART_Thumb.Margin = new Thickness(1, thumbPosition, 1, 0);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateScrollBar();
    }
}
