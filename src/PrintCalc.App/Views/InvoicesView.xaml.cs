using System.Windows.Controls;
using System.Windows.Input;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class InvoicesView : UserControl
{
    private InvoicesViewModel Vm => (InvoicesViewModel)DataContext;

    public InvoicesView(InvoicesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void InvoicesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.OpenSelectedForEditCommand.CanExecute(null))
            return;

        Vm.OpenSelectedForEditCommand.Execute(null);
    }
}
