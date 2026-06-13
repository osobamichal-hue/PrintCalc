using System.Windows.Controls;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class HelpAboutView : UserControl
{
    public HelpAboutView(HelpAboutViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
