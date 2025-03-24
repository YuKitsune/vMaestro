using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TFMS.Wpf
{
    public partial class LadderViewModel : ObservableObject
    {
        [ObservableProperty]
        List<AircraftViewModel> _aircraft;

        public LadderViewModel()
        {
            _aircraft = new List<AircraftViewModel>() { };
        }
    }
}
