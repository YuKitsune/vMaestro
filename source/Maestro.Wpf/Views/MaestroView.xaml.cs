using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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
    const int MinuteHeight = 12;
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
        ClockText.Text = DateTimeOffset.UtcNow.ToString("HH:mm:ss");
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
            var now = DateTimeOffset.UtcNow;
            RedrawLadder(now);
            UpdateLabels(now);
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

    LadderPosition? GetLadderPositionFor(FlightMessage flight)
    {
        var searchTerm = ViewModel.SelectedView.ViewMode switch
        {
            ViewMode.Enroute => flight.FeederFixIdentifier,
            ViewMode.Approach => flight.AssignedRunway,
            _ => throw new ArgumentOutOfRangeException($"Unexpected LadderReferenceTime: {ViewModel.SelectedView.ViewMode}")
        };

        if (string.IsNullOrEmpty(searchTerm))
            return null;

        if (ViewModel.SelectedView.LeftLadder.Contains(searchTerm))
            return LadderPosition.Left;

        if (ViewModel.SelectedView.RightLadder.Contains(searchTerm))
            return LadderPosition.Right;

        return null;
    }

    void RedrawLadder(DateTimeOffset currentTime)
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

        var middlePoint = canvasWidth / 2;
        var ladderLeftPosition = middlePoint - LadderWidth / 2 - LineThickness;
        var ladderRightPosition = middlePoint + LadderWidth / 2 + LineThickness;

        var leftLine = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
        };

        leftLine.Loaded += PositionOnCanvas(
            top: 0,
            bottom: canvasHeight,
            x: ladderLeftPosition);

        LadderCanvas.Children.Add(leftLine);

        var rightLine = new BeveledLine
        {
            Orientation = Orientation.Vertical,
            Width = LineThickness,
            Height = canvasHeight,
            ClipToBounds = true,
            Flipped = true,
        };

        rightLine.Loaded += PositionOnCanvas(
            top: 0,
            bottom: canvasHeight,
            x: ladderRightPosition);

        LadderCanvas.Children.Add(rightLine);

        // Draw a tick for every minute
        var nextMinute = currentTime.Add(new TimeSpan(0, 0, 60 - currentTime.Second));
        var yOffset = GetYOffsetForTime(currentTime, nextMinute);
        while (yOffset <= canvasHeight)
        {
            var yPosition = canvasHeight - yOffset;

            var leftTickXPosition = ladderLeftPosition - LineThickness - TickWidth / 2;
            var rightTickXPosition = ladderRightPosition + LineThickness + TickWidth / 2;

            var leftTick = new BeveledLine
            {
                Orientation = Orientation.Horizontal,
                Height = LineThickness,
                Width = TickWidth
            };
            leftTick.Loaded += PositionOnCanvas(
                x: leftTickXPosition,
                y: yPosition);
            LadderCanvas.Children.Add(leftTick);

            var rightTick = new BeveledLine
            {
                Orientation = Orientation.Horizontal,
                Height = LineThickness,
                Width = TickWidth
            };

            rightTick.Loaded += PositionOnCanvas(
                x: rightTickXPosition,
                y: yPosition);
            LadderCanvas.Children.Add(rightTick);

            yOffset += MinuteHeight;
        }

        // At each 5-minute interval, draw text with 2-digit minutes
        var nextTime = GetNearest5Minutes(currentTime);
        yOffset = GetYOffsetForTime(currentTime, nextTime);
        while (yOffset <= canvasHeight)
        {
            var yPosition = canvasHeight - yOffset;
            var text = new TextBlock
            {
                Text = nextTime.Minute.ToString("00")
            };

            text.Loaded += PositionOnCanvas(
                x: middlePoint,
                y: yPosition);

            LadderCanvas.Children.Add(text);

            nextTime = nextTime.AddMinutes(5);
            yOffset += MinuteHeight * 5;
        }

        // Draw slots
        DrawSlots(currentTime);
    }

    void DrawSlots(DateTimeOffset currentTime)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;
        var ladderLeftPosition = middlePoint - LadderWidth / 2 - LineThickness;
        var ladderRightPosition = middlePoint + LadderWidth / 2 + LineThickness;

        foreach (var slot in ViewModel.Slots)
        {
            // Calculate slot position
            var startYOffset = GetYOffsetForTime(currentTime, slot.StartTime);
            var endYOffset = GetYOffsetForTime(currentTime, slot.EndTime);

            // Only draw slots that are visible on the timeline
            if (endYOffset < 0 || startYOffset > canvasHeight)
                continue;

            var startYPosition = canvasHeight - startYOffset;
            var endYPosition = canvasHeight - endYOffset;
            var slotHeight = Math.Abs(startYPosition - endYPosition);

            // Determine which side(s) of the ladder to draw the slot on
            var drawOnLeft = ShouldDrawSlotOnSide(slot.RunwayIdentifiers, ViewModel.SelectedView.LeftLadder);
            var drawOnRight = ShouldDrawSlotOnSide(slot.RunwayIdentifiers, ViewModel.SelectedView.RightLadder);

            var slotWidth = TickWidth;
            var topY = Math.Min(startYPosition, endYPosition);
            var rectXPosition = drawOnLeft
                ? ladderLeftPosition - LineThickness - slotWidth / 2
                : ladderRightPosition + LineThickness + slotWidth / 2;

            var rectangle = new System.Windows.Shapes.Rectangle
            {
                Width = slotWidth,
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
                x: rectXPosition,
                top: topY);

            LadderCanvas.Children.Add(rectangle);
        }

        return;

        bool ShouldDrawSlotOnSide(string[] slotRunways, string[] sideRunways)
        {
            // Check if all slot's runways are on this side of the ladder
            return slotRunways.All(sideRunways.Contains);
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

    double GetYOffsetForTime(DateTimeOffset currentTime, DateTimeOffset nextTime)
    {
        return (nextTime - currentTime).TotalMinutes * MinuteHeight;
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

    DateTimeOffset GetTimeForYOffset(DateTimeOffset currentTime, double yOffset)
    {
        // Convert the Y offset back to a time
        var minutes = yOffset / MinuteHeight;
        var newTime = currentTime.AddMinutes(minutes);
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
            var currentTime = DateTimeOffset.UtcNow;
            var clickTime = GetTimeForYOffset(currentTime, yOffset);

            // Determine which side of the ladder was clicked to get the correct runways
            var middlePoint = canvasWidth / 2;
            var filterItems = clickPosition.X < middlePoint
                ? ViewModel.SelectedView.LeftLadder
                : ViewModel.SelectedView.RightLadder;

            // Move the selected flight to the clicked position
            ViewModel.MoveFlightCommand.Execute(new MoveFlightRequest(
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

        if (!ViewModel.IsCreatingSlot)
        {
            // Store the right-click position so we can reference it later when creating slots
            _contextMenuPosition = e.GetPosition(LadderCanvas);
        }
        else
        {
            // End the current slot creation if the user right-clicks while inserting a slot
            var clickPosition = e.GetPosition(LadderCanvas);
            var canvasHeight = LadderCanvas.ActualHeight;
            var yOffset = canvasHeight - clickPosition.Y;
            var currentTime = DateTimeOffset.UtcNow;
            var clickTime = GetTimeForYOffset(currentTime, yOffset);
            ViewModel.EndSlotCreation(clickTime);

            // Prevent the context menu from opening as we're ending the slot creation
            _suppressContextMenu = true;
        }

        e.Handled = true;
    }

    void BeginInsertingSlotBefore(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem beforeMenuItem)
            return;

        if (beforeMenuItem.Parent is not MenuItem menuItem)
            return;

        BeginInsertingSlot(menuItem, SlotCreationReferencePoint.Before);
    }

    void BeginInsertingSlotAfter(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem afterMenuItem)
            return;

        if (afterMenuItem.Parent is not MenuItem menuItem)
            return;

        BeginInsertingSlot(menuItem, SlotCreationReferencePoint.After);
    }

    void BeginInsertingSlot(MenuItem menuItem, SlotCreationReferencePoint referencePoint)
    {
        var clickData = GetClickDataFor(menuItem);
        if (clickData is null)
            return;

        if (clickData.ViewMode == ViewMode.Enroute)
            return;

        ViewModel.BeginSlotCreation(
            clickData.ClickTime,
            referencePoint,
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

        if (clickData.ViewMode == ViewMode.Enroute)
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
        var currentTime = DateTimeOffset.UtcNow;
        var clickTime = GetTimeForYOffset(currentTime, yOffset);

        // Determine which side of the ladder was clicked to get the correct runways
        var canvasWidth = canvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        // If the user right-clicked on the left side, we insert a flight for the left ladder
        // If the user right-clicked on the right side, we insert a flight for the right ladder
        var filterItems = mousePosition.X < middlePoint
            ? ViewModel.SelectedView.LeftLadder
            : ViewModel.SelectedView.RightLadder;

        return new ClickData(
            clickTime,
            ViewModel.SelectedView.ViewMode,
            filterItems);
    }

    record ClickData(
        DateTimeOffset ClickTime,
        ViewMode ViewMode,
        string[] FilterItems);

    void OnFlightLabelMouseDown(object sender, MouseButtonEventArgs e, FlightMessage flight)
    {
        if (sender is not FlightLabelView flightLabel)
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

        if (_draggingFlightLabel != flightLabel)
            return;

        flightLabel.ReleaseMouseCapture();

        if (_hasMoved && _isDragging)
        {
            // This was a drag operation - move the flight
            var finalTop = Canvas.GetTop(flightLabel);
            var currentTime = DateTimeOffset.UtcNow;
            var canvasHeight = LadderCanvas.ActualHeight;
            var newYOffset = canvasHeight - finalTop;
            var newTime = GetTimeForYOffset(currentTime, newYOffset);

            // Get the flight from the flight label's data context
            if (flightLabel.DataContext is FlightLabelViewModel flightLabelViewModel)
            {
                var flight = flightLabelViewModel.FlightViewModel;
                var ladderPos = GetLadderPositionFor(flight);
                var filterItems = ladderPos switch
                {
                    LadderPosition.Left => ViewModel.SelectedView.LeftLadder,
                    LadderPosition.Right => ViewModel.SelectedView.RightLadder,
                    _ => []
                };

                ViewModel.MoveFlightCommand.Execute(new MoveFlightRequest(
                    ViewModel.AirportIdentifier,
                    flight.Callsign,
                    filterItems,
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

                // Check if there's already a selected flight
                if (ViewModel.SelectedFlight != null && ViewModel.SelectedFlight.Callsign != clickedFlight.Callsign)
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
                    DrawTimelineIfAllowed(); // Immediately update UI to reflect selection
                }
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
            e.Handled = true;
        }
    }

    void SuppressContextMenuIfRequired(object sender, ContextMenuEventArgs e)
    {
        if (!_suppressContextMenu)
            return;

        e.Handled = true;
        _suppressContextMenu = false;
    }

    void UpdateLabels(DateTimeOffset currentTime)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        var currentFlights = ViewModel.Flights
            .Where(f => f.State is State.Unstable or State.Stable or State.SuperStable or State.Frozen or State.Landed)
            .ToList();

        var currentCallsigns = new HashSet<string>(currentFlights.Select(f => f.Callsign));

        // Remove flight labels for flights that no longer exist
        var callsignsToRemove = _flightLabels.Keys.Except(currentCallsigns).ToList();
        foreach (var callsign in callsignsToRemove)
        {
            var flightLabel = _flightLabels[callsign];
            LadderCanvas.Children.Remove(flightLabel);
            _flightLabels.Remove(callsign);
        }

        // Update or create flight labels for current flights
        foreach (var flight in currentFlights)
        {
            double yOffset;
            switch (ViewModel.SelectedView.ViewMode)
            {
                case ViewMode.Enroute when flight.FeederFixTime.HasValue:
                    yOffset = GetYOffsetForTime(currentTime, flight.FeederFixTime.Value);
                    break;

                case ViewMode.Approach:
                    yOffset = GetYOffsetForTime(currentTime, flight.LandingTime);
                    break;

                // Aircraft without a feeder fix are not displayed on ladders in ENR mode (FF reference time)
                default:
                    continue;
            }

            var yPosition = canvasHeight - yOffset;

            if (yOffset < 0 || yOffset >= canvasHeight)
            {
                // Flight is off-screen, hide if it exists
                if (_flightLabels.TryGetValue(flight.Callsign, out var offScreenLabel))
                {
                    offScreenLabel.Visibility = Visibility.Collapsed;
                }
                continue;
            }

            const int distanceFromMiddle = (LadderWidth / 2) + LineThickness + TickWidth;
            var width = middlePoint - distanceFromMiddle - LineThickness;

            var ladderPosition = GetLadderPositionFor(flight);
            if (ladderPosition is null)
            {
                // Flight can't be positioned, hide if it exists
                if (_flightLabels.TryGetValue(flight.Callsign, out var unpositionedLabel))
                {
                    unpositionedLabel.Visibility = Visibility.Collapsed;
                }
                continue;
            }

            var canMove = ViewModel.SelectedView.ViewMode == ViewMode.Approach;

            // Reuse existing flight label or create new one
            if (!_flightLabels.TryGetValue(flight.Callsign, out var flightLabel))
            {
                // Create new flight label
                var flightLabelViewModel = new FlightLabelViewModel(
                    Ioc.Default.GetRequiredService<IMediator>(),
                    Ioc.Default.GetRequiredService<IErrorReporter>(),
                    ViewModel,
                    flight);

                flightLabel = new FlightLabelView
                {
                    DataContext = flightLabelViewModel,
                    Margin = new Thickness(2, 0, 2, 0),
                    Width = width,
                    IsDraggable = canMove
                };

                // Set up event handlers
                if (ViewModel.SelectedView.ViewMode == ViewMode.Enroute)
                {
                    flightLabel.MouseDoubleClick += (s, e) => OnFlightLabelDoubleClick(s, e, flight);
                }

                if (ViewModel.SelectedView.ViewMode == ViewMode.Approach)
                {
                    flightLabel.MouseLeftButtonDown += (s, e) => OnFlightLabelMouseDown(s, e, flight);
                    flightLabel.MouseMove += OnFlightLabelMouseMove;
                    flightLabel.MouseLeftButtonUp += OnFlightLabelMouseUp;
                }

                // Add right-click handler for both view modes to enable deselection
                flightLabel.MouseRightButtonDown += (s, e) => OnFlightLabelRightClick(s, e, flight);
                flightLabel.ContextMenuOpening += SuppressContextMenuIfRequired;

                _flightLabels[flight.Callsign] = flightLabel;
                LadderCanvas.Children.Add(flightLabel);
            }
            else
            {
                // Update existing flight label properties
                if (flightLabel.DataContext is FlightLabelViewModel existingViewModel)
                {
                    existingViewModel.FlightViewModel = flight;
                    existingViewModel.IsSelected = ViewModel.SelectedFlight?.Callsign == flight.Callsign;
                }

                flightLabel.IsDraggable = canMove;
                flightLabel.Visibility = Visibility.Visible;
            }

            // Update positioning properties
            flightLabel.LadderPosition = ladderPosition.Value;
            flightLabel.ViewMode = ViewModel.SelectedView.ViewMode;

            // Position the flight label on canvas
            switch (ladderPosition)
            {
                case LadderPosition.Left:
                    Canvas.SetLeft(flightLabel, 0);
                    Canvas.SetTop(flightLabel, yPosition - flightLabel.ActualHeight / 2);
                    flightLabel.ClearValue(Canvas.RightProperty);
                    break;

                case LadderPosition.Right:
                    Canvas.SetLeft(flightLabel, middlePoint + LadderWidth / 2 + LineThickness + TickWidth);
                    Canvas.SetTop(flightLabel, yPosition - flightLabel.ActualHeight / 2);
                    flightLabel.ClearValue(Canvas.RightProperty);
                    break;
            }
        }
    }

    void ClearLabels()
    {
        foreach (var flightLabel in _flightLabels.Values)
        {
            LadderCanvas.Children.Remove(flightLabel);
        }
        _flightLabels.Clear();
    }
}
