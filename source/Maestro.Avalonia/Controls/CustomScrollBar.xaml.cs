using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Maestro.Avalonia.Controls;

public partial class CustomScrollBar : UserControl
{
    private bool _isDragging;
    private Point _lastMousePosition;

    public static readonly StyledProperty<ScrollViewer?> ScrollViewerProperty =
        AvaloniaProperty.Register<CustomScrollBar, ScrollViewer?>(nameof(ScrollViewer));

    public ScrollViewer? ScrollViewer
    {
        get => GetValue(ScrollViewerProperty);
        set => SetValue(ScrollViewerProperty, value);
    }

    public CustomScrollBar()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScrollViewerProperty)
        {
            if (change.OldValue is ScrollViewer oldScrollViewer)
                oldScrollViewer.ScrollChanged -= OnScrollChanged;

            if (change.NewValue is ScrollViewer newScrollViewer)
                newScrollViewer.ScrollChanged += OnScrollChanged;

            UpdateScrollBar();
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (PART_ScrollUpButton != null)
            PART_ScrollUpButton.PointerPressed += OnUpButtonClick;

        if (PART_ScrollDownButton != null)
            PART_ScrollDownButton.PointerPressed += OnDownButtonClick;

        if (PART_Thumb != null)
        {
            PART_Thumb.PointerPressed += OnThumbMouseDown;
            PART_Thumb.PointerReleased += OnThumbMouseUp;
            PART_Thumb.PointerMoved += OnThumbMouseMove;
        }

        // Force initial update
        Dispatcher.UIThread.InvokeAsync(UpdateScrollBar);
        UpdateScrollBar();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateScrollBar();
    }

    private void OnUpButtonClick(object? sender, PointerPressedEventArgs e)
    {
        if (ScrollViewer != null)
            ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, Math.Max(0, ScrollViewer.Offset.Y - 20));
    }

    private void OnDownButtonClick(object? sender, PointerPressedEventArgs e)
    {
        if (ScrollViewer != null)
            ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, ScrollViewer.Offset.Y + 20);
    }

    private void OnThumbMouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (PART_Thumb != null && PART_Track != null)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(PART_Thumb);
            e.Pointer.Capture(PART_Thumb);
        }
    }

    private void OnThumbMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (PART_Thumb != null && _isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }
    }

    private void OnThumbMouseMove(object? sender, PointerEventArgs e)
    {
        var scrollableHeight = ScrollViewer != null
            ? Math.Max(0, ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height)
            : 0;

        if (_isDragging && PART_Track != null && ScrollViewer != null && scrollableHeight > 0)
        {
            var currentTrackPosition = e.GetPosition(PART_Track);
            var trackHeight = PART_Track.Bounds.Height;
            var thumbHeight = PART_Thumb?.Bounds.Height ?? 20;
            var scrollableTrackHeight = trackHeight - thumbHeight;

            if (scrollableTrackHeight > 0)
            {
                var desiredThumbTop = currentTrackPosition.Y - _lastMousePosition.Y;
                var clampedThumbTop = Math.Max(0, Math.Min(scrollableTrackHeight, desiredThumbTop));
                var scrollRatio = clampedThumbTop / scrollableTrackHeight;
                var newScrollOffset = scrollRatio * scrollableHeight;

                ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, newScrollOffset);
            }
        }
    }

    private void UpdateScrollBar()
    {
        if (PART_Thumb == null || PART_Track == null)
            return;

        // Always show the thumb for this UI style
        PART_Thumb.IsVisible = true;

        if (ScrollViewer == null)
        {
            // Default thumb when no ScrollViewer
            PART_Thumb.Height = 20;
            PART_Thumb.Margin = new Thickness(1, 0, 1, 0);
            return;
        }

        var viewportHeight = ScrollViewer.Viewport.Height;
        var contentHeight = ScrollViewer.Extent.Height;
        var scrollOffset = ScrollViewer.Offset.Y;
        var scrollableHeight = Math.Max(0, contentHeight - viewportHeight);

        var trackHeight = PART_Track.Bounds.Height;

        if (trackHeight <= 0)
            return;

        double thumbHeight;
        double thumbPosition;

        if (contentHeight <= viewportHeight || scrollableHeight <= 0)
        {
            thumbHeight = trackHeight;
            thumbPosition = 0;
        }
        else
        {
            var ratio = viewportHeight / contentHeight;
            thumbHeight = Math.Max(20, ratio * trackHeight);
            var scrollableTrackHeight = trackHeight - thumbHeight;
            thumbPosition = scrollableTrackHeight * (scrollOffset / scrollableHeight);
        }

        PART_Thumb.Height = thumbHeight;
        PART_Thumb.Margin = new Thickness(1, thumbPosition, 1, 0);
    }
}
