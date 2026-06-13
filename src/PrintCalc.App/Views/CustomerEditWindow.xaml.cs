using System.Windows;

namespace PrintCalc.App.Views;

public partial class CustomerEditWindow : Window
{
    public CustomerEditWindow(CustomerEditModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    public CustomerEditModel Model => (CustomerEditModel)DataContext;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Model.Name))
            return;

        DialogResult = true;
        Close();
    }
}

public sealed class CustomerEditModel
{
    public string Name { get; set; } = "";
    public string? CompanyId { get; set; }
    public string? VatId { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? InvoiceDueDays { get; set; }
    public string? PreferredPaymentMethod { get; set; }
}

