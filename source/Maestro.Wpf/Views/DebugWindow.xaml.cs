using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class DebugWindow : Window
{
    public DebugWindow()
    {
        InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<DebugViewModel>();
    }
}