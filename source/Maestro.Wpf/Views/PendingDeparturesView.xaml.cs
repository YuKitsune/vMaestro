using System.Windows.Controls;
using System.Windows.Input;
using Maestro.Wpf.ViewModels;

namespace Maestro.Wpf.Views;

public partial class PendingDeparturesView : UserControl
{
    public PendingDeparturesView(PendingDeparturesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PendingDeparturesViewModel viewModel)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != Key.Enter)
            return;

        if (!viewModel.InsertCommand.CanExecute(null))
            return;

        viewModel.InsertCommand.Execute(null);
        e.Handled = true;
    }
}
