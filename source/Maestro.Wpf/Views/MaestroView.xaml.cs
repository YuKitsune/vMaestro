using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Wpf.Controls;
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
    private bool _isDragging;

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
            LadderCanvas.Children.Clear();

            var now = DateTimeOffset.UtcNow;
            DrawLadder(now);
            DrawAircraft(now);
            InvalidateVisual();
        });
    }

    void DrawAircraft(DateTimeOffset currentTime)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        if (ViewModel.SelectedSequence is null)
            return;

        foreach (var slot in ViewModel.SelectedSequence.Slots)
        {
            var flight = slot.Flight;
            if (flight is null)
                continue;

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

            // BUG: When dragging, the label moves down slightly.
            // BUG: The position the label moves into doesn't correspond with the slot times.

            flightLabel.DragStarted += (_, _) =>
            {
                _isDragging = true;
            };

            flightLabel.GetSnappedY += (label, targetY) => GetSnappedYPosition(currentTime, targetY, flight.AssignedRunway, canvasHeight) - label.ActualHeight / 2;

            flightLabel.DragEnded += (_, newY) =>
            {
                _isDragging = false;
                var newYOffset = canvasHeight - newY;
                var nearestSlotIdentifier = GetNearestSlotIdentifier(currentTime, newYOffset, flight.AssignedRunway);
                if (nearestSlotIdentifier != null)
                {
                    ViewModel.MoveFlightCommand.Execute(new MoveFlightRequest(
                        ViewModel.SelectedSequence.AirportIdentifier,
                        flight.Callsign,
                        nearestSlotIdentifier,
                        flight.AssignedRunway ?? ""
                    ));
                }
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
        for (var yOffset = GetYOffsetForTime(currentTime, nextMinute); yOffset <= canvasHeight; yOffset += MinuteHeight)
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
        }

        // At each 5-minute interval, draw text with 2-digit minutes
        var nextTime = GetNearest5Minutes(currentTime);
        for (var yOffset = GetYOffsetForTime(currentTime, nextTime); yOffset <= canvasHeight; yOffset += MinuteHeight * 5)
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

    double GetSnappedYPosition(DateTimeOffset currentTime, double targetY, string runwayIdentifier, double canvasHeight)
    {
        if (ViewModel.SelectedSequence?.Slots == null)
            return targetY;

        // Convert the target Y position to Y offset (distance from bottom)
        var yOffset = canvasHeight - targetY;

        // Convert Y offset to target time
        var targetTime = GetTimeForYOffset(currentTime, yOffset);

        // Get slots for the specified runway, ordered by time
        var runwaySlots = ViewModel.SelectedSequence.Slots
            .Where(slot => slot.RunwayIdentifier == runwayIdentifier)
            .OrderBy(slot => slot.Time)
            .ToList();

        if (!runwaySlots.Any())
            return targetY;

        // Find the closest slot by time
        var closestSlot = runwaySlots
            .OrderBy(slot => Math.Abs((slot.Time - targetTime).TotalMinutes))
            .First();

        // Convert the closest slot's time back to Y position
        var snappedYOffset = GetYOffsetForTime(currentTime, closestSlot.Time);
        var snappedY = canvasHeight - snappedYOffset;

        return snappedY;
    }

    string? GetNearestSlotIdentifier(DateTimeOffset currentTime, double yOffset, string runwayIdentifier)
    {
        if (ViewModel.SelectedSequence?.Slots == null)
            return null;

        // Convert Y offset to target time
        var targetTime = GetTimeForYOffset(currentTime, yOffset);

        // Get slots for the specified runway, ordered by time
        var runwaySlots = ViewModel.SelectedSequence.Slots
            .Where(slot => slot.RunwayIdentifier == runwayIdentifier)
            .OrderBy(slot => slot.Time)
            .ToList();

        if (!runwaySlots.Any())
            return null;

        // Find the closest slot by time
        var closestSlot = runwaySlots
            .OrderBy(slot => Math.Abs((slot.Time - targetTime).TotalMinutes))
            .First();

        return closestSlot.Identifier;
    }
}
