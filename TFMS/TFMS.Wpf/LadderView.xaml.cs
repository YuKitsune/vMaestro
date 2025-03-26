using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace TFMS.Wpf
{
    /// <summary>
    /// Interaction logic for LadderView.xaml
    /// </summary>
    public partial class LadderView : UserControl
    {
        const int MinuteHeight = 10;
        const int SpineWidth = 16;
        const int TickWidth = 8;

        public LadderView()
        {
            InitializeComponent();
            DataContext = Ioc.Default.GetRequiredService<LadderViewModel>();

            Loaded += ControlLoaded;
            SizeChanged += OnSizeChanged;
            ViewModel.PropertyChanged += PropertyChanged;
        }

        public LadderViewModel ViewModel => (LadderViewModel)DataContext;

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
                    Canvas.SetRight(aircraftView, middlePoint - SpineWidth/2 - TickWidth);
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

            var leftLine = new Line()
            {
                Y1 = 0,
                X1 = middlePoint - SpineWidth / 2,
                Y2 = canvasHeight,
                X2 = middlePoint - SpineWidth / 2,
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Colors.Black),
            };

            var rightLine = new Line()
            {
                Y1 = 0,
                X1 = middlePoint + SpineWidth / 2,
                Y2 = canvasHeight,
                X2 = middlePoint + SpineWidth / 2,
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Colors.Black),
            };

            LadderCanvas.Children.Add(rightLine);
            LadderCanvas.Children.Add(leftLine);

            var nextMinute = currentTime.Add(new TimeSpan(0, 0, 60 - currentTime.Second));
            var yOffset = GetYOffsetForTime(currentTime, nextMinute);
            while (yOffset <= canvasHeight)
            {
                var yPosition = canvasHeight - yOffset;

                var leftTick = new Line()
                {
                    X1 = middlePoint - SpineWidth / 2 - TickWidth,
                    Y1 = yPosition,
                    X2 = middlePoint - SpineWidth / 2,
                    Y2 = yPosition,
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Colors.Black),
                };

                var rightTick = new Line()
                {
                    X1 = middlePoint + SpineWidth / 2 + TickWidth,
                    Y1 = yPosition,
                    X2 = middlePoint + SpineWidth / 2,
                    Y2 = yPosition,
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Colors.Black),
                };

                LadderCanvas.Children.Add(leftTick);
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
                    Text = nextTime.Minute.ToString("00"),
                };

                text.Loaded += OnTextLoaded;

                LadderCanvas.Children.Add(text);

                nextTime = nextTime.AddMinutes(5);
                yOffset += MinuteHeight * 5;

                void OnTextLoaded(object _, RoutedEventArgs e)
                {
                    Canvas.SetTop(text, yPosition - text.ActualHeight / 2);
                    Canvas.SetLeft(text, middlePoint - text.ActualWidth / 2);
                    text.Loaded -= OnTextLoaded;
                }
            }
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
}
