using System.Windows.Controls;
using System.Windows.Input;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class OrdersView : UserControl
{
    private OrdersViewModel Vm => (OrdersViewModel)DataContext;

    public OrdersView(OrdersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OrdersGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.OpenSelectedForEditCommand.CanExecute(null))
            return;

        Vm.OpenSelectedForEditCommand.Execute(null);
    }
}
