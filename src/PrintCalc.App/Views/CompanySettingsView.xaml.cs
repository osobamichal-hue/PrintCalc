using System.Windows.Controls;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class CompanySettingsView : UserControl
{
    public CompanySettingsView(CompanySettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
