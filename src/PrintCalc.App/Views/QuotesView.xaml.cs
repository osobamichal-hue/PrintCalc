using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PrintCalc.Core.Entities;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class QuotesView : UserControl
{
    private static readonly Brush[] QuoteLineGroupBrushes =
    [
        new SolidColorBrush(Color.FromRgb(237, 231, 246)),
        new SolidColorBrush(Color.FromRgb(227, 242, 253))
    ];

    static QuotesView()
    {
        foreach (var b in QuoteLineGroupBrushes)
        {
            if (b.CanFreeze)
                b.Freeze();
        }
    }

    private QuotesViewModel Vm => (QuotesViewModel)DataContext;

    public QuotesView(QuotesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void CalcSelectionGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        Vm.SetSelectedCalculations(dg.SelectedItems.OfType<Calculation>());
    }

    private void QuotesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.OpenSelectedForEditCommand.CanExecute(null))
            return;

        Vm.OpenSelectedForEditCommand.Execute(null);
    }

    private void QuoteLinesGrid_OnLoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not QuoteLine line || sender is not DataGrid dg)
            return;

        var list = dg.Items.Cast<QuoteLine>().ToList();
        var idx = list.IndexOf(line);
        if (idx < 0)
            return;

        if (line.SourceCalculationId is null)
        {
            e.Row.Background = Brushes.Transparent;
            return;
        }

        var band = 0;
        for (var i = 0; i < idx; i++)
        {
            if (list[i].SourceCalculationId != list[i + 1].SourceCalculationId)
                band++;
        }

        e.Row.Background = QuoteLineGroupBrushes[band % QuoteLineGroupBrushes.Length];
    }
}
