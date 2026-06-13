using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PrintCalc.App.Helpers;
using PrintCalc.App.Services;
using PrintCalc.App.Views;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class InvoicesViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IInvoicePdfService _pdf;
    private readonly IAccountingExportService _csv;
    private decimal _defaultVatRatePercent = 21m;
    private bool _isLoadingSettings;
    private bool _isEditingNewInvoiceSession;
    private bool _uiSettingsLoaded;

    public ObservableCollection<Invoice> Invoices { get; } = new();
    public ObservableCollection<Order> OpenOrders { get; } = new();
    public ObservableCollection<OrderSelectionItem> OpenOrderSelections { get; } = new();
    public ObservableCollection<string> InvoiceSeriesOptions { get; } = new();
    public ObservableCollection<string> PaymentMethodOptions { get; } = new() { "Převodem", "Hotově", "Kartou" };

    [ObservableProperty] private Invoice? selectedInvoice;
    [ObservableProperty] private InvoiceLine? selectedInvoiceLine;
    [ObservableProperty] private int? selectedOrderId;
    [ObservableProperty] private string selectedInvoiceSeries = "INV";
    [ObservableProperty] private string customNextInvoiceNumber = "";
    [ObservableProperty] private string newInvoiceDueDays = "14";
    [ObservableProperty] private string newInvoicePaymentMethod = "Převodem";
    [ObservableProperty] private int wizardStepIndex = 0;
    [ObservableProperty] private string newLineDescription = "Položka";
    [ObservableProperty] private decimal newLineQuantity = 1m;
    [ObservableProperty] private decimal newLineUnitPrice = 0m;
    [ObservableProperty] private decimal newLineTaxRatePercent = 21m;
    [ObservableProperty] private string currencySymbol = "Kč";
    [ObservableProperty] private bool createAsDetailedFromOrders = true;
    [ObservableProperty] private bool compactTables;
    public string SelectedOpenOrdersCountText => $"Vybráno zakázek: {OpenOrderSelections.Count(x => x.IsSelected)}";
    public decimal NewLineUnitPriceWithVat => Math.Round(NewLineUnitPrice * (1 + (NewLineTaxRatePercent / 100m)), 0, MidpointRounding.AwayFromZero);
    public decimal NewLineTotalWithoutVat => Math.Round(NewLineQuantity * NewLineUnitPrice, 0, MidpointRounding.AwayFromZero);
    public decimal NewLineTotalWithVat => Math.Round(NewLineTotalWithoutVat * (1 + (NewLineTaxRatePercent / 100m)), 0, MidpointRounding.AwayFromZero);
    public double TableRowHeight => CompactTables ? 30 : 42;

    public InvoicesViewModel(
        AppDbContext db,
        IDocumentNumberService numbers,
        IInvoicePdfService pdf,
        IAccountingExportService csv)
    {
        _db = db;
        _numbers = numbers;
        _pdf = pdf;
        _csv = csv;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_uiSettingsLoaded)
        {
            await LoadUiSettingsAsync();
            _uiSettingsLoaded = true;
        }
        _defaultVatRatePercent = await GetSettingDecimalAsync("Finance.DefaultVatRatePercent", 21m);
        CurrencySymbol = await GetSettingAsync("Finance.CurrencySymbol", "Kč");
        await LoadInvoiceSeriesOptionsAsync();
        await LoadInvoiceDefaultsFromCompanyAsync();
        if (NewLineTaxRatePercent <= 0)
            NewLineTaxRatePercent = _defaultVatRatePercent;

        await LoadOpenOrdersAsync();
        await LoadInvoicesAsync();
    }

    private async Task LoadOpenOrdersAsync()
    {
        _db.ChangeTracker.Clear();
        OpenOrders.Clear();
        OpenOrderSelections.Clear();
        foreach (var o in await _db.Orders.AsNoTracking().Include(x => x.Lines).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync())
        {
            OpenOrders.Add(o);
            var item = new OrderSelectionItem(o);
            item.PropertyChanged += OpenOrderSelectionOnPropertyChanged;
            OpenOrderSelections.Add(item);
        }
        OnPropertyChanged(nameof(SelectedOpenOrdersCountText));
    }

    private async Task LoadInvoicesAsync()
    {
        var list = await _db.Invoices.AsNoTracking().Include(x => x.Lines).OrderByDescending(x => x.IssueDate).ToListAsync();
        await DispatcherObservableRefresh.ReplaceAsync(Invoices, list);
    }

    [RelayCommand]
    private async Task FromOrderAsync()
    {
        var selectedIds = OpenOrderSelections.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => selectedIds.Contains(o.Id))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(CustomNextInvoiceNumber))
        {
            if (!int.TryParse(CustomNextInvoiceNumber.Trim(), out var parsedNext) || parsedNext <= 0)
            {
                AppDialog.ShowInfo("Pole „Další číslo“ musí být kladné celé číslo.", "Číslování faktur");
                return;
            }
            await SetInvoiceSeriesNextNumberAsync(SelectedInvoiceSeries, parsedNext);
        }
        if (orders.Count == 0)
        {
            AppDialog.ShowInfo("Vyberte alespoň jednu zakázku v seznamu.", "Nová faktura ze zakázek");
            return;
        }
        var selected = orders;

        var group = selected.Where(o => o.CustomerId > 0).GroupBy(o => o.CustomerId).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (group is null) return;
        var items = group.OrderBy(o => o.CreatedAt).ToList();
        var num = await _numbers.NextInvoiceNumberAsync(SelectedInvoiceSeries);
        var inv = new Invoice
        {
            CustomerId = group.Key,
            Number = num,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(ParseDueDaysOrDefault(NewInvoiceDueDays)),
            PaymentMethod = string.IsNullOrWhiteSpace(NewInvoicePaymentMethod) ? "Převodem" : NewInvoicePaymentMethod.Trim(),
            OrderId = items.Count == 1 ? items[0].Id : null,
            Status = InvoiceStatus.Draft,
            TotalAmount = items.Sum(x => x.TotalAmount)
        };
        foreach (var order in items)
        {
            if (CreateAsDetailedFromOrders)
            {
                foreach (var l in order.Lines)
                {
                    inv.Lines.Add(new InvoiceLine
                    {
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRatePercent = _defaultVatRatePercent,
                        LineTotal = l.LineTotal,
                        SourceCalculationId = l.SourceCalculationId,
                        SourceOrderId = order.Id,
                        SourceOrderLineId = l.Id
                    });
                }
            }
            else
            {
                inv.Lines.Add(new InvoiceLine
                {
                    Description = order.Title,
                    Quantity = 1,
                    UnitPrice = order.TotalAmount,
                    TaxRatePercent = _defaultVatRatePercent,
                    LineTotal = order.TotalAmount,
                    SourceOrderId = order.Id
                });
            }
        }

        var supplierName = await GetSettingAsync("Company.Name", "Moje 3D firma");
        var supplierAddress = await GetSettingAsync("Company.Address", "");
        var supplierIco = await GetSettingAsync("Company.Ico", "");
        var supplierDic = await GetSettingAsync("Company.Dic", "");
        var supplierEmail = await GetSettingAsync("Company.Email", "");
        var supplierPhone = await GetSettingAsync("Company.Phone", "");
        var paymentMethod = inv.PaymentMethod ?? await GetSettingAsync("Company.PaymentMethod", "Převodem");
        var iban = await GetSettingAsync("Company.Iban", "");
        var swift = await GetSettingAsync("Company.Swift", "");
        var bankAccount = await GetSettingAsync("Company.BankAccount", "");
        var logoPath = await GetSettingAsync("Company.LogoPath", "");
        var visualStyle = await GetSettingAsync("Company.DocumentVisualStyle", "Phoenix");
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == inv.CustomerId);

        var preview = new InvoicePreviewWindow(
            inv,
            supplierName,
            supplierAddress,
            supplierIco,
            supplierDic,
            supplierEmail,
            supplierPhone,
            paymentMethod,
            iban,
            swift,
            bankAccount,
            logoPath,
            visualStyle,
            customer?.Name ?? $"Zákazník #{inv.CustomerId}",
            $"{customer?.Street} {customer?.Zip} {customer?.City}".Trim(),
            $"{customer?.Email} {customer?.Phone}".Trim(),
            customer?.CompanyId ?? "",
            customer?.VatId ?? "")
        { Owner = Application.Current?.MainWindow };
        if (preview.ShowDialog() != true) return;

        var exportAfterSave = AppDialog.ShowQuestion(
            "Přejete si po uložení faktury rovnou vytvořit PDF a otevřít ho v systému?",
            "Uložení faktury",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes;

        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync();

        if (exportAfterSave)
            await SaveInvoicePdfAndOpenAsync(inv);

        await LoadAsync();
        SelectedInvoice = Invoices.FirstOrDefault(x => x.Id == inv.Id);
        SetIsEditingNewInvoiceSession(false);
        CustomNextInvoiceNumber = "";
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private void SelectAllOpenOrdersForInvoice()
    {
        foreach (var item in OpenOrderSelections)
            item.IsSelected = true;
        OnPropertyChanged(nameof(SelectedOpenOrdersCountText));
    }

    [RelayCommand]
    private void ClearOpenOrdersForInvoice()
    {
        foreach (var item in OpenOrderSelections)
            item.IsSelected = false;
        OnPropertyChanged(nameof(SelectedOpenOrdersCountText));
    }

    partial void OnSelectedOrderIdChanged(int? value)
    {
        _ = LoadDefaultsFromSelectedOrderCustomerAsync(value);
    }

    private async Task LoadInvoiceSeriesOptionsAsync()
    {
        var listRaw = await GetSettingAsync("Finance.InvoiceNumberSeriesList", "");
        var defaultPrefix = await GetSettingAsync("Finance.InvoiceNumberPrefix", "INV");
        var options = listRaw
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!options.Contains(defaultPrefix, StringComparer.OrdinalIgnoreCase))
            options.Insert(0, string.IsNullOrWhiteSpace(defaultPrefix) ? "INV" : defaultPrefix.Trim().ToUpperInvariant());
        if (options.Count == 0)
            options.Add("INV");

        InvoiceSeriesOptions.Clear();
        foreach (var option in options)
            InvoiceSeriesOptions.Add(option);

        if (!InvoiceSeriesOptions.Contains(SelectedInvoiceSeries, StringComparer.OrdinalIgnoreCase))
            SelectedInvoiceSeries = InvoiceSeriesOptions[0];
    }

    private async Task SetInvoiceSeriesNextNumberAsync(string seriesPrefix, int nextNumber)
    {
        var clean = new string((seriesPrefix ?? "INV").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        if (string.IsNullOrWhiteSpace(clean))
            clean = "INV";
        var key = $"Finance.InvoiceSeriesNext.{clean.ToUpperInvariant()}";
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (row is null)
        {
            row = new AppSettingsRow { Key = key };
            _db.AppSettings.Add(row);
        }
        row.Value = nextNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedInvoice is null) return;
        var selectedId = SelectedInvoice.Id;
        foreach (var line in SelectedInvoice.Lines)
            line.LineTotal = Math.Round(line.Quantity * line.UnitPrice, 0, MidpointRounding.AwayFromZero);
        SelectedInvoice.TotalAmount = SelectedInvoice.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();
        await LoadInvoicesAsync();
        SelectedInvoice = Invoices.FirstOrDefault(x => x.Id == selectedId);
        SetIsEditingNewInvoiceSession(false);
    }

    [RelayCommand]
    private async Task SaveAndBackToListAsync()
    {
        await SaveAsync();
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var invoiceId = SelectedInvoice?.Id;
        if (invoiceId is null) return;
        var tracked = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId.Value);
        if (tracked is null) return;
        _db.Invoices.Remove(tracked);
        await _db.SaveChangesAsync();
        await LoadInvoicesAsync();
        SelectedInvoice = null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedId = SelectedInvoice?.Id;
        await LoadAsync();
        if (selectedId is not null)
            SelectedInvoice = Invoices.FirstOrDefault(x => x.Id == selectedId.Value);
    }

    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (SelectedInvoice is null) return;
        var description = string.IsNullOrWhiteSpace(NewLineDescription) ? "Položka" : NewLineDescription.Trim();
        var qty = NewLineQuantity <= 0 ? 1 : NewLineQuantity;
        var price = NewLineUnitPrice < 0 ? 0 : NewLineUnitPrice;
        var tax = NewLineTaxRatePercent < 0 ? 0 : NewLineTaxRatePercent;
        var l = new InvoiceLine
        {
            InvoiceId = SelectedInvoice.Id,
            Description = description,
            Quantity = qty,
            UnitPrice = price,
            TaxRatePercent = tax,
            LineTotal = Math.Round(qty * price, 0, MidpointRounding.AwayFromZero)
        };
        _db.InvoiceLines.Add(l);
        await _db.SaveChangesAsync();
        await LoadInvoicesAsync();
        SelectedInvoice = Invoices.FirstOrDefault(x => x.Id == l.InvoiceId);
        NewLineDescription = "Položka";
        NewLineQuantity = 1m;
        NewLineUnitPrice = 0m;
        NewLineTaxRatePercent = _defaultVatRatePercent;
    }

    partial void OnSelectedInvoiceChanged(Invoice? value)
    {
        SelectedInvoiceLine = null;
        RemoveSelectedInvoiceLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedInvoiceLineChanged(InvoiceLine? value) =>
        RemoveSelectedInvoiceLineCommand.NotifyCanExecuteChanged();

    private bool CanRemoveSelectedInvoiceLine() => SelectedInvoice is not null && SelectedInvoiceLine is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedInvoiceLine))]
    private async Task RemoveSelectedInvoiceLineAsync()
    {
        if (SelectedInvoice is null || SelectedInvoiceLine is null) return;
        var invoiceId = SelectedInvoice.Id;
        var lineId = SelectedInvoiceLine.Id;

        var inv = await _db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId);
        if (inv is null) return;
        var trackedLine = inv.Lines.FirstOrDefault(l => l.Id == lineId);
        if (trackedLine is null) return;

        inv.Lines.Remove(trackedLine);
        foreach (var l in inv.Lines)
            l.LineTotal = Math.Round(l.Quantity * l.UnitPrice, 0, MidpointRounding.AwayFromZero);
        inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();

        await LoadInvoicesAsync();
        SelectedInvoice = Invoices.FirstOrDefault(x => x.Id == invoiceId);
        SelectedInvoiceLine = null;
    }

    [RelayCommand]
    private void OpenSelectedForEdit()
    {
        if (SelectedInvoice is null) return;
        SetIsEditingNewInvoiceSession(false);
        WizardStepIndex = 1;
    }

    [RelayCommand]
    private async Task PreviewSelectedInvoiceAsync()
    {
        if (SelectedInvoice is null) return;
        var preview = await BuildInvoicePreviewWindowAsync(SelectedInvoice, false, "Náhled");
        preview.ShowDialog();
    }

    partial void OnNewLineQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(NewLineTotalWithoutVat));
        OnPropertyChanged(nameof(NewLineTotalWithVat));
    }

    partial void OnNewLineUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(NewLineUnitPriceWithVat));
        OnPropertyChanged(nameof(NewLineTotalWithoutVat));
        OnPropertyChanged(nameof(NewLineTotalWithVat));
    }

    partial void OnNewLineTaxRatePercentChanged(decimal value)
    {
        OnPropertyChanged(nameof(NewLineUnitPriceWithVat));
        OnPropertyChanged(nameof(NewLineTotalWithVat));
    }

    partial void OnCompactTablesChanged(bool value)
    {
        OnPropertyChanged(nameof(TableRowHeight));
    }

    private void OpenOrderSelectionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderSelectionItem.IsSelected))
            OnPropertyChanged(nameof(SelectedOpenOrdersCountText));
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (SelectedInvoice is null) return;
        var preview = await BuildInvoicePreviewWindowAsync(SelectedInvoice, true, "Exportovat PDF");
        if (preview.ShowDialog() != true) return;
        var path = await SaveInvoicePdfAndOpenAsync(SelectedInvoice);
        AppDialog.ShowInfo($"PDF uloženo:\n{path}", "Export");
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (SelectedInvoice is null) return;
        var dir = await GetExportPathAsync("Export.InvoicesCsvPath", "Export");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"faktura_{SelectedInvoice.Number.Replace('/', '-')}.csv");
        await using (var fs = File.Create(path))
            await _csv.WriteInvoiceCsvAsync(SelectedInvoice, fs);
        AppDialog.ShowInfo($"CSV uloženo:\n{path}", "Export");
    }

    private async Task<string> GetSettingAsync(string key, string fallback)
    {
        var v = await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    private async Task<string> GetDataRootPathAsync()
    {
        var custom = await GetSettingAsync("App.DataRootPath", "");
        var candidate = string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PrintCalc")
            : custom;
        return EnsureWritableDirectory(candidate, GetSafeFallbackRoot());
    }

    private async Task<string> GetExportPathAsync(string key, string fallbackFolder)
    {
        var custom = await GetSettingAsync(key, "");
        var preferred = !string.IsNullOrWhiteSpace(custom)
            ? custom
            : Path.Combine(await GetDataRootPathAsync(), fallbackFolder);
        return EnsureWritableDirectory(preferred, Path.Combine(GetSafeFallbackRoot(), fallbackFolder));
    }

    private static string GetSafeFallbackRoot()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs))
            return Path.Combine(docs, "PrintCalc");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintCalc");
    }

    private static string EnsureWritableDirectory(string preferredPath, string fallbackPath)
    {
        try
        {
            Directory.CreateDirectory(preferredPath);
            return preferredPath;
        }
        catch (UnauthorizedAccessException)
        {
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }
        catch (IOException)
        {
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }
    }

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal fallback)
    {
        var raw = await GetSettingAsync(key, "");
        return decimal.TryParse(raw.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val)
            ? val
            : fallback;
    }

    partial void OnCreateAsDetailedFromOrdersChanged(bool value)
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
                .Where(x => x.Key == "Invoices.CreateAsDetailedFromOrders")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            CreateAsDetailedFromOrders = value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task SaveUiSettingsAsync(bool value)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Invoices.CreateAsDetailedFromOrders");
        if (row is null)
        {
            row = new AppSettingsRow { Key = "Invoices.CreateAsDetailedFromOrders" };
            _db.AppSettings.Add(row);
        }
        row.Value = value ? "true" : "false";
        await _db.SaveChangesAsync();
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
    public string EditSaveButtonText => _isEditingNewInvoiceSession ? "Uložit" : "Uložit změny";
    public string WizardStepTitle => WizardStepIndex switch
    {
        0 => "Krok 1/3 - Seznam faktur",
        2 => "Krok 2/3 - Nová faktura ze zakázek",
        _ => "Krok 3/3 - Editace faktury"
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
            await FromOrderAsync();
            return;
        }

        if (SelectedInvoice is not null)
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

    [RelayCommand]
    private void StartNewInvoice()
    {
        WizardStepIndex = 2;
    }

    [RelayCommand]
    private void BackToList()
    {
        SetIsEditingNewInvoiceSession(false);
        WizardStepIndex = 0;
    }

    private void SetIsEditingNewInvoiceSession(bool value)
    {
        if (_isEditingNewInvoiceSession == value) return;
        _isEditingNewInvoiceSession = value;
        OnPropertyChanged(nameof(EditSaveButtonText));
    }

    private static void TryOpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // Nechceme blokovat export, když systém nepůjde otevřít viewer.
        }
    }

    private async Task<string> SaveInvoicePdfAndOpenAsync(Invoice invoice)
    {
        var dir = await GetExportPathAsync("Export.InvoicesPdfPath", "Faktury");
        var path = await _pdf.SaveInvoicePdfAsync(invoice, dir);
        TryOpenFile(path);
        return path;
    }

    private async Task<InvoicePreviewWindow> BuildInvoicePreviewWindowAsync(Invoice invoice, bool showConfirmButton, string confirmButtonText)
    {
        var supplierName = await GetSettingAsync("Company.Name", "Moje 3D firma");
        var supplierAddress = await GetSettingAsync("Company.Address", "");
        var supplierIco = await GetSettingAsync("Company.Ico", "");
        var supplierDic = await GetSettingAsync("Company.Dic", "");
        var supplierEmail = await GetSettingAsync("Company.Email", "");
        var supplierPhone = await GetSettingAsync("Company.Phone", "");
        var paymentMethod = invoice.PaymentMethod ?? await GetSettingAsync("Company.PaymentMethod", "Převodem");
        var iban = await GetSettingAsync("Company.Iban", "");
        var swift = await GetSettingAsync("Company.Swift", "");
        var bankAccount = await GetSettingAsync("Company.BankAccount", "");
        var logoPath = await GetSettingAsync("Company.LogoPath", "");
        var visualStyle = await GetSettingAsync("Company.DocumentVisualStyle", "Phoenix");
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == invoice.CustomerId);

        return new InvoicePreviewWindow(
            invoice,
            supplierName,
            supplierAddress,
            supplierIco,
            supplierDic,
            supplierEmail,
            supplierPhone,
            paymentMethod,
            iban,
            swift,
            bankAccount,
            logoPath,
            visualStyle,
            customer?.Name ?? $"Zákazník #{invoice.CustomerId}",
            $"{customer?.Street} {customer?.Zip} {customer?.City}".Trim(),
            $"{customer?.Email} {customer?.Phone}".Trim(),
            customer?.CompanyId ?? "",
            customer?.VatId ?? "",
            showConfirmButton,
            confirmButtonText)
        { Owner = Application.Current?.MainWindow };
    }

    private async Task LoadInvoiceDefaultsFromCompanyAsync()
    {
        if (string.IsNullOrWhiteSpace(NewInvoicePaymentMethod))
            NewInvoicePaymentMethod = await GetSettingAsync("Company.PaymentMethod", "Převodem");
        if (string.IsNullOrWhiteSpace(NewInvoiceDueDays))
            NewInvoiceDueDays = "14";
    }

    private async Task LoadDefaultsFromSelectedOrderCustomerAsync(int? selectedOrderId)
    {
        if (selectedOrderId is null) return;
        var customerDefaults = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == selectedOrderId.Value)
            .Join(
                _db.Customers.AsNoTracking(),
                o => o.CustomerId,
                c => c.Id,
                (_, c) => new { c.InvoiceDueDays, c.PreferredPaymentMethod })
            .FirstOrDefaultAsync();
        if (customerDefaults is null) return;

        if (customerDefaults.InvoiceDueDays is > 0)
            NewInvoiceDueDays = customerDefaults.InvoiceDueDays.Value.ToString();
        if (!string.IsNullOrWhiteSpace(customerDefaults.PreferredPaymentMethod))
            NewInvoicePaymentMethod = customerDefaults.PreferredPaymentMethod!;
    }

    private static int ParseDueDaysOrDefault(string? value)
    {
        if (int.TryParse(value, out var dueDays) && dueDays > 0)
            return dueDays;
        return 14;
    }
}

public partial class OrderSelectionItem : ObservableObject
{
    public OrderSelectionItem(Order order)
    {
        Id = order.Id;
        Number = order.Number;
        Title = order.Title;
        Status = order.Status.ToString();
        CreatedAt = order.CreatedAt;
        TotalAmount = order.TotalAmount;
        CustomerId = order.CustomerId;
    }

    public int Id { get; }
    public int CustomerId { get; }
    public string Number { get; }
    public string Title { get; }
    public string Status { get; }
    public DateTime CreatedAt { get; }
    public decimal TotalAmount { get; }

    [ObservableProperty] private bool isSelected;
}

