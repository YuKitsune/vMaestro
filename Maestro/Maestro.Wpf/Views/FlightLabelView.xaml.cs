using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maestro.Core.Configuration;

namespace Maestro.Wpf.Views;

/// <summary>
/// Interaction logic for AircraftView.xaml
/// </summary>
public partial class FlightLabelView : UserControl
{
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        "Click",
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(TabItem));

    public static readonly DependencyProperty LadderPositionProperty = DependencyProperty.Register(
        nameof(LadderPosition),
        typeof(LadderPosition),
        typeof(FlightLabelView));

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public LadderPosition LadderPosition
    {
        get => (LadderPosition)GetValue(LadderPositionProperty);
        set => SetValue(LadderPositionProperty, value);
    }
    
    public FlightLabelView()
    {
        InitializeComponent();
    }

    void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        RoutedEventArgs newEventArgs = new RoutedEventArgs(ClickEvent);
        RaiseEvent(newEventArgs);
    }
}
