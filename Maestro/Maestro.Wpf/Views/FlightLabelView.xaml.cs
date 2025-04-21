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
    public static readonly DependencyProperty LadderPositionProperty = DependencyProperty.Register(
        nameof(LadderPosition),
        typeof(LadderPosition),
        typeof(FlightLabelView));

    public LadderPosition LadderPosition
    {
        get => (LadderPosition)GetValue(LadderPositionProperty);
        set => SetValue(LadderPositionProperty, value);
    }
    
    public FlightLabelView()
    {
        InitializeComponent();
    }
}
