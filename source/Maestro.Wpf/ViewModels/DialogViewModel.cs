using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Maestro.Wpf.ViewModels;

public partial class DialogViewModel(string message) : ObservableObject
{
    [ObservableProperty]
    string _message = message;

    [RelayCommand]
    void Confirm()
    {

    }

    [RelayCommand]
    void Close()
    {

    }
}
