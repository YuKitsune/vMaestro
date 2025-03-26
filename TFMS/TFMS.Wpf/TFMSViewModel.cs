using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TFMS.Core;

namespace TFMS.Wpf
{
    public class AirportViewModel
    {
        public string Identifier { get; set; }
    }

    public class RunwayModeViewModel
    {
        public string Identifier { get; set; }

        public RunwayViewModel[] Runways { get; set; }

    }

    public class RunwayViewModel
    {
        public string Identifier { get; set; }
        public TimeSpan LandingRate { get; set; }
    }

    public class SectorViewModel
    {
        public string Identifier { get; set; }
        public string[] FeederFixes { get; set; }
    }

    public partial class TFMSViewModel : ObservableObject
    {
        [ObservableProperty]
        private AirportViewModel[] _availableAirports = [];

        [ObservableProperty]
        private AirportViewModel? _selectedAirport;

        [ObservableProperty]
        private RunwayModeViewModel[] _availableRunwayModes = [];

        [ObservableProperty]
        private RunwayModeViewModel? _selectedRunwayMode;

        [ObservableProperty]
        private RunwayViewModel[] _runwayRates;

        [ObservableProperty]
        private SectorViewModel[] _availableSectors = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LeftFeederFixes))]
        [NotifyPropertyChangedFor(nameof(RightFeederFixes))]
        private SectorViewModel? _selectedSector;

        public string[] LeftFeederFixes => SelectedSector is null ? [] : SelectedSector.FeederFixes.Take(SelectedSector.FeederFixes.Length / 2).ToArray();
        public string[] RightFeederFixes => SelectedSector is null ? [] : SelectedSector.FeederFixes.Skip(SelectedSector.FeederFixes.Length / 2).ToArray();

        [ObservableProperty]
        List<AircraftViewModel> _aircraft = [];

        public TFMSViewModel()
        {
            AvailableAirports = Configuration.Demo.Airports
                .Select(a => new AirportViewModel { Identifier = a.Identifier })
                .ToArray();

            SelectedAirport = AvailableAirports.FirstOrDefault();
        }

        partial void OnSelectedAirportChanged(AirportViewModel airportViewModel)
        {
            AvailableRunwayModes = Configuration.Demo.Airports
                .First(a => a.Identifier == airportViewModel.Identifier)
                .RunwayModes.Select(rm =>
                    new RunwayModeViewModel
                    { 
                        Identifier = rm.Identifier,
                        Runways = rm.RunwayRates.Select(r =>
                            new RunwayViewModel
                            {
                                Identifier = r.RunwayIdentifier,
                                LandingRate = r.LandingRate,
                            }).ToArray()
                    })
                .ToArray();

            SelectedRunwayMode = AvailableRunwayModes.FirstOrDefault();
        }

        partial void OnSelectedRunwayModeChanged(RunwayModeViewModel runwayModeViewModel)
        {
            RunwayRates = Configuration.Demo.Airports
                .First(a => a.Identifier == SelectedAirport.Identifier)
                .RunwayModes.First(r => r.Identifier == runwayModeViewModel.Identifier)
                .RunwayRates.Select(r =>
                    new RunwayViewModel
                    {
                        Identifier = r.RunwayIdentifier,
                        LandingRate = r.LandingRate
                    })
                .ToArray();
        }

        [RelayCommand]
        void SelectSector(SectorViewModel sectorViewModel)
        {
            SelectedSector = sectorViewModel;
        }
    }
}
