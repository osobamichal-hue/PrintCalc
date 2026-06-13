using System.Windows;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.SelectedMenu = vm.Menu[0];
    }
}
