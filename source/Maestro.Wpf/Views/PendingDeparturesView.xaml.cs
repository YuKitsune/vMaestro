using System.Windows.Controls;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class PendingDeparturesView : UserControl
{
    public PendingDeparturesView(PendingDeparturesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

