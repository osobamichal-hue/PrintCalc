using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class ModelsView : UserControl
{
    public ModelsView(ModelsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void ModelsGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await OpenSelectedModelEditorAsync();
    }

    private async void EditModel_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenSelectedModelEditorAsync();
    }

    private async Task OpenSelectedModelEditorAsync()
    {
        if (DataContext is not ModelsViewModel vm || vm.SelectedModel is null)
            return;

        var selected = vm.SelectedModel;
        var model = new ModelEditModel
        {
            Name = selected.Name,
            EstimatedMaterialGrams = selected.EstimatedMaterialGrams?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
            EstimatedPrintHours = selected.EstimatedPrintHours?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
            Notes = selected.Notes
        };

        var dlg = new ModelEditWindow(model)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true)
            return;

        await vm.UpdateModelAsync(selected.Id, model.Name, model.ParseMaterial(), model.ParseHours(), model.Notes);
    }
}
