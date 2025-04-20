using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Core.Configuration;
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
        InvalidateVisual();
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
        Dispatcher.Invoke(() =>
        {
            LadderCanvas.Children.Clear();

            var now = DateTimeOffset.UtcNow;
            DrawLadder(now);
            DrawAircraft(now);
        });
    }

    void DrawAircraft(DateTimeOffset currentTime)
    {
        var canvasHeight = LadderCanvas.ActualHeight;
        var canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        if (ViewModel.SelectedRunwayMode is null || ViewModel.SelectedView is null)
            return;

        foreach (var aircraft in ViewModel.Aircraft)
        {
            double yOffset;
            switch (ViewModel.SelectedView.LadderReferenceTime)
            {
                case LadderReferenceTime.FeederFixTime when aircraft.ScheduledFeederFixTime.HasValue:
                    yOffset = GetYOffsetForTime(currentTime, aircraft.ScheduledFeederFixTime.Value);
                    break;
                
                case LadderReferenceTime.LandingTime:
                    yOffset = GetYOffsetForTime(currentTime, aircraft.ScheduledLandingTime);
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

            var ladderPosition = GetLadderPositionFor(aircraft);

            var flightLabel = new FlightLabelView
            {
                DataContext = new FlightLabelViewModel(
                    Ioc.Default.GetRequiredService<IMediator>(),
                    aircraft,
                    ViewModel.SelectedRunwayMode),
                Width = width,
                Margin = new Thickness(2,0,2,0),
                LadderPosition = ladderPosition
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

            LadderCanvas.Children.Add(flightLabel);
        }
    }

    LadderPosition GetLadderPositionFor(FlightViewModel flight)
    {
        if (ViewModel.SelectedView?.LeftLadderConfiguration is not null && ShowOnLadder(ViewModel.SelectedView.LeftLadderConfiguration))
        {
            return LadderPosition.Left;
        }
        
        if (ViewModel.SelectedView?.RightLadderConfiguration is not null && ShowOnLadder(ViewModel.SelectedView.RightLadderConfiguration))
        {
            return LadderPosition.Right;
        }

        // TODO: Log a warning if we've made it to this point
        throw new Exception($"Flight {flight.Callsign} could not be positioned on the ladder");

        bool ShowOnLadder(LadderConfiguration ladderConfiguration)
        {
            var runwayMatches = ladderConfiguration.Runways is null ||
                                !ladderConfiguration.Runways.Any() ||
                                string.IsNullOrEmpty(flight.AssignedRunway) ||
                                ladderConfiguration.Runways.Contains(flight.AssignedRunway);
            
            var feederFixMatches = ladderConfiguration.FeederFixes is null ||
                                   !ladderConfiguration.FeederFixes.Any() ||
                                   string.IsNullOrEmpty(flight.FeederFixIdentifier) ||
                                   ladderConfiguration.FeederFixes.Contains(flight.FeederFixIdentifier);

            return runwayMatches && feederFixMatches;
        }
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
}
