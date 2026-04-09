using Avalonia.Controls;
using Maestro.Avalonia.ViewModels;

namespace Maestro.Avalonia.Views;

public partial class PendingDeparturesView : UserControl
{
    public PendingDeparturesView()
    {
        InitializeComponent();
    }

    public PendingDeparturesView(PendingDeparturesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

