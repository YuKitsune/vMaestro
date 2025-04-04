﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Wpf.ViewModels;

public partial class AirportViewModel(string identifier, RunwayModeViewModel[] runwayModes, ViewConfigurationDto[] views) : ObservableObject
{
    public string Identifier => identifier;

    [ObservableProperty]
    ObservableCollection<RunwayModeViewModel> _runwayModes = new(runwayModes);

    [ObservableProperty]
    ObservableCollection<ViewConfigurationDto> _views = new(views);
}

public class RunwayModeViewModel(string identifier, RunwayViewModel[] runwayModes)
{
    public string Identifier => identifier;

    public RunwayViewModel[] Runways => runwayModes;
}

public partial class RunwayViewModel(string identifier, TimeSpan defaultLandingRate) : ObservableObject
{
    public string Identifier => identifier;

    public TimeSpan DefaultLandingRate => defaultLandingRate;

    [ObservableProperty]
    TimeSpan _landingRate = defaultLandingRate;
}
