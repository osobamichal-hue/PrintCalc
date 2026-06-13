using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class FilamentsView : UserControl
{
    public FilamentsView(FilamentsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void TypesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not FilamentsViewModel vm || vm.SelectedType is null)
            return;

        OpenTypeEditor(vm);
    }

    private void EditType_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FilamentsViewModel vm || vm.SelectedType is null)
            return;

        OpenTypeEditor(vm);
    }

    private async void OpenTypeEditor(FilamentsViewModel vm)
    {
        var type = vm.SelectedType;
        if (type is null) return;

        var model = new FilamentTypeEditModel
        {
            Name = type.Name,
            Manufacturer = type.Manufacturer,
            DiameterMm = type.DiameterMm,
            Color = type.Color,
            DensityGPerCm3 = type.DensityGPerCm3,
            NozzleTempMinC = type.NozzleTempMinC,
            NozzleTempMaxC = type.NozzleTempMaxC,
            BedTempMinC = type.BedTempMinC,
            BedTempMaxC = type.BedTempMaxC,
            Notes = type.Notes
        };

        var dlg = new FilamentTypeEditWindow(model)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true)
            return;

        type.Name = model.Name.Trim();
        type.Manufacturer = string.IsNullOrWhiteSpace(model.Manufacturer) ? null : model.Manufacturer.Trim();
        type.DiameterMm = model.DiameterMm;
        type.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        type.DensityGPerCm3 = model.DensityGPerCm3;
        type.NozzleTempMinC = model.NozzleTempMinC;
        type.NozzleTempMaxC = model.NozzleTempMaxC;
        type.BedTempMinC = model.BedTempMinC;
        type.BedTempMaxC = model.BedTempMaxC;
        type.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

        await vm.SaveTypesCommand.ExecuteAsync(null);
    }
}
