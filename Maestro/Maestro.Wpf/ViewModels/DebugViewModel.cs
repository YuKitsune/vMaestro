﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Dtos.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DebugViewModel : ObservableObject, INotificationHandler<SequenceModifiedNotification>
{
    [ObservableProperty]
    ObservableCollection<FlightViewModel> _flights = new();
    
    public Task Handle(SequenceModifiedNotification notification, CancellationToken cancellationToken)
    {
        Flights.Clear();
        Flights = new ObservableCollection<FlightViewModel>(notification.Sequence.Flights.Select(a =>
            new FlightViewModel(
                a.Callsign,
                a.AircraftType,
                a.WakeCategory,
                a.Origin,
                a.Destination,
                -1, // TODO:
                a.FeederFix,
                a.InitialFeederFixTime,
                a.EstimatedFeederFixTime,
                a.ScheduledFeederFixTime,
                a.AssignedRunway,
                -1, // TODO:
                a.InitialLandingTime,
                a.EstimatedLandingTime,
                a.ScheduledLandingTime,
                a.InitialDelay,
                a.CurrentDelay)));
        
        return Task.CompletedTask;
    }
}