using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Maestro.Wpf;

/// <summary>
/// Interaction logic for AircraftView.xaml
/// </summary>
public partial class AircraftView : UserControl
{
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        "Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TabItem));

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }
    
    public AircraftView()
    {
        InitializeComponent();
    }

    void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        RoutedEventArgs newEventArgs = new RoutedEventArgs(ClickEvent);
        RaiseEvent(newEventArgs);
    }
}
