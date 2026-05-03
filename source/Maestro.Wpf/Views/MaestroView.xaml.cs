using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Wpf.Contracts;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Wpf.Controls;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using MediatR;
using Point = System.Windows.Point;

namespace Maestro.Wpf.Views;

/// <summary>
/// Interaction logic for MaestroView.xaml
/// </summary>
public partial class MaestroView : IRecipient<VatsysTrackSelectedNotification>
{
    const int FooterHeight = 32;
    const int LineThickness = 2;

    readonly DispatcherTimer _dispatcherTimer;
    private bool _isDragging = false;
    private Point? _contextMenuPosition;
    private bool _suppressContextMenu = false;

    // Drag state
    private FlightLabelView? _draggingFlightLabel;
    private Point _dragStartPoint;
    private double _originalTop;
    private bool _hasMoved;

    // Flight label reuse pool
    private readonly Dictionary<string, FlightLabelView> _flightLabels = new();
    private string? _vatsysSelectedTrackCallsign;

    DateTimeOffset ReferenceTime => DateTimeOffset.UtcNow.Add(ViewModel.ScrollOffset);

    public MaestroView(MaestroViewModel maestroViewModel)
    {
        InitializeComponent();
        DataContext = maestroViewModel;

        _dispatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _dispatcherTimer.Tick += TimerTick;
        _dispatcherTimer.Start();

        Loaded += ControlLoaded;
        SizeChanged += OnSizeChanged;

        WeakReferenceMessenger.Default.Register(this);

        // Subscribe to property changes that affect rendering
        maestroViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MaestroViewModel.ScrollOffset) ||
                args.PropertyName == nameof(MaestroViewModel.SelectedView) ||
                args.PropertyName == nameof(MaestroViewModel.HorizontalScrollOffset))
            {
                DrawSequence();
            }
        };
    }

    public MaestroViewModel ViewModel => (MaestroViewModel)DataContext;

    public void Receive(VatsysTrackSelectedNotification message)
    {
        _vatsysSelectedTrackCallsign = message.Callsign;
    }

    void TimerTick(object sender, EventArgs args)
    {
        // Update UTC time display
        UtcTimeText?.Text = DateTime.UtcNow.ToString("HH:mm:ss");

        TryDrawSequence();
    }

    void ControlLoaded(object sender, RoutedEventArgs e)
    {
        DrawSequence();
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawSequence();
    }

    void TryDrawSequence()
    {
        // Don't redraw during confirmation dialogs
        if (ViewModel.IsConfirmationDialogOpen)
            return;

        DrawSequence();
    }

    void DrawSequence()
    {
        if (_isDragging) return;
        Dispatcher.Invoke(() =>
        {
            RedrawSequence(ReferenceTime);
            UpdateLabels(ReferenceTime);
            InvalidateVisual();
        });
    }

    List<int> GetMatchingLadders(FlightDto flight)
    {
        var view = (ViewConfiguration)ViewModel.SelectedView;
        var matching = new List<int>();

        for (int i = 0; i < view.Ladders.Length; i++)
        {
            var ladder = view.Ladders[i];

            // Empty arrays match all
            bool matchesRunway = ladder.Runways.Length == 0 ||
                ladder.Runways.Contains(flight.AssignedRunwayIdentifier);
            bool matchesFeeder = ladder.FeederFixes.Length == 0 ||
                (flight.FeederFixIdentifier != null && ladder.FeederFixes.Contains(flight.FeederFixIdentifier));

            if (matchesRunway && matchesFeeder)
            {
                matching.Add(i);
            }
        }

        return matching;
    }

    LadderElement? GetClickedLadder(double clickX, LayoutInfo layout)
    {
        // Find which ladder the click position is in
        foreach (var element in layout.VisualElements.OfType<LadderElement>())
        {
            if (clickX >= element.X && clickX <= element.X + element.Width)
            {
                return element;
            }
        }

        return null;
    }

    double MinuteHeight(int timeHorizonMinutes) => LadderCanvas.ActualHeight / timeHorizonMinutes;

    double MeasureCharacterWidth(string character)
    {
        var textBlock = new TextBlock
        {
            FontFamily = Theme.FontFamily,
            FontSize = Theme.FontSize,
            Text = character
        };

        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return textBlock.DesiredSize.Width;
    }

    LayoutInfo CalculateLadderLayout(ViewConfiguration view, LabelLayoutConfiguration labelLayout, double canvasWidth)
    {
        // Calculate character width by measuring '_'
        double characterWidth = MeasureCharacterWidth("_");

        // Calculate ladder width from label items (character count * character width)
        int totalCharacters = labelLayout.Items.Sum(item => item.Width + item.Padding);
        // Add padding from UserControl and margin from ItemsControl in FlightLabelView
        // UserControl.Padding + ItemsControl.Margin = 2 * BeveledBorderThickness on each side
        double horizontalSpacing = 2 * (Theme.BeveledBorderThickness.Left + Theme.BeveledBorderThickness.Right);
        double ladderWidth = totalCharacters * characterWidth + horizontalSpacing;

        const double tickWidth = 8;
        const double timelineWidth = 24;
        const double gapWidth = 24;

        // Calculate horizontal offset based on HorizontalScrollOffset
        // Each scroll unit represents 2 ladders (one complete pattern)
        // Pattern: Ladder 0 → Ticks → Timeline → Ticks → Ladder 1 → Gap → Ladder 2 → ...
        int laddersToSkip = ViewModel.HorizontalScrollOffset * 2;
        double horizontalOffset = ViewModel.HorizontalScrollOffset * (2 * ladderWidth + 2 * tickWidth + timelineWidth + gapWidth);

        // Build visual elements in order: Ladder -> Ticks -> Timeline -> Ticks -> Ladder -> ...
        var elements = new List<VisualElement>();
        double xPos = 0;

        for (int i = 0; i < view.Ladders.Length; i++)
        {
            // Skip ladders that are scrolled out of view
            if (i < laddersToSkip)
            {
                // Still need to advance xPos to maintain correct spacing for later calculations
                xPos += ladderWidth;

                if (i % 2 == 0)
                {
                    xPos += tickWidth + timelineWidth + tickWidth;
                }
                else if (i + 1 < view.Ladders.Length)
                {
                    xPos += gapWidth;
                }
                continue;
            }

            // Add ladder with offset applied
            elements.Add(new LadderElement
            {
                Index = i,
                X = xPos - horizontalOffset,
                Width = ladderWidth,
                Config = view.Ladders[i]
            });
            xPos += ladderWidth;

            // Only even-indexed ladders (0, 2, 4...) have ticks and timeline after them
            // Pattern: Ladder 0 → Ticks 0 → Timeline → Ticks 1 → Ladder 1 → Ladder 2 → Ticks 2 → Timeline → ...
            if (i % 2 == 0)
            {
                // Add ticks to right of even-indexed ladder
                elements.Add(new TicksElement
                {
                    X = xPos - horizontalOffset,
                    Width = tickWidth,
                    LadderIndex = i
                });
                xPos += tickWidth;

                // Add timeline after even-indexed ladder
                elements.Add(new TimelineElement
                {
                    X = xPos - horizontalOffset,
                    Width = timelineWidth,
                    LeftLadderIndex = i,
                    RightLadderIndex = i + 1 < view.Ladders.Length ? i + 1 : -1
                });
                xPos += timelineWidth;

                // Add ticks to left of next ladder if it exists
                if (i + 1 < view.Ladders.Length)
                {
                    elements.Add(new TicksElement
                    {
                        X = xPos - horizontalOffset,
                        Width = tickWidth,
                        LadderIndex = i + 1
                    });
                    xPos += tickWidth;
                }
            }
            else
            {
                // Odd-indexed ladders (1, 3, 5...) have a gap after them if not the last ladder
                if (i + 1 < view.Ladders.Length)
                {
                    xPos += gapWidth;
                }
            }
        }

        return new LayoutInfo { VisualElements = elements };
    }

    void RedrawSequence(DateTimeOffset referenceTime)
    {
        // Remove all elements that are NOT flight labels
        for (int i = LadderCanvas.Children.Count - 1; i >= 0; i--)
        {
            var child = LadderCanvas.Children[i];
            if (child is not FlightLabelView)
            {
                LadderCanvas.Children.RemoveAt(i);
            }
        }

        var canvasWidth = LadderCanvas.ActualWidth;
        var canvasHeight = LadderCanvas.ActualHeight;

        // Get current view first to check direction
        var view = ViewModel.SelectedView;
        var labelLayout = ViewModel.GetLabelLayout(view);

        if (labelLayout == null)
        {
            // TODO: Display an error
            //  Ideally before the plugin starts.
            return;
        }

        // Reserve space for footer (reference boxes and filter text)
        var ladderHeight = canvasHeight - FooterHeight;

        // Prevent rendering when canvas is too small to avoid infinite loop hangs
        const double minCanvasSize = 10.0;
        if (ladderHeight < minCanvasSize || canvasWidth < minCanvasSize)
        {
            return;
        }

        var layout = CalculateLadderLayout(view, labelLayout, canvasWidth);

        // Calculate ladder offset based on direction
        var ladderYOffset = view.Direction == TimelineDirection.Up ? FooterHeight : 0;

        // Draw all visual elements using ladderHeight for the drawing area
        foreach (var element in layout.VisualElements)
        {
            switch (element)
            {
                case TicksElement ticks:
                    DrawTicks(ticks, ladderHeight, referenceTime, view.TimeWindowMinutes, view.Direction, ladderYOffset);
                    break;

                case TimelineElement timeline:
                    DrawTimeline(timeline, ladderHeight, referenceTime, view.TimeWindowMinutes, view.Direction, ladderYOffset);
                    break;
            }
        }

        DrawSlots(referenceTime, view, layout, ladderHeight, ladderYOffset);
        DrawFooter(referenceTime, view, canvasWidth, canvasHeight, layout);
    }

    void DrawFooter(
        DateTimeOffset referenceTime,
        ViewConfiguration view,
        double canvasWidth,
        double canvasHeight,
        LayoutInfo layout)
    {
        DrawFooterBackground(view.Direction, canvasWidth);
        foreach (var element in layout.VisualElements.OfType<TimelineElement>())
        {
            DrawTimeReferenceBox(element, referenceTime, view.Direction);
            DrawLadderFooter(element, view, view.Direction, canvasHeight);
        }
    }

    void DrawFooterBackground(TimelineDirection direction, double canvasWidth)
    {
        const double footerHeight = 32;

        var background = new System.Windows.Shapes.Rectangle
        {
            Width = canvasWidth,
            Height = footerHeight,
            Fill = Theme.BackgroundColor
        };

        // Position at top when Direction is Up, bottom when Direction is Down
        if (direction == TimelineDirection.Up)
        {
            background.Loaded += PositionOnCanvas(
                left: 0,
                top: 0);
        }
        else
        {
            background.Loaded += PositionOnCanvas(
                left: 0,
                bottom: 0);
        }

        LadderCanvas.Children.Add(background);
    }

    void DrawTicks(TicksElement ticks, double canvasHeight, DateTimeOffset referenceTime, int timeWindowMinutes, TimelineDirection direction, double ladderYOffset)
    {
        // Draw a tick for every minute
        var nextMinute = referenceTime.Add(new TimeSpan(0, 0, 60 - referenceTime.Second));
        var yOffset = GetYOffsetForTime(referenceTime, nextMinute, timeWindowMinutes);

        while (yOffset <= canvasHeight)
        {
            var yPosition = GetYPositionForOffset(yOffset, canvasHeight, direction) + ladderYOffset;

            var tick = new BeveledLine
            {
                Orientation = Orientation.Horizontal,
                Height = LineThickness,
                Width = ticks.Width,
            };

            tick.Loaded += PositionOnCanvas(
                x: ticks.X + ticks.Width / 2,
                y: yPosition);

            LadderCanvas.Children.Add(tick);

            yOffset += MinuteHeight(timeWindowMinutes);
        }
    }

    void DrawTimeline(
        TimelineElement timeline,
        double canvasHeight,
        DateTimeOffset referenceTime,
        int timeWindowMinutes,
        TimelineDirection direction,
        double ladderYOffset)
    {
        // Draw left border
        var leftBorder = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
        };

        leftBorder.Loaded += PositionOnCanvas(
            top: ladderYOffset,
            x: timeline.X);

        LadderCanvas.Children.Add(leftBorder);

        // Draw right border
        var rightBorder = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
            Flipped = true,
        };

        rightBorder.Loaded += PositionOnCanvas(
            top: ladderYOffset,
            x: timeline.X + timeline.Width);

        LadderCanvas.Children.Add(rightBorder);

        // Draw 5-minute markers
        var nextTime = GetNearest5Minutes(referenceTime);
        var yOffset = GetYOffsetForTime(referenceTime, nextTime, timeWindowMinutes);

        while (yOffset <= canvasHeight)
        {
            var yPosition = GetYPositionForOffset(yOffset, canvasHeight, direction) + ladderYOffset;
            var text = new TextBlock
            {
                Text = nextTime.Minute.ToString("00")
            };

            text.Loaded += PositionOnCanvas(
                x: timeline.X + timeline.Width / 2,
                y: yPosition);

            LadderCanvas.Children.Add(text);

            nextTime = nextTime.AddMinutes(5);
            yOffset += MinuteHeight(timeWindowMinutes) * 5;
        }
    }

    void DrawTimeReferenceBox(TimelineElement timeline, DateTimeOffset referenceTime, TimelineDirection direction)
    {
        // Draw reference time using BeveledBorder (like V1)
        const double refBoxWidth = 80;

        var refTimeText = new TextBlock
        {
            Text = referenceTime.ToString("HH:mm:ss"),
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ViewModel.IsScrolling ? Theme.InteractiveTextColor : Theme.GenericTextColor
        };

        var border = new BeveledBorder
        {
            Width = refBoxWidth,
            Height = 32,
            BevelType = BevelType.Outline,
            BorderThickness = Theme.BeveledBorderThickness,
            Child = refTimeText
        };

        // Position at top when Direction is Up, bottom when Direction is Down
        if (direction == TimelineDirection.Up)
        {
            border.Loaded += PositionOnCanvas(
                x: timeline.X + timeline.Width / 2,
                top: 0);
        }
        else
        {
            border.Loaded += PositionOnCanvas(
                x: timeline.X + timeline.Width / 2,
                bottom: 0);
        }

        LadderCanvas.Children.Add(border);
    }

    void DrawLadderFooter(TimelineElement timeline, ViewConfiguration view, TimelineDirection direction, double canvasHeight)
    {
        // Draw info for both left and right ladders if they exist
        if (timeline.LeftLadderIndex >= 0 && timeline.LeftLadderIndex < view.Ladders.Length)
        {
            DrawLadderInfo(timeline.LeftLadderIndex, view, timeline, isLeftLadder: true, direction, canvasHeight);
        }

        if (timeline.RightLadderIndex >= 0 && timeline.RightLadderIndex < view.Ladders.Length)
        {
            DrawLadderInfo(timeline.RightLadderIndex, view, timeline, isLeftLadder: false, direction, canvasHeight);
        }
    }

    void DrawLadderInfo(int ladderIndex, ViewConfiguration view, TimelineElement timeline, bool isLeftLadder, TimelineDirection direction, double canvasHeight)
    {
        var ladder = view.Ladders[ladderIndex];

        // Format filter text
        var feeders = ladder.FeederFixes.Length == 0
            ? "All Feeders"
            : string.Join(", ", ladder.FeederFixes);

        var runways = ladder.Runways.Length == 0
            ? "All Runways"
            : string.Join(", ", ladder.Runways);

        // Timeline reference box is wider than timeline
        const double refBoxWidth = 80;
        double refBoxCenter = timeline.X + (timeline.Width / 2);

        if (isLeftLadder)
        {
            // Even-indexed ladder: Filter text on LEFT of ref box, right-aligned
            double textX = refBoxCenter - (refBoxWidth / 2) - 10;

            var runwayText = new TextBlock
            {
                Text = runways,
                TextAlignment = TextAlignment.Right,
                Foreground = Theme.GenericTextColor
            };

            var feederText = new TextBlock
            {
                Text = feeders,
                TextAlignment = TextAlignment.Right,
                Foreground = Theme.GenericTextColor
            };

            if (direction == TimelineDirection.Up)
            {
                runwayText.Loaded += PositionOnCanvas(
                    right: LadderCanvas.ActualWidth - textX,
                    top: 2);
                feederText.Loaded += PositionOnCanvas(
                    right: LadderCanvas.ActualWidth - textX,
                    top: 17);
            }
            else
            {
                runwayText.Loaded += PositionOnCanvas(
                    right: LadderCanvas.ActualWidth - textX,
                    bottom: 17);
                feederText.Loaded += PositionOnCanvas(
                    right: LadderCanvas.ActualWidth - textX,
                    bottom: 2);
            }

            LadderCanvas.Children.Add(runwayText);
            LadderCanvas.Children.Add(feederText);
        }
        else
        {
            // Odd-indexed ladder: Filter text on RIGHT of ref box, left-aligned
            double textX = refBoxCenter + (refBoxWidth / 2) + 10;

            var runwayText = new TextBlock
            {
                Text = runways,
                TextAlignment = TextAlignment.Left,
                Foreground = Theme.GenericTextColor
            };

            var feederText = new TextBlock
            {
                Text = feeders,
                TextAlignment = TextAlignment.Left,
                Foreground = Theme.GenericTextColor
            };

            if (direction == TimelineDirection.Up)
            {
                runwayText.Loaded += PositionOnCanvas(
                    left: textX,
                    top: 2);
                feederText.Loaded += PositionOnCanvas(
                    left: textX,
                    top: 17);
            }
            else
            {
                runwayText.Loaded += PositionOnCanvas(
                    left: textX,
                    bottom: 17);
                feederText.Loaded += PositionOnCanvas(
                    left: textX,
                    bottom: 2);
            }

            LadderCanvas.Children.Add(runwayText);
            LadderCanvas.Children.Add(feederText);
        }
    }

    void DrawSlots(DateTimeOffset currentTime, ViewConfiguration view, LayoutInfo layout, double ladderHeight, double ladderYOffset)
    {
        // Slots are only drawn when the view uses LandingTimes
        if (view.Reference != LadderReference.LandingTime)
            return;

        foreach (var slot in ViewModel.Slots)
        {
            // Calculate slot position
            var startYOffset = GetYOffsetForTime(currentTime, slot.StartTime, view.TimeWindowMinutes);
            var endYOffset = GetYOffsetForTime(currentTime, slot.EndTime, view.TimeWindowMinutes);

            // Only draw slots that are visible on the timeline
            if (endYOffset < 0 || startYOffset > ladderHeight)
                continue;

            var startYPosition = GetYPositionForOffset(startYOffset, ladderHeight, view.Direction) + ladderYOffset;
            var endYPosition = GetYPositionForOffset(endYOffset, ladderHeight, view.Direction) + ladderYOffset;
            var slotHeight = Math.Abs(startYPosition - endYPosition);
            var topY = Math.Min(startYPosition, endYPosition);

            // Find which ladders this slot should be drawn on
            for (int i = 0; i < view.Ladders.Length; i++)
            {
                var ladder = view.Ladders[i];

                // Check if slot's runways match this ladder's filter
                // Draw if ladder has no filter OR if any of the slot's runways are in the ladder's filter
                bool shouldDraw = ladder.Runways.Length == 0 ||
                    slot.RunwayIdentifiers.Any(slotRunway => ladder.Runways.Contains(slotRunway));

                if (!shouldDraw)
                    continue;

                // Find the TicksElement for this ladder
                var ticksElement = layout.VisualElements
                    .OfType<TicksElement>()
                    .FirstOrDefault(t => t.LadderIndex == i);

                if (ticksElement == null)
                    continue;

                var rectangle = new System.Windows.Shapes.Rectangle
                {
                    Width = ticksElement.Width,
                    Height = slotHeight,
                    Fill = Theme.UnstableColor,
                    Opacity = 1,
                    Cursor = Cursors.Hand
                };

                rectangle.MouseLeftButtonDown += (_, args) =>
                {
                    ViewModel.ShowSlotWindow(slot);
                    args.Handled = true;
                };

                rectangle.Loaded += PositionOnCanvas(
                    x: ticksElement.X + ticksElement.Width / 2,
                    top: topY);

                LadderCanvas.Children.Add(rectangle);
            }
        }
    }

    static RoutedEventHandler PositionOnCanvas(
        double? left = null,
        double? top = null,
        double? right = null,
        double? bottom = null,
        double? x = null,
        double? y = null)
    {
        return (sender, _) =>
        {
            if (sender is not FrameworkElement element)
                return;

            if (left.HasValue)
                Canvas.SetLeft(element, left.Value);

            if (top.HasValue)
                Canvas.SetTop(element, top.Value);

            if (right.HasValue)
                Canvas.SetRight(element, right.Value);

            if (bottom.HasValue)
                Canvas.SetBottom(element, bottom.Value);

            if (x.HasValue)
            {
                Canvas.SetLeft(element, x.Value - element.ActualWidth / 2);
            }

            if (y.HasValue)
            {
                Canvas.SetTop(element, y.Value - element.ActualHeight / 2);
            }
        };
    }

    double GetYOffsetForTime(DateTimeOffset currentTime, DateTimeOffset nextTime, int timeWindowMinutes)
    {
        return (nextTime - currentTime).TotalMinutes * MinuteHeight(timeWindowMinutes);
    }

    double GetYPositionForOffset(double yOffset, double canvasHeight, TimelineDirection direction)
    {
        return direction switch
        {
            TimelineDirection.Up => yOffset,
            TimelineDirection.Down => canvasHeight - yOffset,
            _ => yOffset
        };
    }

    DateTimeOffset GetNearest5Minutes(DateTimeOffset currentTime)
    {
        // Round up the minutes to the next multiple of 5
        var minutes = currentTime.Minute / 5 * 5;
        if (currentTime.Minute % 5 != 0 || currentTime.Second > 0 || currentTime.Millisecond > 0)
        {
            minutes += 5; // Add 5 minutes to round up
        }

        // If we added 5 minutes, and it goes beyond 60 minutes, reset to 0 and increment the hour
        if (minutes >= 60)
        {
            minutes = 0;
            currentTime = currentTime.AddHours(1);
        }

        // Create the new DateTime with the rounded-up minute
        var nextInterval = new DateTimeOffset(
            currentTime.Year,
            currentTime.Month,
            currentTime.Day,
            currentTime.Hour,
            minutes,
            0,
            currentTime.Offset);

        return nextInterval;
    }

    DateTimeOffset GetTimeForYOffset(DateTimeOffset referenceTime, double yOffset)
    {
        // Convert the Y offset back to a time
        var minutes = yOffset / MinuteHeight(ViewModel.SelectedView.TimeWindowMinutes);
        var newTime = referenceTime.AddMinutes(minutes);
        return newTime;
    }

    DateTimeOffset GetTimeForClickPosition(double clickY)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var view = ViewModel.SelectedView;

        const double footerHeight = 32;
        var ladderHeight = canvasHeight - footerHeight;
        var ladderYOffset = view.Direction == TimelineDirection.Up ? footerHeight : 0;

        var adjustedY = clickY - ladderYOffset;
        var yOffset = view.Direction == TimelineDirection.Up
            ? adjustedY
            : ladderHeight - adjustedY;

        return GetTimeForYOffset(ReferenceTime, yOffset);
    }

    void OnCanvasLeftClick(object sender, MouseButtonEventArgs e)
    {
        // If there's a selected flight, move it to the clicked position
        if (ViewModel.SelectedFlight != null)
        {
            var clickPosition = e.GetPosition(LadderCanvas);
            var canvasWidth = LadderCanvas.ActualWidth;
            var clickTime = GetTimeForClickPosition(clickPosition.Y);

            // Get the clicked ladder's runway filters
            var view = ViewModel.SelectedView;
            var labelLayout = ViewModel.GetLabelLayout(view);
            var layout = CalculateLadderLayout(view, labelLayout!, canvasWidth);

            var clickedLadder = GetClickedLadder(clickPosition.X, layout);
            var availableRunways = clickedLadder != null && clickedLadder.Index < view.Ladders.Length
                ? view.Ladders[clickedLadder.Index].Runways
                : Array.Empty<string>();

            // If the selected ladder doesn't have the assigned runway, move the flight to the first runway defined
            // in the new ladder
            var runwayIdentifier = ViewModel.SelectedFlight.AssignedRunwayIdentifier;
            if (availableRunways.Length != 0 && !availableRunways.Contains(runwayIdentifier))
                runwayIdentifier = availableRunways.First();

            // Move the selected flight to the clicked position
            ViewModel.MoveFlightWithoutConfirmationCommand.Execute(new MoveFlightRequest(
                ViewModel.AirportIdentifier,
                ViewModel.SelectedFlight.Callsign,
                runwayIdentifier,
                clickTime
            ));

            // Deselect the flight after moving
            ViewModel.DeselectFlight();

            e.Handled = true;
        }
        else
        {
            // No flight selected, just deselect any existing selection
            ViewModel.DeselectFlight();
        }
    }

    void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        // Right-clicking when a flight is selected will deselect it
        if (ViewModel.SelectedFlight != null)
        {
            ViewModel.DeselectFlight();
            _suppressContextMenu = true; // Flag to suppress context menu
            TryDrawSequence(); // Immediately update UI to reflect deselection
            e.Handled = true;
            return;
        }

        // No context menu in Enroute mode
        if (ViewModel.SelectedView.Reference == LadderReference.FeederFixTime)
        {
            _suppressContextMenu = true;
        }

        _contextMenuPosition = e.GetPosition(LadderCanvas);

        e.Handled = true;
    }

    void InsertSlot(object sender, RoutedEventArgs routedEventArgs)
    {
        if (sender is not MenuItem menuItem)
            return;

        var clickData = GetClickDataFor(menuItem);
        if (clickData is null)
            return;

        if (clickData.Reference == LadderReference.FeederFixTime)
            return;

        ViewModel.ShowSlotWindow(
            clickData.ClickTime,
            clickData.ClickTime.Add(TimeSpan.FromMinutes(5)),
            clickData.FilterItems);

        _contextMenuPosition = null;
    }

    void InsertFlight(object sender, RoutedEventArgs routedEventArgs)
    {
        if (sender is not MenuItem menuItem)
            return;

        var clickData = GetClickDataFor(menuItem);
        if (clickData is null)
            return;

        if (clickData.Reference == LadderReference.FeederFixTime)
            return;

        var options = new ExactInsertionOptions(clickData.ClickTime, clickData.FilterItems);

        ViewModel.ShowInsertFlightWindow(options);
    }

    ClickData? GetClickDataFor(MenuItem menuItem)
    {
        // Get the ContextMenu to access its placement target (the Canvas)
        var contextMenu = menuItem.Parent as ContextMenu;
        if (contextMenu?.PlacementTarget is not Canvas canvas || !_contextMenuPosition.HasValue)
            return null;

        contextMenu.IsOpen = false;

        // Use the stored right-click position to determine the time
        var mousePosition = _contextMenuPosition.Value;
        var clickTime = GetTimeForClickPosition(mousePosition.Y);

        // Get the clicked ladder's runway filters
        var view = ViewModel.SelectedView;
        var labelLayout = ViewModel.GetLabelLayout(view);

        if (labelLayout == null)
            return null;

        var canvasWidth = canvas.ActualWidth;
        var layout = CalculateLadderLayout(view, labelLayout, canvasWidth);

        var clickedLadder = GetClickedLadder(mousePosition.X, layout);
        var filterItems = clickedLadder != null && clickedLadder.Index < view.Ladders.Length
            ? view.Ladders[clickedLadder.Index].Runways
            : Array.Empty<string>();

        return new ClickData(
            clickTime,
            view.Reference,
            filterItems);
    }

    record ClickData(
        DateTimeOffset ClickTime,
        LadderReference Reference,
        string[] FilterItems);

    void OnFlightLabelMouseDown(object sender, MouseButtonEventArgs e, FlightDto flight)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Only handle mouse up when view is referenced by STA
        if (ViewModel.SelectedView.Reference != LadderReference.LandingTime)
            return;

        if (_isDragging)
            return;

        // Check if dragging is enabled for this flight label
        if (!flightLabel.IsDraggable)
        {
            // If not draggable, select immediately
            ViewModel.SelectFlight(flight);
            TryDrawSequence(); // Immediately update UI to reflect selection
            return;
        }

        // For draggable flights, defer selection until we know if it's a click or drag
        _draggingFlightLabel = flightLabel;
        _dragStartPoint = e.GetPosition(LadderCanvas);
        _originalTop = Canvas.GetTop(flightLabel);
        _hasMoved = false;

        flightLabel.CaptureMouse();
        e.Handled = true;
    }

    void OnFlightLabelMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Only handle mouse up when view is referenced by STA
        if (ViewModel.SelectedView.Reference != LadderReference.LandingTime)
            return;

        if (_draggingFlightLabel != flightLabel || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(LadderCanvas);
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        // Only start dragging if mouse has moved significantly (avoids accidental drags on clicks)
        if (!_hasMoved && Math.Abs(deltaY) > SystemParameters.MinimumVerticalDragDistance)
        {
            _hasMoved = true;
            _isDragging = true;
            flightLabel.IsDragging = true;
        }

        if (_hasMoved)
        {
            Canvas.SetTop(flightLabel, _originalTop + deltaY);
        }
    }

    void OnFlightLabelMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Only handle mouse up when view is referenced by STA
        if (ViewModel.SelectedView.Reference != LadderReference.LandingTime)
            return;

        if (_draggingFlightLabel != flightLabel)
            return;

        flightLabel.ReleaseMouseCapture();

        if (_hasMoved && _isDragging)
        {
            // This was a drag operation - move the flight
            var finalTop = Canvas.GetTop(flightLabel);
            var centerY = finalTop + (flightLabel.ActualHeight / 2);
            var newTime = GetTimeForClickPosition(centerY);

            // Get the flight from the flight label's data context
            if (flightLabel.DataContext is FlightLabelViewModel flightLabelViewModel)
            {
                var flight = flightLabelViewModel.FlightViewModel;

                ViewModel.MoveFlightWithConfirmationCommand.Execute(new MoveFlightRequest(
                    ViewModel.AirportIdentifier,
                    flight.Callsign,
                    flight.AssignedRunwayIdentifier,
                    newTime
                ));

                DrawSequence();
            }
        }
        else
        {
            // This was a click operation
            if (flightLabel.DataContext is FlightLabelViewModel flightLabelViewModel)
            {
                var clickedFlight = flightLabelViewModel.FlightViewModel;

                // Clicked the same flight, deselect it
                if (ViewModel.SelectedFlight?.Callsign == clickedFlight.Callsign)
                {
                    ViewModel.DeselectFlight();
                }

                // Clicked a different flight, swap them
                else if (ViewModel.SelectedFlight != null && ViewModel.SelectedFlight.Callsign != clickedFlight.Callsign)
                {
                    // Swap the positions of the selected flight and the clicked flight
                    ViewModel.SwapFlightsCommand.Execute(
                        new SwapFlightsRequest(
                            ViewModel.AirportIdentifier,
                            ViewModel.SelectedFlight.Callsign,
                            clickedFlight.Callsign
                        ));

                    // Deselect after swapping
                    ViewModel.DeselectFlight();
                }
                else
                {
                    // No flight selected or clicking the same flight - just select this flight
                    ViewModel.SelectFlight(clickedFlight);
                }

                TryDrawSequence(); // Immediately update UI to reflect selection
            }
        }

        // Reset drag state
        _isDragging = false;
        if (_draggingFlightLabel != null)
        {
            _draggingFlightLabel.IsDragging = false;
        }
        _draggingFlightLabel = null;
        _hasMoved = false;

        e.Handled = true;
    }

    void OnFlightLabelDoubleClick(object sender, MouseButtonEventArgs e, FlightDto flight)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Double-clicking a flight makes it stable
        ViewModel.MakeStableCommand.Execute(new MakeStableRequest(
            ViewModel.AirportIdentifier,
            flight.Callsign
        ));

        e.Handled = true;
    }

    void OnFlightLabelRightClick(object sender, MouseButtonEventArgs e, FlightDto flight)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Right-clicking a flight label deselects any selected flight and prevents context menu
        if (ViewModel.SelectedFlight != null)
        {
            ViewModel.DeselectFlight();
            _suppressContextMenu = true; // Flag to suppress context menu
            TryDrawSequence(); // Immediately update UI to reflect deselection
        }

        // Always handle the event to prevent it from bubbling to the canvas
        // (which would suppress the context menu in Enroute mode)
        e.Handled = true;
    }

    void SuppressContextMenuIfRequired(object sender, ContextMenuEventArgs e)
    {
        if (!_suppressContextMenu)
            return;

        e.Handled = true;
        _suppressContextMenu = false;
    }

    void UpdateLabels(DateTimeOffset referenceTime)
    {
        var canvasWidth = LadderCanvas.ActualWidth;
        var canvasHeight = LadderCanvas.ActualHeight;

        // Reserve space at bottom for footer
        const double footerHeight = 32;
        var ladderHeight = canvasHeight - footerHeight;

        // Prevent rendering when canvas is too small
        const double minCanvasSize = 10.0;
        if (ladderHeight < minCanvasSize || canvasWidth < minCanvasSize)
        {
            return;
        }

        var view = ViewModel.SelectedView;
        var labelLayout = ViewModel.GetLabelLayout(view);

        if (labelLayout == null)
        {
            // Fall back to V1 if no label layout
            return;
        }

        var layout = CalculateLadderLayout(view, labelLayout, canvasWidth);

        // Calculate ladder offset based on direction
        double ladderYOffset = view.Direction == TimelineDirection.Up ? footerHeight : 0;

        var currentFlights = ViewModel.Flights
            .Where(f => f.State is State.Unstable or State.Stable or State.SuperStable or State.Frozen or State.Landed)
            .ToList();

        // Build a set of expected keys (callsign + ladder index) for visible flights only
        var expectedKeys = new HashSet<string>();

        // Update or create flight labels for current flights
        foreach (var flight in currentFlights)
        {
            // Determine reference time based on view configuration
            DateTimeOffset? referenceFlightTime = view.Reference switch
            {
                LadderReference.LandingTime => flight.LandingTime,
                LadderReference.FeederFixTime => flight.FeederFixTime,
                _ => null
            };

            if (!referenceFlightTime.HasValue)
            {
                // Can't display without reference time
                continue;
            }

            double yOffset = GetYOffsetForTime(referenceTime, referenceFlightTime.Value, view.TimeWindowMinutes);
            var yPosition = GetYPositionForOffset(yOffset, ladderHeight, view.Direction) + ladderYOffset;

            // Check if flight is visible
            var isVisible = yOffset >= 0 && yOffset < ladderHeight;

            var matchingLadders = GetMatchingLadders(flight);
            if (matchingLadders.Count == 0)
            {
                // Flight doesn't match any ladder
                continue;
            }

            var canMove = view.Reference == LadderReference.LandingTime;

            // Create/update label for each matching ladder
            foreach (var ladderIndex in matchingLadders)
            {
                var key = $"{flight.Callsign}_{ladderIndex}";

                // Skip creating/updating label if not visible in time window
                if (!isVisible)
                {
                    continue;
                }

                // Find the ladder element for this index
                var ladderElement = layout.VisualElements
                    .OfType<LadderElement>()
                    .FirstOrDefault(l => l.Index == ladderIndex);

                // Skip if ladder is scrolled out of view (no ladder element exists)
                if (ladderElement == null)
                    continue;

                // Only track labels that are both time-visible AND on visible ladders
                expectedKeys.Add(key);

                // Reuse existing flight label or create new one
                if (!_flightLabels.TryGetValue(key, out var flightLabel))
                {
                    var approachTypes = GetApproachTypes(flight);
                    // TODO: Grab the config from the View constructor or something
                    var labelConfiguration = Ioc.Default.GetRequiredService<LabelsConfiguration>();

                    // Create new flight label
                    var flightLabelViewModel = new FlightLabelViewModel(
                        Ioc.Default.GetRequiredService<IMediator>(),
                        Ioc.Default.GetRequiredService<IErrorReporter>(),
                        ViewModel,
                        labelConfiguration.GlobalColours,
                        flight,
                        ViewModel.Runways,
                        approachTypes,
                        _vatsysSelectedTrackCallsign);

                    flightLabelViewModel.UpdateLabelItems(labelLayout, flight, ladderIndex);

                    flightLabel = new FlightLabelView
                    {
                        DataContext = flightLabelViewModel,
                        Margin = new Thickness(2, 0, 2, 0),
                        IsDraggable = canMove
                    };

                    // Set up event handlers
                    flightLabel.MouseDoubleClick += (s, e) => OnFlightLabelDoubleClick(s, e, flight);
                    flightLabel.MouseLeftButtonDown += (s, e) => OnFlightLabelMouseDown(s, e, flight);
                    flightLabel.MouseMove += OnFlightLabelMouseMove;
                    flightLabel.MouseLeftButtonUp += OnFlightLabelMouseUp;
                    flightLabel.MouseRightButtonDown += (s, e) => OnFlightLabelRightClick(s, e, flight);
                    flightLabel.ContextMenuOpening += SuppressContextMenuIfRequired;

                    _flightLabels[key] = flightLabel;
                    LadderCanvas.Children.Add(flightLabel);

                    // Force layout update
                    flightLabel.UpdateLayout();
                }
                else
                {
                    // Update existing flight label
                    if (flightLabel.DataContext is FlightLabelViewModel existingViewModel)
                    {
                        existingViewModel.FlightViewModel = flight;
                        existingViewModel.IsSelected = ViewModel.SelectedFlight?.Callsign == flight.Callsign;
                        existingViewModel.UpdateLabelItems(labelLayout, flight, ladderIndex);
                    }

                    flightLabel.IsDraggable = canMove;
                    flightLabel.Visibility = Visibility.Visible;
                }

                // Position the flight label on the ladder
                // The ladder IS the area where labels are displayed
                Canvas.SetLeft(flightLabel, ladderElement.X);
                Canvas.SetTop(flightLabel, yPosition - flightLabel.ActualHeight / 2);
                flightLabel.Width = ladderElement.Width;
            }
        }

        // Remove flight labels that are no longer visible
        var keysToRemove = _flightLabels.Keys.Except(expectedKeys).ToList();
        foreach (var key in keysToRemove)
        {
            var flightLabel = _flightLabels[key];
            LadderCanvas.Children.Remove(flightLabel);
            _flightLabels.Remove(key);
        }
    }

    static string[] GetApproachTypes(FlightDto flight)
    {
        var airportConfigurationProvider = Ioc.Default.GetRequiredService<IAirportConfigurationProvider>();
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(flight.DestinationIdentifier);
        var results = new HashSet<string>();
        foreach (var trajectoryConfiguration in airportConfiguration.TerminalTrajectories)
        {
            results.Add(trajectoryConfiguration.ApproachType);
        }

        return results.ToArray();
    }

    void ClearLabels()
    {
        foreach (var flightLabel in _flightLabels.Values)
        {
            LadderCanvas.Children.Remove(flightLabel);
        }

        _flightLabels.Clear();
    }

    // Layout Helper Classes
    private abstract class VisualElement { }

    private class LadderElement : VisualElement
    {
        public required int Index { get; init; }
        public required double X { get; init; }
        public required double Width { get; init; }
        public required LadderConfiguration Config { get; init; }
    }

    private class TicksElement : VisualElement
    {
        public required double X { get; init; }
        public required double Width { get; init; }
        public required int LadderIndex { get; init; }
    }

    private class TimelineElement : VisualElement
    {
        public required double X { get; init; }
        public required double Width { get; init; }
        public required int LeftLadderIndex { get; init; }
        public required int RightLadderIndex { get; init; }
    }

    private class LayoutInfo
    {
        public required List<VisualElement> VisualElements { get; init; }
    }
}
