using System.Collections.ObjectModel;
using System.Collections;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PrintCalc.App.Helpers;
using PrintCalc.Core.Helpers;
using PrintCalc.App.Services;
using PrintCalc.App.Views;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class OrdersViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private bool _isLoadingSettings;
    private bool _uiSettingsLoaded;

    public ObservableCollection<Order> Orders { get; } = new();
    public ObservableCollection<Quote> OpenQuotes { get; } = new();

    [ObservableProperty] private Order? selectedOrder;
    [ObservableProperty] private OrderLine? selectedOrderLine;
    [ObservableProperty] private int? selectedQuoteId;
    [ObservableProperty] private int wizardStepIndex = 0;
    [ObservableProperty] private string newLineDescription = "Položka";
    [ObservableProperty] private decimal newLineQuantity = 1m;
    [ObservableProperty] private decimal newLineUnitPrice = 0m;
    [ObservableProperty] private bool createAsDetailedFromQuotes = true;
    [ObservableProperty] private bool compactTables;

    public OrdersViewModel(AppDbContext db, IDocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_uiSettingsLoaded)
        {
            await LoadUiSettingsAsync();
            _uiSettingsLoaded = true;
        }

        await LoadOpenQuotesAsync();
        await LoadOrdersAsync();
    }

    private async Task LoadOpenQuotesAsync()
    {
        _db.ChangeTracker.Clear();
        OpenQuotes.Clear();
        foreach (var q in await _db.Quotes.AsNoTracking().OrderByDescending(x => x.IssueDate).Take(100).ToListAsync())
            OpenQuotes.Add(q);
    }

    private async Task LoadOrdersAsync()
    {
        var list = await _db.Orders.AsNoTracking().Include(x => x.Lines).OrderByDescending(x => x.CreatedAt).ToListAsync();
        await DispatcherObservableRefresh.ReplaceAsync(Orders, list);
    }

    [RelayCommand]
    private async Task FromQuoteAsync()
    {
        var quotes = await _db.Quotes.AsNoTracking().Include(q => q.Lines).OrderByDescending(q => q.IssueDate).Take(200).ToListAsync();
        if (SelectedQuoteId is { } selectedId)
            quotes = quotes.Where(q => q.Id == selectedId).ToList();
        var picker = new NewOrderFromQuotesWindow(quotes) { Owner = Application.Current?.MainWindow };
        picker.CreateAsDetailedFromQuotes = CreateAsDetailedFromQuotes;
        if (picker.ShowDialog() != true) return;
        CreateAsDetailedFromQuotes = picker.CreateAsDetailedFromQuotes;
        var selected = quotes.Where(q => picker.SelectedQuoteIds.Contains(q.Id)).ToList();
        if (selected.Count == 0) return;

        var group = selected.Where(q => q.CustomerId > 0).GroupBy(q => q.CustomerId).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (group is null) return;
        var items = group.OrderBy(q => q.IssueDate).ToList();
        var num = await _numbers.NextOrderNumberAsync();
        var o = new Order
        {
            CustomerId = group.Key,
            Number = num,
            Title = items.Count == 1 ? items[0].Title : DocumentTitleExcerpt.FromQuotesForOrderTitle(items),
            QuoteId = items.Count == 1 ? items[0].Id : null,
            TotalAmount = items.Sum(x => x.TotalAmount),
            Status = OrderStatus.Confirmed
        };
        foreach (var quote in items)
        {
            if (CreateAsDetailedFromQuotes)
            {
                foreach (var l in quote.Lines)
                {
                    o.Lines.Add(new OrderLine
                    {
                        Description = items.Count == 1 ? l.Description : $"[{quote.Number}] {l.Description}",
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.LineTotal,
                        SourceCalculationId = l.SourceCalculationId
                    });
                }
            }
            else
            {
                o.Lines.Add(new OrderLine
                {
                    Description = items.Count == 1 ? quote.Title : $"[{quote.Number}] {quote.Title}",
                    Quantity = 1,
                    UnitPrice = quote.TotalAmount,
                    LineTotal = quote.TotalAmount
                });
            }
        }
        o.Title = DocumentTitleExcerpt.ForOrderGridCaption(o);
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == o.CustomerId);
        var preview = new OrderPreviewWindow(
            o,
            customer?.Name ?? $"Zákazník #{o.CustomerId}",
            $"{customer?.Street} {customer?.Zip} {customer?.City}".Trim(),
            customer?.CompanyId ?? "",
            customer?.VatId ?? "")
        { Owner = Application.Current?.MainWindow };
        if (preview.ShowDialog() != true) return;
        _db.Orders.Add(o);
        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedOrder = Orders.FirstOrDefault(x => x.Id == o.Id);
    }

    private async Task SaveOrderChangesAsync()
    {
        if (SelectedOrder is null) return;
        var selectedId = SelectedOrder.Id;
        foreach (var line in SelectedOrder.Lines)
            line.LineTotal = Math.Round(line.Quantity * line.UnitPrice, 0, MidpointRounding.AwayFromZero);
        SelectedOrder.TotalAmount = SelectedOrder.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();
        await LoadOrdersAsync();
        SelectedOrder = Orders.FirstOrDefault(x => x.Id == selectedId);
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveOrderChangesAsync();

    [RelayCommand]
    private async Task SaveAndBackToListAsync()
    {
        await SaveOrderChangesAsync();
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var orderId = SelectedOrder?.Id;
        if (orderId is null) return;
        var tracked = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId.Value);
        if (tracked is null) return;
        _db.Orders.Remove(tracked);
        await _db.SaveChangesAsync();
        await LoadOrdersAsync();
        SelectedOrder = null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedId = SelectedOrder?.Id;
        await LoadAsync();
        if (selectedId is not null)
            SelectedOrder = Orders.FirstOrDefault(x => x.Id == selectedId.Value);
    }

    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (SelectedOrder is null) return;
        var description = string.IsNullOrWhiteSpace(NewLineDescription) ? "Položka" : NewLineDescription.Trim();
        var qty = NewLineQuantity <= 0 ? 1 : NewLineQuantity;
        var price = NewLineUnitPrice < 0 ? 0 : NewLineUnitPrice;
        var l = new OrderLine
        {
            OrderId = SelectedOrder.Id,
            Description = description,
            Quantity = qty,
            UnitPrice = price,
            LineTotal = Math.Round(qty * price, 0, MidpointRounding.AwayFromZero)
        };
        _db.OrderLines.Add(l);
        await _db.SaveChangesAsync();
        await LoadOrdersAsync();
        SelectedOrder = Orders.FirstOrDefault(x => x.Id == l.OrderId);
        NewLineDescription = "Položka";
        NewLineQuantity = 1m;
        NewLineUnitPrice = 0m;
    }

    partial void OnSelectedOrderChanged(Order? value)
    {
        SelectedOrderLine = null;
        RemoveSelectedOrderLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedOrderLineChanged(OrderLine? value) =>
        RemoveSelectedOrderLineCommand.NotifyCanExecuteChanged();

    private bool CanRemoveSelectedOrderLine() => SelectedOrder is not null && SelectedOrderLine is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedOrderLine))]
    private async Task RemoveSelectedOrderLineAsync()
    {
        if (SelectedOrder is null || SelectedOrderLine is null) return;
        var orderId = SelectedOrder.Id;
        var lineId = SelectedOrderLine.Id;

        var o = await _db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == orderId);
        if (o is null) return;
        var trackedLine = o.Lines.FirstOrDefault(l => l.Id == lineId);
        if (trackedLine is null) return;

        o.Lines.Remove(trackedLine);
        foreach (var l in o.Lines)
            l.LineTotal = Math.Round(l.Quantity * l.UnitPrice, 0, MidpointRounding.AwayFromZero);
        o.TotalAmount = o.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();

        await LoadOrdersAsync();
        SelectedOrder = Orders.FirstOrDefault(x => x.Id == orderId);
        SelectedOrderLine = null;
    }

    [RelayCommand]
    private void OpenSelectedForEdit()
    {
        if (SelectedOrder is null) return;
        WizardStepIndex = 1;
    }

    partial void OnWizardStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevStep));
        OnPropertyChanged(nameof(CanGoNextStep));
        OnPropertyChanged(nameof(WizardStepTitle));
        OnPropertyChanged(nameof(IsListStep));
        OnPropertyChanged(nameof(IsCreateStep));
        OnPropertyChanged(nameof(IsEditStep));
    }

    public bool CanGoPrevStep => WizardStepIndex > 0;
    public bool CanGoNextStep => WizardStepIndex < 2;
    public bool IsListStep => WizardStepIndex == 0;
    public bool IsCreateStep => WizardStepIndex == 2;
    public bool IsEditStep => WizardStepIndex == 1;
    public double TableRowHeight => CompactTables ? 30 : 42;
    public string WizardStepTitle => WizardStepIndex switch
    {
        0 => "Krok 1/3 - Seznam zakázek",
        2 => "Krok 2/3 - Nová zakázka z nabídek",
        _ => "Krok 3/3 - Editace zakázky"
    };

    [RelayCommand]
    private void PrevStep()
    {
        if (WizardStepIndex > 0) WizardStepIndex--;
    }

    [RelayCommand]
    private async Task NextStep()
    {
        if (WizardStepIndex == 0)
        {
            WizardStepIndex = 2;
            return;
        }

        if (WizardStepIndex == 2)
        {
            await FromQuoteAsync();
            if (SelectedOrder is not null)
                WizardStepIndex = 1;
            return;
        }

        if (SelectedOrder is not null)
            await SaveAsync();
    }

    [RelayCommand]
    private void GoToStep(object? index)
    {
        int parsed;
        if (index is int i) parsed = i;
        else if (index is string s && int.TryParse(s, out var si)) parsed = si;
        else return;
        if (parsed < 0 || parsed > 2) return;
        WizardStepIndex = parsed;
    }

    partial void OnCompactTablesChanged(bool value)
    {
        OnPropertyChanged(nameof(TableRowHeight));
    }

    [RelayCommand]
    private void StartNewOrder()
    {
        WizardStepIndex = 2;
    }

    [RelayCommand]
    private void BackToList()
    {
        WizardStepIndex = 0;
    }

    partial void OnCreateAsDetailedFromQuotesChanged(bool value)
    {
        if (_isLoadingSettings) return;
        _ = SaveUiSettingsAsync(value);
    }

    private async Task LoadUiSettingsAsync()
    {
        _isLoadingSettings = true;
        try
        {
            var value = await _db.AppSettings.AsNoTracking()
                .Where(x => x.Key == "Orders.CreateAsDetailedFromQuotes")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            CreateAsDetailedFromQuotes = value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task SaveUiSettingsAsync(bool value)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Orders.CreateAsDetailedFromQuotes");
        if (row is null)
        {
            row = new AppSettingsRow { Key = "Orders.CreateAsDetailedFromQuotes" };
            _db.AppSettings.Add(row);
        }
        row.Value = value ? "true" : "false";
        await _db.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task CreateInvoiceFromSelectedOrdersAsync(object? selectedItems)
    {
        var selectedIds = new HashSet<int>();
        if (selectedItems is IList list)
        {
            foreach (var item in list.OfType<Order>())
                selectedIds.Add(item.Id);
        }

        if (selectedIds.Count == 0 && SelectedOrder is not null)
            selectedIds.Add(SelectedOrder.Id);

        if (selectedIds.Count == 0)
        {
            AppDialog.ShowInfo("Vyberte alespoň jednu zakázku.", "Faktura ze zakázek");
            return;
        }

        var selectedOrders = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => selectedIds.Contains(o.Id))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        if (selectedOrders.Count == 0)
            return;

        var customerIds = selectedOrders
            .Where(o => o.CustomerId > 0)
            .Select(o => o.CustomerId)
            .Distinct()
            .ToList();
        if (customerIds.Count != 1)
        {
            AppDialog.ShowInfo("Pro jednu fakturu vyberte zakázky pouze jednoho zákazníka.", "Faktura ze zakázek");
            return;
        }

        var numberPrefix = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "Finance.InvoiceNumberPrefix")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        var dueDaysRaw = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "Company.InvoiceDueDays")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        var paymentMethodRaw = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "Company.PaymentMethod")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        var defaultVatRaw = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "Finance.DefaultVatRatePercent")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        var series = string.IsNullOrWhiteSpace(numberPrefix) ? "INV" : numberPrefix.Trim().ToUpperInvariant();
        var dueDays = int.TryParse(dueDaysRaw, out var parsedDueDays) && parsedDueDays > 0 ? parsedDueDays : 14;
        var paymentMethod = string.IsNullOrWhiteSpace(paymentMethodRaw) ? "Převodem" : paymentMethodRaw.Trim();
        var vatRate = decimal.TryParse(defaultVatRaw?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedVat)
            ? parsedVat
            : 21m;

        var invoice = new Invoice
        {
            CustomerId = customerIds[0],
            Number = await _numbers.NextInvoiceNumberAsync(series),
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(dueDays),
            PaymentMethod = paymentMethod,
            OrderId = selectedOrders.Count == 1 ? selectedOrders[0].Id : null,
            Status = InvoiceStatus.Draft,
            TotalAmount = selectedOrders.Sum(o => o.TotalAmount)
        };

        foreach (var order in selectedOrders)
        {
            foreach (var line in order.Lines)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxRatePercent = vatRate,
                    LineTotal = line.LineTotal,
                    SourceCalculationId = line.SourceCalculationId,
                    SourceOrderId = order.Id,
                    SourceOrderLineId = line.Id
                });
            }
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        AppDialog.ShowInfo($"Faktura {invoice.Number} byla vytvořena ze {selectedOrders.Count} zakázek.", "Faktura ze zakázek");
    }
}

