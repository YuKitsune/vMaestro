using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Maestro.Wpf.ViewModels;

public partial class DialogViewModel : ObservableObject
{
    readonly Action<bool>? _onResult;
    readonly Action? _closeDialog;

    [ObservableProperty]
    string _message;

    public DialogViewModel(string message, Action<bool>? onResult = null, Action? closeDialog = null)
    {
        _message = message;
        _onResult = onResult;
        _closeDialog = closeDialog;
    }

    [RelayCommand]
    void Confirm()
    {
        _onResult?.Invoke(true);
        _closeDialog?.Invoke();
    }

    [RelayCommand]
    void Close()
    {
        _onResult?.Invoke(false);
        _closeDialog?.Invoke();
    }
}
