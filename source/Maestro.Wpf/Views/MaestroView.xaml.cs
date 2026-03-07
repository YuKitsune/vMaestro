using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Controls;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;
using Point = System.Windows.Point;

namespace Maestro.Wpf.Views;

/// <summary>
/// Interaction logic for MaestroView.xaml
/// </summary>
public partial class MaestroView
{
    const int LadderWidth = 24;
    const int TickWidth = 8;
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
    }

    public MaestroViewModel ViewModel => (MaestroViewModel)DataContext;

    void TimerTick(object sender, EventArgs args)
    {
        ClockText.Text = ReferenceTime.ToString("HH:mm:ss");
        DrawTimelineIfAllowed();
    }

    void ControlLoaded(object sender, RoutedEventArgs e)
    {
        DrawTimeline();
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawTimeline();
    }

    void DrawTimeline()
    {
        if (_isDragging) return;
        Dispatcher.Invoke(() =>
        {
            RedrawLadder(ReferenceTime);
            UpdateLabels(ReferenceTime);
            InvalidateVisual();
        });
    }

    void DrawTimelineIfAllowed()
    {
        // Don't redraw during confirmation dialogs
        if (ViewModel.IsConfirmationDialogOpen)
            return;

        DrawTimeline();
    }

    List<int> GetMatchingLadders(FlightMessage flight)
    {
        var view = (ViewConfigurationV2)ViewModel.SelectedView;
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

    LayoutInfo CalculateLadderLayout(ViewConfigurationV2 view, LabelLayoutConfigurationV2 labelLayout, double canvasWidth)
    {
        // Calculate character width by measuring '_'
        double characterWidth = MeasureCharacterWidth("_");

        // Calculate ladder width from label items (character count * character width)
        int totalCharacters = labelLayout.Items.Sum(item => item.Width + item.Padding);
        double ladderWidth = totalCharacters * characterWidth;

        const double tickWidth = 8;
        const double timelineWidth = 16;

        // Build visual elements in order: Ladder -> Ticks -> Timeline -> Ticks -> Ladder -> ...
        var elements = new List<VisualElement>();
        double xPos = 0;

        for (int i = 0; i < view.Ladders.Length; i++)
        {
            // Add ladder
            elements.Add(new LadderElement
            {
                Index = i,
                X = xPos,
                Width = ladderWidth,
                Config = view.Ladders[i]
            });
            xPos += ladderWidth;

            // Add ticks to right of ladder
            elements.Add(new TicksElement
            {
                X = xPos,
                Width = tickWidth,
                LadderIndex = i
            });
            xPos += tickWidth;

            // Add timeline (shared between this ladder and next)
            // Only add if this is an even-indexed ladder OR the last ladder
            if (i % 2 == 0 || i == view.Ladders.Length - 1)
            {
                elements.Add(new TimelineElement
                {
                    X = xPos,
                    Width = timelineWidth,
                    LeftLadderIndex = i,
                    RightLadderIndex = i + 1 < view.Ladders.Length ? i + 1 : -1
                });
                xPos += timelineWidth;

                // Add ticks to right of timeline if there's a next ladder
                if (i + 1 < view.Ladders.Length)
                {
                    elements.Add(new TicksElement
                    {
                        X = xPos,
                        Width = tickWidth,
                        LadderIndex = i + 1
                    });
                    xPos += tickWidth;
                }
            }
        }

        return new LayoutInfo { VisualElements = elements };
    }

    void RedrawLadder(DateTimeOffset referenceTime)
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

        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;

        // Prevent rendering when canvas is too small to avoid infinite loop hangs
        const double minCanvasSize = 10.0;
        if (canvasHeight < minCanvasSize || canvasWidth < minCanvasSize)
        {
            return;
        }

        // Get current view and label layout
        var view = ViewModel.SelectedView;
        var labelLayout = ViewModel.GetLabelLayout(view);

        if (labelLayout == null)
        {
            // TODO: Display an error
            return;
        }

        // Calculate V2 layout
        var layout = CalculateLadderLayout(view, labelLayout, canvasWidth);

        // Draw all visual elements
        foreach (var element in layout.VisualElements)
        {
            switch (element)
            {
                case LadderElement ladder:
                    DrawLadderBorder(ladder, canvasHeight);
                    break;
                case TicksElement ticks:
                    DrawTicks(ticks, canvasHeight, referenceTime, view.TimeWindowMinutes);
                    break;
                case TimelineElement timeline:
                    DrawTimeline(timeline, canvasHeight, referenceTime, view.TimeWindowMinutes);
                    DrawTimeReferenceBox(timeline, referenceTime);
                    DrawLadderFooter(timeline, view);
                    break;
            }
        }

        // Draw slots
        DrawSlotsV2(referenceTime, view, layout);
    }

    // V2 Drawing Methods
    void DrawLadderBorder(LadderElement ladder, double canvasHeight)
    {
        var line = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
        };

        line.Loaded += PositionOnCanvas(
            top: 0,
            bottom: canvasHeight,
            x: ladder.X + ladder.Width / 2);

        LadderCanvas.Children.Add(line);
    }

    void DrawTicks(TicksElement ticks, double canvasHeight, DateTimeOffset referenceTime, int timeWindowMinutes)
    {
        var view = (ViewConfigurationV2)ViewModel.SelectedView;

        // Draw a tick for every minute
        var nextMinute = referenceTime.Add(new TimeSpan(0, 0, 60 - referenceTime.Second));
        var yOffset = GetYOffsetForTime(referenceTime, nextMinute, timeWindowMinutes);

        while (yOffset <= canvasHeight)
        {
            var yPosition = GetYPositionForOffset(yOffset, canvasHeight, view.Direction);

            var tick = new BeveledLine
            {
                Orientation = Orientation.Horizontal,
                Height = LineThickness,
                Width = ticks.Width
            };

            tick.Loaded += PositionOnCanvas(
                x: ticks.X + ticks.Width / 2,
                y: yPosition);

            LadderCanvas.Children.Add(tick);

            yOffset += MinuteHeight(timeWindowMinutes);
        }
    }

    void DrawTimeline(TimelineElement timeline, double canvasHeight, DateTimeOffset referenceTime, int timeWindowMinutes)
    {
        var view = (ViewConfigurationV2)ViewModel.SelectedView;

        // Draw left border
        var leftBorder = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
        };

        leftBorder.Loaded += PositionOnCanvas(
            top: 0,
            bottom: canvasHeight,
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
            top: 0,
            bottom: canvasHeight,
            x: timeline.X + timeline.Width);

        LadderCanvas.Children.Add(rightBorder);

        // Draw 5-minute markers
        var nextTime = GetNearest5Minutes(referenceTime);
        var yOffset = GetYOffsetForTime(referenceTime, nextTime, timeWindowMinutes);

        while (yOffset <= canvasHeight)
        {
            var yPosition = GetYPositionForOffset(yOffset, canvasHeight, view.Direction);
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

    void DrawTimeReferenceBox(TimelineElement timeline, DateTimeOffset referenceTime)
    {
        // Draw reference time at bottom of timeline
        var refTimeText = new TextBlock
        {
            Text = referenceTime.ToString("HH:mm:ss"),
            Background = Theme.BackgroundColor
        };

        refTimeText.Loaded += PositionOnCanvas(
            x: timeline.X + timeline.Width / 2,
            bottom: 0);

        LadderCanvas.Children.Add(refTimeText);
    }

    void DrawLadderFooter(TimelineElement timeline, ViewConfigurationV2 view)
    {
        // Draw info for both left and right ladders if they exist
        if (timeline.LeftLadderIndex >= 0 && timeline.LeftLadderIndex < view.Ladders.Length)
        {
            DrawLadderInfo(timeline.LeftLadderIndex, view, timeline, isLeftLadder: true);
        }

        if (timeline.RightLadderIndex >= 0 && timeline.RightLadderIndex < view.Ladders.Length)
        {
            DrawLadderInfo(timeline.RightLadderIndex, view, timeline, isLeftLadder: false);
        }
    }

    void DrawLadderInfo(int ladderIndex, ViewConfigurationV2 view, TimelineElement timeline, bool isLeftLadder)
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
            // Filter text on LEFT of ref box, right-aligned
            double textX = refBoxCenter - (refBoxWidth / 2) - 10;

            var runwayText = new TextBlock
            {
                Text = runways,
                TextAlignment = TextAlignment.Right
            };
            runwayText.Loaded += PositionOnCanvas(
                right: LadderCanvas.ActualWidth - textX,
                bottom: 30);
            LadderCanvas.Children.Add(runwayText);

            var feederText = new TextBlock
            {
                Text = feeders,
                TextAlignment = TextAlignment.Right
            };
            feederText.Loaded += PositionOnCanvas(
                right: LadderCanvas.ActualWidth - textX,
                bottom: 15);
            LadderCanvas.Children.Add(feederText);
        }
        else
        {
            // Filter text on RIGHT of ref box, left-aligned
            double textX = refBoxCenter + (refBoxWidth / 2) + 10;

            var runwayText = new TextBlock
            {
                Text = runways,
                TextAlignment = TextAlignment.Left
            };
            runwayText.Loaded += PositionOnCanvas(
                left: textX,
                bottom: 30);
            LadderCanvas.Children.Add(runwayText);

            var feederText = new TextBlock
            {
                Text = feeders,
                TextAlignment = TextAlignment.Left
            };
            feederText.Loaded += PositionOnCanvas(
                left: textX,
                bottom: 15);
            LadderCanvas.Children.Add(feederText);
        }
    }

    void DrawSlotsV2(DateTimeOffset currentTime, ViewConfigurationV2 view, LayoutInfo layout)
    {
        // Slots are only drawn when there's a LandingTime reference
        if (view.Reference != LadderReference.LandingTime)
            return;

        var canvasHeight = LadderCanvas.ActualHeight;

        foreach (var slot in ViewModel.Slots)
        {
            // Calculate slot position
            var startYOffset = GetYOffsetForTime(currentTime, slot.StartTime, view.TimeWindowMinutes);
            var endYOffset = GetYOffsetForTime(currentTime, slot.EndTime, view.TimeWindowMinutes);

            // Only draw slots that are visible on the timeline
            if (endYOffset < 0 || startYOffset > canvasHeight)
                continue;

            var startYPosition = GetYPositionForOffset(startYOffset, canvasHeight, view.Direction);
            var endYPosition = GetYPositionForOffset(endYOffset, canvasHeight, view.Direction);
            var slotHeight = Math.Abs(startYPosition - endYPosition);
            var topY = Math.Min(startYPosition, endYPosition);

            // Find which ladders this slot should be drawn on
            for (int i = 0; i < view.Ladders.Length; i++)
            {
                var ladder = view.Ladders[i];

                // Check if slot's runways match this ladder's filter
                bool shouldDraw = slot.RunwayIdentifiers.All(slotRunway =>
                    ladder.Runways.Length == 0 || ladder.Runways.Contains(slotRunway));

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

    double GetYOffsetForTime(DateTimeOffset currentTime, DateTimeOffset nextTime)
    {
        var view = (ViewConfigurationV2)ViewModel.SelectedView;
        return GetYOffsetForTime(currentTime, nextTime, view.TimeWindowMinutes);
    }

    double GetYPositionForOffset(double yOffset, double canvasHeight, LadderDirection direction)
    {
        return direction switch
        {
            LadderDirection.Up => canvasHeight - yOffset,
            LadderDirection.Down => yOffset,
            _ => canvasHeight - yOffset
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

    void OnCanvasLeftClick(object sender, MouseButtonEventArgs e)
    {
        // If there's a selected flight, move it to the clicked position
        if (ViewModel.SelectedFlight != null)
        {
            var clickPosition = e.GetPosition(LadderCanvas);
            var canvasHeight = LadderCanvas.ActualHeight;
            var canvasWidth = LadderCanvas.ActualWidth;
            var yOffset = canvasHeight - clickPosition.Y;
            var clickTime = GetTimeForYOffset(ReferenceTime, yOffset);

            // Get the clicked ladder's runway filters
            var view = ViewModel.SelectedView;
            var labelLayout = ViewModel.GetLabelLayout(view);
            var layout = CalculateLadderLayout(view, labelLayout!, canvasWidth);

            var clickedLadder = GetClickedLadder(clickPosition.X, layout);
            var filterItems = clickedLadder != null && clickedLadder.Index < view.Ladders.Length
                ? view.Ladders[clickedLadder.Index].Runways
                : Array.Empty<string>();

            // Move the selected flight to the clicked position
            ViewModel.MoveFlightWithoutConfirmationCommand.Execute(new MoveFlightRequest(
                ViewModel.AirportIdentifier,
                ViewModel.SelectedFlight.Callsign,
                filterItems,
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
            DrawTimelineIfAllowed(); // Immediately update UI to reflect deselection
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
        var canvasHeight = canvas.ActualHeight;
        var yOffset = canvasHeight - mousePosition.Y;
        var clickTime = GetTimeForYOffset(ReferenceTime, yOffset);

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

    void OnFlightLabelMouseDown(object sender, MouseButtonEventArgs e, FlightMessage flight)
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
            DrawTimelineIfAllowed(); // Immediately update UI to reflect selection
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
            var canvasHeight = LadderCanvas.ActualHeight;
            var newYOffset = canvasHeight - finalTop - (flightLabel.ActualHeight/2);
            var newTime = GetTimeForYOffset(ReferenceTime, newYOffset);

            // Get the flight from the flight label's data context
            if (flightLabel.DataContext is FlightLabelViewModel flightLabelViewModel)
            {
                var flight = flightLabelViewModel.FlightViewModel;
                var view = ViewModel.SelectedView;

                // Find which ladder this flight label belongs to by looking up in the dictionary
                var ladderIndex = -1;
                foreach (var kvp in _flightLabels)
                {
                    if (kvp.Value == flightLabel)
                    {
                        // Key format is "{callsign}_{ladderIndex}"
                        var parts = kvp.Key.Split('_');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var index))
                        {
                            ladderIndex = index;
                            break;
                        }
                    }
                }

                // Get runway filters from the ladder configuration
                var runwayFilters = ladderIndex >= 0 && ladderIndex < view.Ladders.Length
                    ? view.Ladders[ladderIndex].Runways
                    : Array.Empty<string>();

                ViewModel.MoveFlightWithConfirmationCommand.Execute(new MoveFlightRequest(
                    ViewModel.AirportIdentifier,
                    flight.Callsign,
                    runwayFilters,
                    newTime
                ));

                DrawTimeline();
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

                DrawTimelineIfAllowed(); // Immediately update UI to reflect selection
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

    void OnFlightLabelDoubleClick(object sender, MouseButtonEventArgs e, FlightMessage flight)
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

    void OnFlightLabelRightClick(object sender, MouseButtonEventArgs e, FlightMessage flight)
    {
        if (sender is not FlightLabelView flightLabel)
            return;

        // Right-clicking a flight label deselects any selected flight and prevents context menu
        if (ViewModel.SelectedFlight != null)
        {
            ViewModel.DeselectFlight();
            _suppressContextMenu = true; // Flag to suppress context menu
            DrawTimelineIfAllowed(); // Immediately update UI to reflect deselection
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
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;

        // Prevent rendering when canvas is too small
        const double minCanvasSize = 10.0;
        if (canvasHeight < minCanvasSize || canvasWidth < minCanvasSize)
        {
            return;
        }

        var view = (ViewConfigurationV2)ViewModel.SelectedView;
        var labelLayout = ViewModel.GetLabelLayout(view);

        if (labelLayout == null)
        {
            // Fall back to V1 if no label layout
            return;
        }

        var layout = CalculateLadderLayout(view, labelLayout, canvasWidth);

        var currentFlights = ViewModel.Flights
            .Where(f => f.State is State.Unstable or State.Stable or State.SuperStable or State.Frozen or State.Landed)
            .ToList();

        // Build a set of expected keys (callsign + ladder index)
        var expectedKeys = new HashSet<string>();
        foreach (var flight in currentFlights)
        {
            var matchingLadders = GetMatchingLadders(flight);
            foreach (var ladderIndex in matchingLadders)
            {
                expectedKeys.Add($"{flight.Callsign}_{ladderIndex}");
            }
        }

        // Remove flight labels that no longer exist
        var keysToRemove = _flightLabels.Keys.Except(expectedKeys).ToList();
        foreach (var key in keysToRemove)
        {
            var flightLabel = _flightLabels[key];
            LadderCanvas.Children.Remove(flightLabel);
            _flightLabels.Remove(key);
        }

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
            var yPosition = GetYPositionForOffset(yOffset, canvasHeight, view.Direction);

            if (yOffset < 0 || yOffset >= canvasHeight)
            {
                // Flight is off-screen
                continue;
            }

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

                // Find the ladder element for this index
                var ladderElement = layout.VisualElements
                    .OfType<LadderElement>()
                    .FirstOrDefault(l => l.Index == ladderIndex);

                if (ladderElement == null)
                    continue;

                // Reuse existing flight label or create new one
                if (!_flightLabels.TryGetValue(key, out var flightLabel))
                {
                    var approachTypeLookups = GetApproachTypeLookups(flight);

                    // Create new flight label
                    var flightLabelViewModel = new FlightLabelViewModel(
                        Ioc.Default.GetRequiredService<IMediator>(),
                        Ioc.Default.GetRequiredService<IErrorReporter>(),
                        ViewModel,
                        flight,
                        ViewModel.Runways,
                        approachTypeLookups);

                    // Update label items for V2
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
                // Labels on even-indexed ladders (0, 2, 4...) are right-aligned to the ladder
                // Labels on odd-indexed ladders (1, 3, 5...) are left-aligned to the ladder
                if (ladderIndex % 2 == 0)
                {
                    // Even ladder: right-align to left edge of ladder
                    Canvas.SetLeft(flightLabel, ladderElement.X - ladderElement.Width);
                    Canvas.SetTop(flightLabel, yPosition - flightLabel.ActualHeight / 2);
                }
                else
                {
                    // Odd ladder: left-align to right edge of ladder
                    Canvas.SetLeft(flightLabel, ladderElement.X + ladderElement.Width);
                    Canvas.SetTop(flightLabel, yPosition - flightLabel.ActualHeight / 2);
                }

                flightLabel.Width = ladderElement.Width;
            }
        }
    }

    static ApproachTypeLookup[] GetApproachTypeLookups(FlightMessage flight)
    {
        var airportConfigurationProvider = Ioc.Default.GetRequiredService<IAirportConfigurationProviderV2>();
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(flight.DestinationIdentifier);
        var results = new HashSet<ApproachTypeLookup>();
        foreach (var arrivalConfiguration in airportConfiguration.Trajectories)
        {
            results.Add(new ApproachTypeLookup(arrivalConfiguration.FeederFix, arrivalConfiguration.RunwayIdentifier, arrivalConfiguration.ApproachType));
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

    // V2 Layout Helper Classes
    private abstract class VisualElement { }

    private class LadderElement : VisualElement
    {
        public required int Index { get; init; }
        public required double X { get; init; }
        public required double Width { get; init; }
        public required LadderConfigurationV2 Config { get; init; }
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
