using System.Windows;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class IssueStockWindow : Window
{
    public IssueStockWindow(FilamentsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void ConfirmIssue_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilamentsViewModel vm) return;
        await vm.IssueCommand.ExecuteAsync(null);
        DialogResult = true;
        Close();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
