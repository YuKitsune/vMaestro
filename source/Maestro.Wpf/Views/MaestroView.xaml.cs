using System.ComponentModel;
using System.Drawing;
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
    private System.Windows.Point? _contextMenuPosition;
    private bool _suppressContextMenu = false;

    public MaestroView()
    {
        DataContext = Ioc.Default.GetRequiredService<MaestroViewModel>();

        InitializeComponent();

        _dispatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _dispatcherTimer.Tick += TimerTick;
        _dispatcherTimer.Start();

        Loaded += ControlLoaded;
        SizeChanged += OnSizeChanged;
        ViewModel.PropertyChanged += PropertyChanged;
    }

    public MaestroViewModel ViewModel => (MaestroViewModel)DataContext;

    void TimerTick(object sender, EventArgs args)
    {
        ClockText.Text = DateTimeOffset.UtcNow.ToString("HH:mm:ss");
        DrawLadder();
    }

    void ControlLoaded(object sender, RoutedEventArgs e)
    {
        DrawLadder();
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawLadder();
    }

    void PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        DrawLadder();
    }

    void DrawLadder()
    {
        if (_isDragging) return;
        Dispatcher.Invoke(() =>
        {
            // BUG: If a context menu is open for a flight label, the border will disappear when the ladder is redrawn
            // TODO: Instead of deleting everything and re-drawing, we should just move existing elements
            LadderCanvas.Children.Clear();

            var now = DateTimeOffset.UtcNow;
            DrawLadder(now);
            DrawFlights(now);
            InvalidateVisual();
        });
    }

    void DrawFlights(DateTimeOffset currentTime)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        if (ViewModel.SelectedSequence is null)
            return;

        foreach (var flight in ViewModel.SelectedSequence.Flights.Where(f => f.State is State.Unstable or State.Stable or State.SuperStable or State.Frozen or State.Landed))
        {
            double yOffset;
            switch (ViewModel.SelectedSequence.SelectedView.ViewMode)
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
                continue;

            const int distanceFromMiddle = (LadderWidth / 2) + (LineThickness * 2) + TickWidth;
            var width = middlePoint - distanceFromMiddle;

            var ladderPosition = GetLadderPositionFor(flight);
            if (ladderPosition is null)
                continue;

            var canDrag = ViewModel.SelectedSequence.SelectedView.ViewMode == ViewMode.Approach;
            var flightLabel = new FlightLabelView
            {
                DataContext = new FlightLabelViewModel(
                    Ioc.Default.GetRequiredService<IMediator>(),
                    Ioc.Default.GetRequiredService<IErrorReporter>(),
                    ViewModel.SelectedSequence,
                    flight,
                    ViewModel.SelectedSequence.CurrentRunwayMode),
                Width = width,
                Margin = new Thickness(2,0,2,0),
                LadderPosition = ladderPosition.Value,
                ViewMode = ViewModel.SelectedSequence.SelectedView.ViewMode,
                IsDraggable = canDrag
            };

            flightLabel.Loaded += ladderPosition switch
            {
                LadderPosition.Left => PositionOnCanvas(
                    left: 0,
                    right: middlePoint - distanceFromMiddle,
                    y: yPosition),
                LadderPosition.Right => PositionOnCanvas(
                    left: middlePoint + distanceFromMiddle,
                    right: canvasWidth,
                    y: yPosition),
                _ => throw new ArgumentException($"Unexpected LadderPosition: {ladderPosition}")
            };

            flightLabel.DragStarted += (s, e) =>
            {
                _isDragging = true;
            };

            flightLabel.DragEnded += (s, newY) =>
            {
                _isDragging = false;
                var newYOffset = canvasHeight - newY;
                var newTime = GetTimeForYOffset(currentTime, newYOffset);

                var ladderPos = GetLadderPositionFor(flight);
                var filterItems = ladderPos switch
                {
                    LadderPosition.Left => ViewModel.SelectedSequence.SelectedView.LeftLadder,
                    LadderPosition.Right => ViewModel.SelectedSequence.SelectedView.RightLadder,
                    _ => []
                };

                ViewModel.MoveFlightCommand.Execute(new MoveFlightRequest(
                    ViewModel.SelectedSequence.AirportIdentifier,
                    flight.Callsign,
                    filterItems,
                    newTime
                ));

                DrawLadder();
            };

            LadderCanvas.Children.Add(flightLabel);
        }
    }

    LadderPosition? GetLadderPositionFor(FlightMessage flight)
    {
        if (ViewModel.SelectedSequence.SelectedView is null)
            return null;

        var searchTerm = ViewModel.SelectedSequence.SelectedView.ViewMode switch
        {
            ViewMode.Enroute => flight.FeederFixIdentifier,
            ViewMode.Approach => flight.AssignedRunway,
            _ => throw new ArgumentOutOfRangeException($"Unexpected LadderReferenceTime: {ViewModel.SelectedSequence.SelectedView.ViewMode}")
        };

        if (string.IsNullOrEmpty(searchTerm))
            return null;

        if (ViewModel.SelectedSequence.SelectedView.LeftLadder.Contains(searchTerm))
            return LadderPosition.Left;

        if (ViewModel.SelectedSequence.SelectedView.RightLadder.Contains(searchTerm))
            return LadderPosition.Right;

        return null;
    }

    void DrawLadder(DateTimeOffset currentTime)
    {
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
        if (ViewModel.SelectedSequence?.Slots == null)
            return;

        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;
        var ladderLeftPosition = middlePoint - LadderWidth / 2 - LineThickness;
        var ladderRightPosition = middlePoint + LadderWidth / 2 + LineThickness;

        foreach (var slot in ViewModel.SelectedSequence.Slots)
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
            var drawOnLeft = ShouldDrawSlotOnSide(slot.RunwayIdentifiers, ViewModel.SelectedSequence.SelectedView.LeftLadder);
            var drawOnRight = ShouldDrawSlotOnSide(slot.RunwayIdentifiers, ViewModel.SelectedSequence.SelectedView.RightLadder);

            var slotWidth = TickWidth + 4; // Slightly wider than tick marks
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

    void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
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

    void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_suppressContextMenu)
            return;

        // Prevent the menu from opening during slot creation
        e.Handled = true;

        // Reset the flag for next time
        _suppressContextMenu = false;
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

        // Determine which side was clicked and get corresponding runway identifiers
        if (ViewModel.SelectedSequence?.SelectedView == null)
            return null;

        // Determine which side of the ladder was clicked to get the correct runways
        var canvasWidth = canvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        // If the user right-clicked on the left side, we insert a flight for the left ladder
        // If the user right-clicked on the right side, we insert a flight for the right ladder
        var filterItems = mousePosition.X < middlePoint
            ? ViewModel.SelectedSequence.SelectedView.LeftLadder
            : ViewModel.SelectedSequence.SelectedView.RightLadder;

        return new ClickData(
            clickTime,
            ViewModel.SelectedSequence.SelectedView.ViewMode,
            filterItems);
    }

    record ClickData(
        DateTimeOffset ClickTime,
        ViewMode ViewMode,
        string[] FilterItems);
}
