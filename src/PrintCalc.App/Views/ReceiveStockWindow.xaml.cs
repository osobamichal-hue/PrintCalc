using System.Windows;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class ReceiveStockWindow : Window
{
    public ReceiveStockWindow(FilamentsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void ConfirmReceive_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilamentsViewModel vm) return;
        await vm.ReceiveCommand.ExecuteAsync(null);
        DialogResult = true;
        Close();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
