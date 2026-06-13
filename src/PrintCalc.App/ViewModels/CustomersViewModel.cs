using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly AppDbContext _db;

    public ObservableCollection<Customer> Items { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newCompanyId = "";
    [ObservableProperty] private string newVatId = "";
    [ObservableProperty] private string newStreet = "";
    [ObservableProperty] private string newCity = "";
    [ObservableProperty] private string newZip = "";
    [ObservableProperty] private string newEmail = "";
    [ObservableProperty] private string newPhone = "";
    [ObservableProperty] private string newInvoiceDueDays = "14";
    [ObservableProperty] private string newPreferredPaymentMethod = "Převodem";
    [ObservableProperty] private string statusMessage = "";

    public CustomersViewModel(AppDbContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db.ChangeTracker.Clear();
        Items.Clear();
        foreach (var c in await _db.Customers.OrderBy(x => x.Name).ToListAsync())
            Items.Add(c);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            StatusMessage = "Vyplňte alespoň název zákazníka.";
            return;
        }

        var c = new Customer
        {
            Name = NewName.Trim(),
            CompanyId = string.IsNullOrWhiteSpace(NewCompanyId) ? null : NewCompanyId.Trim(),
            VatId = string.IsNullOrWhiteSpace(NewVatId) ? null : NewVatId.Trim(),
            Street = string.IsNullOrWhiteSpace(NewStreet) ? null : NewStreet.Trim(),
            City = string.IsNullOrWhiteSpace(NewCity) ? null : NewCity.Trim(),
            Zip = string.IsNullOrWhiteSpace(NewZip) ? null : NewZip.Trim(),
            Email = string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail.Trim(),
            Phone = string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
            InvoiceDueDays = ParseDueDaysOrNull(NewInvoiceDueDays),
            PreferredPaymentMethod = string.IsNullOrWhiteSpace(NewPreferredPaymentMethod) ? null : NewPreferredPaymentMethod.Trim()
        };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        Items.Add(c);
        SelectedCustomer = c;
        ClearForm();
        StatusMessage = "Zákazník byl přidán.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var c = SelectedCustomer;
        if (c is null) return;
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        Items.Remove(c);
        SelectedCustomer = null;
        StatusMessage = "Zákazník byl smazán.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _db.SaveChangesAsync();
        StatusMessage = "Změny byly uloženy.";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        StatusMessage = "Seznam zákazníků byl obnoven.";
    }

    [RelayCommand]
    private void ClearForm()
    {
        NewName = "";
        NewCompanyId = "";
        NewVatId = "";
        NewStreet = "";
        NewCity = "";
        NewZip = "";
        NewEmail = "";
        NewPhone = "";
        NewInvoiceDueDays = "14";
        NewPreferredPaymentMethod = "Převodem";
    }

    private static int? ParseDueDaysOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value.Trim(), out var days) && days > 0 ? days : null;
    }
}
