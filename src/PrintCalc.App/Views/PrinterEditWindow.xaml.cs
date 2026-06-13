using System.Windows;
using PrintCalc.Core.Enums;

namespace PrintCalc.App.Views;

public partial class PrinterEditWindow : Window
{
    public PrinterEditWindow(PrinterEditModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    public PrinterEditModel Model => (PrinterEditModel)DataContext;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Model.Name))
            return;

        DialogResult = true;
        Close();
    }
}

public sealed class PrinterEditModel
{
    public string Name { get; set; } = "";
    public PrinterKind Kind { get; set; } = PrinterKind.Fff;
    public decimal HourlyRate { get; set; }
    public decimal KwhPerHour { get; set; }
    public decimal StartFeePerPrint { get; set; }
    public string? MaxVolumeDescription { get; set; }
    public string? Notes { get; set; }
}

