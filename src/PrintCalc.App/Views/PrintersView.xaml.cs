using System.Windows;
using System.Windows.Controls;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class PrintersView : UserControl
{
    public PrintersView(PrintersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void PrintersGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await OpenSelectedPrinterEditorAsync();
    }

    private async void EditPrinter_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenSelectedPrinterEditorAsync();
    }

    private async Task OpenSelectedPrinterEditorAsync()
    {
        if (DataContext is not PrintersViewModel vm || vm.SelectedPrinter is null)
            return;

        var selected = vm.SelectedPrinter;
        var model = new PrinterEditModel
        {
            Name = selected.Name,
            Kind = selected.Kind,
            HourlyRate = selected.HourlyRate,
            KwhPerHour = selected.KwhPerHour,
            StartFeePerPrint = selected.StartFeePerPrint,
            MaxVolumeDescription = selected.MaxVolumeDescription,
            Notes = selected.Notes
        };

        var dlg = new PrinterEditWindow(model)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true)
            return;

        selected.Name = model.Name.Trim();
        selected.Kind = model.Kind;
        selected.HourlyRate = model.HourlyRate;
        selected.KwhPerHour = model.KwhPerHour;
        selected.StartFeePerPrint = model.StartFeePerPrint;
        selected.MaxVolumeDescription = string.IsNullOrWhiteSpace(model.MaxVolumeDescription) ? null : model.MaxVolumeDescription.Trim();
        selected.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

        await vm.SaveCommand.ExecuteAsync(null);
    }
}
