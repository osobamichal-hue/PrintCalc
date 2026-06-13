using System.Windows;
using System.Windows.Controls;
using PrintCalc.App.ViewModels;

namespace PrintCalc.App.Views;

public partial class CustomersView : UserControl
{
    public CustomersView(CustomersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void CustomersGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await OpenSelectedCustomerEditorAsync();
    }

    private async void EditCustomer_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenSelectedCustomerEditorAsync();
    }

    private async Task OpenSelectedCustomerEditorAsync()
    {
        if (DataContext is not CustomersViewModel vm || vm.SelectedCustomer is null)
            return;

        var selected = vm.SelectedCustomer;
        var model = new CustomerEditModel
        {
            Name = selected.Name,
            CompanyId = selected.CompanyId,
            VatId = selected.VatId,
            Street = selected.Street,
            City = selected.City,
            Zip = selected.Zip,
            Email = selected.Email,
            Phone = selected.Phone,
            InvoiceDueDays = selected.InvoiceDueDays,
            PreferredPaymentMethod = selected.PreferredPaymentMethod
        };

        var dlg = new CustomerEditWindow(model)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true)
            return;

        selected.Name = model.Name.Trim();
        selected.CompanyId = string.IsNullOrWhiteSpace(model.CompanyId) ? null : model.CompanyId.Trim();
        selected.VatId = string.IsNullOrWhiteSpace(model.VatId) ? null : model.VatId.Trim();
        selected.Street = string.IsNullOrWhiteSpace(model.Street) ? null : model.Street.Trim();
        selected.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
        selected.Zip = string.IsNullOrWhiteSpace(model.Zip) ? null : model.Zip.Trim();
        selected.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        selected.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        selected.InvoiceDueDays = model.InvoiceDueDays is > 0 ? model.InvoiceDueDays : null;
        selected.PreferredPaymentMethod = string.IsNullOrWhiteSpace(model.PreferredPaymentMethod) ? null : model.PreferredPaymentMethod.Trim();

        await vm.SaveCommand.ExecuteAsync(null);
    }
}
