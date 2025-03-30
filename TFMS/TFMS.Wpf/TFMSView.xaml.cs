using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using TFMS.Wpf.Controls;

namespace TFMS.Wpf;

/// <summary>
/// Interaction logic for TFMSView.xaml
/// </summary>
public partial class TFMSView : UserControl
{
    const int MinuteHeight = 12;
    const int LadderWidth = 24;
    const int TickWidth = 8;
    const int LineThickness = 2;

    readonly DispatcherTimer dispatcherTimer;

    public TFMSView()
    {
        InitializeComponent();

        DataContext = Ioc.Default.GetRequiredService<TFMSViewModel>();

        dispatcherTimer = new DispatcherTimer();
        dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
        dispatcherTimer.Tick += TimerTick;
        dispatcherTimer.Start();

        Loaded += ControlLoaded;
        SizeChanged += OnSizeChanged;
        ViewModel.PropertyChanged += PropertyChanged;
    }

    public TFMSViewModel ViewModel => (TFMSViewModel)DataContext;

    void TimerTick(object sender, EventArgs args)
    {
        ClockText.Text = DateTimeOffset.UtcNow.ToString("HH:mm:ss");
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
        this.Dispatcher.Invoke(() =>
        {
            LadderCanvas.Children.Clear();

            var now = DateTimeOffset.Now;
            DrawSpine(now);
            DrawAircraft(now);
        });
    }

    void DrawAircraft(DateTimeOffset currentTime)
    {
        double canvasHeight = LadderCanvas.ActualHeight;
        double canvasWidth = LadderCanvas.ActualWidth;
        var middlePoint = canvasWidth / 2;

        foreach (var aircraft in ViewModel.Aircraft)
        {
            var yOffset = GetYOffsetForTime(currentTime, aircraft.LandingTime);

            var yPosition = canvasHeight - yOffset;

            if (yOffset < 0 || yOffset >= canvasHeight)
                continue;

            var aircraftView = new AircraftView
            {
                DataContext = aircraft
            };

            aircraftView.Loaded += OnAircraftLoaded;

            LadderCanvas.Children.Add(aircraftView);

            void OnAircraftLoaded(object _, RoutedEventArgs e)
            {
                Canvas.SetTop(aircraftView, yPosition - aircraftView.ActualHeight / 2);
                Canvas.SetLeft(aircraftView, 0);
                Canvas.SetRight(aircraftView, middlePoint - LadderWidth / 2 - TickWidth);
                aircraftView.Loaded -= OnAircraftLoaded;
            }
        }
    }

    void DrawSpine(DateTimeOffset currentTime)
    {
        double canvasHeight = LadderCanvas.ActualHeight;
        double canvasWidth = LadderCanvas.ActualWidth;

        var durationToDisplay = TimeSpan.FromMinutes(canvasHeight / MinuteHeight);
        var ladderCeilingTime = currentTime.Add(durationToDisplay);

        var middlePoint = canvasWidth / 2;
        var ladderLeftPosition = middlePoint - (LadderWidth / 2) - LineThickness;
        var ladderRightPosition = middlePoint + (LadderWidth / 2) + LineThickness;

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
        };

        rightLine.Loaded += PositionOnCanvas(
            top: 0,
            bottom: canvasHeight,
            x: ladderRightPosition);

        LadderCanvas.Children.Add(rightLine);

        var nextMinute = currentTime.Add(new TimeSpan(0, 0, 60 - currentTime.Second));
        var yOffset = GetYOffsetForTime(currentTime, nextMinute);
        while (yOffset <= canvasHeight)
        {
            var yPosition = canvasHeight - yOffset;
            var topPosition = yPosition - (LineThickness / 2);

            var leftTickXPosition = ladderLeftPosition - LineThickness - (TickWidth / 2);
            var rightTickXPosition = ladderRightPosition + LineThickness + (TickWidth / 2);

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

    RoutedEventHandler PositionOnCanvas(
        double? left = null,
        double? top = null,
        double? right = null,
        double? bottom = null,
        double? x = null,
        double? y = null)
    {
        return (object sender, RoutedEventArgs args) =>
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
                var left = x.Value - (element.ActualWidth / 2);
                Canvas.SetLeft(element, left);
            }

            if (y.HasValue)
            {
                var top = y.Value - (element.ActualHeight / 2);
                Canvas.SetTop(element, top);
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
        int minutes = (currentTime.Minute / 5) * 5;
        if (currentTime.Minute % 5 != 0 || currentTime.Second > 0 || currentTime.Millisecond > 0)
        {
            minutes += 5; // Add 5 minutes to round up
        }

        // If we added 5 minutes and it goes beyond 60 minutes, reset to 0 and increment the hour
        if (minutes >= 60)
        {
            minutes = 0;
            currentTime = currentTime.AddHours(1);
        }

        // Create the new DateTime with the rounded-up minute
        var nextInterval = new DateTimeOffset(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, minutes, 0, currentTime.Offset);
        return nextInterval;
    }
}
