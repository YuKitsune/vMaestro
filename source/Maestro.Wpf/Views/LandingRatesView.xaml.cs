using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class LandingRatesView : UserControl
{
    public LandingRatesView(LandingRatesViewModel landingRatesViewModel)
    {
        InitializeComponent();
        DataContext = landingRatesViewModel;
    }
}
