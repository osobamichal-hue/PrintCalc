using System.Windows;

namespace PrintCalc.App.Views;

public partial class FilamentTypeEditWindow : Window
{
    public FilamentTypeEditWindow(FilamentTypeEditModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    public FilamentTypeEditModel Model => (FilamentTypeEditModel)DataContext;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Model.Name))
            return;
        DialogResult = true;
        Close();
    }
}

public sealed class FilamentTypeEditModel
{
    public string Name { get; set; } = "";
    public string? Manufacturer { get; set; }
    public decimal DiameterMm { get; set; }
    public string? Color { get; set; }
    public decimal DensityGPerCm3 { get; set; }
    public int? NozzleTempMinC { get; set; }
    public int? NozzleTempMaxC { get; set; }
    public int? BedTempMinC { get; set; }
    public int? BedTempMaxC { get; set; }
    public string? Notes { get; set; }
}

