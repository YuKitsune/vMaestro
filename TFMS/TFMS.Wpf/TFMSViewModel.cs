using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TFMS.Wpf
{
    public class Aerodrome
    {
        public string Code { get; set; }

        public Aerodrome(string code)
        {
            this.Code = code;
        }
    }

    public class RunwayConfiguration
    {
        public string Name { get; set; }

        public RunwayConfiguration(string name)
        {
            this.Name = name;
        }
    }

    public partial class TFMSViewModel : ObservableObject
    {
        [ObservableProperty]
        private Aerodrome[] _availableAerodromes;

        [ObservableProperty]
        private Aerodrome? _selectedAerodrome;

        [ObservableProperty]
        private RunwayConfiguration[] _availableRunwayConfigurations;

        [ObservableProperty]
        private RunwayConfiguration? _selectedRunwayConfiguration;

        public TFMSViewModel()
        {
            _availableAerodromes = new Aerodrome[]
            {
                new Aerodrome("YBBN"),
                new Aerodrome("YSSY"),
                new Aerodrome("YMML"),
                new Aerodrome("YPAD"),
            };

            _availableRunwayConfigurations = new RunwayConfiguration[]
            {
                new RunwayConfiguration("16A27D"),
                new RunwayConfiguration("34"),
                new RunwayConfiguration("27"),
                new RunwayConfiguration("16")
            };
        }
    }
}
