using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
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

public partial class QuotesViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IQuotePdfService _quotePdf;
    private List<Calculation> _selectedCalculations = [];
    private bool _isLoadingSettings;
    private bool _isEditingNewQuoteSession;
    private bool _uiSettingsLoaded;

    public ObservableCollection<Quote> Quotes { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Calculation> AvailableCalculations { get; } = new();

    [ObservableProperty] private Quote? selectedQuote;
    [ObservableProperty] private QuoteLine? selectedQuoteLine;
    [ObservableProperty] private int? selectedCustomerId;
    [ObservableProperty] private int wizardStepIndex = 0;
    [ObservableProperty] private string newLineDescription = "Položka";
    [ObservableProperty] private decimal newLineQuantity = 1m;
    [ObservableProperty] private decimal newLineUnitPrice = 0m;
    [ObservableProperty] private bool createAsDetailedCalculation = true;
    [ObservableProperty] private bool compactTables;

    public QuotesViewModel(AppDbContext db, IDocumentNumberService numbers, IQuotePdfService quotePdf)
    {
        _db = db;
        _numbers = numbers;
        _quotePdf = quotePdf;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_uiSettingsLoaded)
        {
            await LoadUiSettingsAsync();
            _uiSettingsLoaded = true;
        }

        await LoadLookupDataAsync();
        await LoadAvailableCalculationsAsync();
        await LoadQuotesAsync();
    }

    private async Task LoadLookupDataAsync()
    {
        _db.ChangeTracker.Clear();
        Customers.Clear();
        foreach (var c in await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
            Customers.Add(c);
    }

    private async Task LoadAvailableCalculationsAsync()
    {
        AvailableCalculations.Clear();
        var calcQuery = _db.Calculations.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(300);
        if (SelectedCustomerId is { } cid)
            calcQuery = (IOrderedQueryable<Calculation>)calcQuery.Where(c => c.CustomerId == cid).OrderByDescending(c => c.CreatedAt);
        foreach (var calc in await calcQuery.ToListAsync())
            AvailableCalculations.Add(calc);
    }

    private async Task LoadQuotesAsync()
    {
        var list = await _db.Quotes
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.IssueDate)
            .ToListAsync();
        await DispatcherObservableRefresh.ReplaceAsync(Quotes, list);
    }

    [RelayCommand]
    private async Task NewQuoteAsync()
    {
        var calcs = await _db.Calculations.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(300)
            .ToListAsync();
        if (SelectedCustomerId is { } cidFilter)
            calcs = calcs.Where(c => c.CustomerId == cidFilter).ToList();

        var picker = new NewQuoteFromCalculationsWindow(calcs)
        {
            Owner = Application.Current?.MainWindow
        };
        if (picker.ShowDialog() != true) return;

        var selectedIds = picker.SelectedCalculationIds;
        if (selectedIds.Count == 0)
            return;

        var selectedCalcs = await _db.Calculations.AsNoTracking()
            .Where(c => selectedIds.Contains(c.Id))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        CreateAsDetailedCalculation = picker.CreateAsDetailedCalculation;
        await CreateQuoteFromCalculationsInternalAsync(selectedCalcs, CreateAsDetailedCalculation);
    }

    [RelayCommand]
    private async Task FromLastCalculationAsync()
    {
        var calc = await _db.Calculations.AsNoTracking().OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (calc is null) return;
        var cid = calc.CustomerId ?? SelectedCustomerId;
        if (cid is null) return;
        var num = await _numbers.NextQuoteNumberAsync();
        var q = new Quote
        {
            CustomerId = cid.Value,
            Number = num,
            Title = calc.Title,
            IssueDate = DateTime.UtcNow,
            Status = QuoteStatus.Draft,
            SourceCalculationId = calc.Id,
            TotalAmount = calc.TotalWithMargin
        };
        QuoteFromCalculationHelper.AddDetailedLines(q, calc);
        q.TotalAmount = q.Lines.Sum(x => x.LineTotal);
        _db.Quotes.Add(q);
        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == q.Id);
    }

    private async Task SaveQuoteChangesAsync()
    {
        if (SelectedQuote is null) return;
        var selectedId = SelectedQuote.Id;
        foreach (var line in SelectedQuote.Lines)
            line.LineTotal = Math.Round(line.Quantity * line.UnitPrice, 0, MidpointRounding.AwayFromZero);
        SelectedQuote.TotalAmount = SelectedQuote.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();
        await LoadQuotesAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == selectedId);
        SetIsEditingNewQuoteSession(false);
    }

    /// <summary>Kontextové menu / rychlé uložení bez opuštění kroku editace.</summary>
    [RelayCommand]
    private async Task SaveAsync() => await SaveQuoteChangesAsync();

    /// <summary>Hlavní tlačítko Uložit v průvodci — po uložení návrat na seznam (stejně jako faktury).</summary>
    [RelayCommand]
    private async Task SaveAndBackToListAsync()
    {
        await SaveQuoteChangesAsync();
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task ApproveSelectedQuoteAsync()
    {
        if (SelectedQuote is null) return;
        var quote = await _db.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == SelectedQuote.Id);
        if (quote is null) return;
        quote.Status = QuoteStatus.Accepted;

        var existing = await _db.Orders.FirstOrDefaultAsync(o => o.QuoteId == quote.Id);
        if (existing is null)
        {
            var num = await _numbers.NextOrderNumberAsync();
            var order = new Order
            {
                CustomerId = quote.CustomerId,
                Number = num,
                Title = quote.Title,
                QuoteId = quote.Id,
                TotalAmount = quote.TotalAmount,
                Status = OrderStatus.Confirmed
            };
            foreach (var l in quote.Lines)
            {
                order.Lines.Add(new OrderLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    SourceCalculationId = l.SourceCalculationId
                });
            }
            order.Title = DocumentTitleExcerpt.ForOrderGridCaption(order);
            _db.Orders.Add(order);
        }

        await _db.SaveChangesAsync();
        await LoadQuotesAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == quote.Id);
        AppDialog.ShowInfo("Nabídka schválena. Zakázka byla automaticky vytvořena.", "Schválení nabídky");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var quoteId = SelectedQuote?.Id;
        if (quoteId is null) return;
        var tracked = await _db.Quotes.FirstOrDefaultAsync(x => x.Id == quoteId.Value);
        if (tracked is null) return;
        _db.Quotes.Remove(tracked);
        await _db.SaveChangesAsync();
        await LoadQuotesAsync();
        SelectedQuote = null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedId = SelectedQuote?.Id;
        await LoadAsync();
        if (selectedId is not null)
            SelectedQuote = Quotes.FirstOrDefault(x => x.Id == selectedId.Value);
    }

    partial void OnSelectedCustomerIdChanged(int? value) => _ = LoadAvailableCalculationsAsync();
    partial void OnCreateAsDetailedCalculationChanged(bool value)
    {
        if (_isLoadingSettings) return;
        _ = SaveUiSettingsAsync(value);
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
    public string EditSaveButtonText => _isEditingNewQuoteSession ? "Uložit" : "Uložit změny";
    public double TableRowHeight => CompactTables ? 30 : 42;
    public string WizardStepTitle => WizardStepIndex switch
    {
        0 => "Krok 1/3 - Seznam nabídek",
        2 => "Krok 2/3 - Nová nabídka z kalkulací",
        _ => "Krok 3/3 - Editace nabídky"
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
            await CreateFromSelectedCalculationsAsync();
            if (SelectedQuote is not null)
            {
                SetIsEditingNewQuoteSession(true);
                WizardStepIndex = 1;
            }
            return;
        }

        if (SelectedQuote is not null)
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
    private void StartNewQuote()
    {
        WizardStepIndex = 2;
    }

    [RelayCommand]
    private void BackToList()
    {
        SetIsEditingNewQuoteSession(false);
        WizardStepIndex = 0;
    }

    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (SelectedQuote is null) return;
        var description = string.IsNullOrWhiteSpace(NewLineDescription) ? "Položka" : NewLineDescription.Trim();
        var qty = NewLineQuantity <= 0 ? 1 : NewLineQuantity;
        var price = NewLineUnitPrice < 0 ? 0 : NewLineUnitPrice;
        var l = new QuoteLine
        {
            QuoteId = SelectedQuote.Id,
            Description = description,
            Quantity = qty,
            UnitPrice = price,
            LineTotal = Math.Round(qty * price, 0, MidpointRounding.AwayFromZero)
        };
        _db.QuoteLines.Add(l);
        await _db.SaveChangesAsync();
        await LoadQuotesAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == l.QuoteId);
        NewLineDescription = "Položka";
        NewLineQuantity = 1m;
        NewLineUnitPrice = 0m;
    }

    partial void OnSelectedQuoteChanged(Quote? value)
    {
        SelectedQuoteLine = null;
        RemoveSelectedQuoteLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedQuoteLineChanged(QuoteLine? value) =>
        RemoveSelectedQuoteLineCommand.NotifyCanExecuteChanged();

    private bool CanRemoveSelectedQuoteLine() => SelectedQuote is not null && SelectedQuoteLine is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedQuoteLine))]
    private async Task RemoveSelectedQuoteLineAsync()
    {
        if (SelectedQuote is null || SelectedQuoteLine is null) return;
        var quoteId = SelectedQuote.Id;
        var lineId = SelectedQuoteLine.Id;

        var q = await _db.Quotes.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == quoteId);
        if (q is null) return;
        var trackedLine = q.Lines.FirstOrDefault(l => l.Id == lineId);
        if (trackedLine is null) return;

        q.Lines.Remove(trackedLine);
        foreach (var l in q.Lines)
            l.LineTotal = Math.Round(l.Quantity * l.UnitPrice, 0, MidpointRounding.AwayFromZero);
        q.TotalAmount = q.Lines.Sum(l => l.LineTotal);
        await _db.SaveChangesAsync();

        await LoadQuotesAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == quoteId);
        SelectedQuoteLine = null;
    }

    [RelayCommand]
    private void OpenSelectedForEdit()
    {
        if (SelectedQuote is null) return;
        SetIsEditingNewQuoteSession(false);
        WizardStepIndex = 1;
    }

    [RelayCommand]
    private async Task CreateFromSelectedCalculationsAsync()
    {
        await CreateQuoteFromCalculationsInternalAsync(_selectedCalculations, CreateAsDetailedCalculation);
    }

    [RelayCommand]
    private async Task ExportSelectedPdfAsync()
    {
        if (SelectedQuote is null) return;
        var supplierName = await GetSettingAsync("Company.Name", "Moje 3D firma");
        var supplierAddress = await GetSettingAsync("Company.Address", "");
        var supplierIco = await GetSettingAsync("Company.Ico", "");
        var supplierDic = await GetSettingAsync("Company.Dic", "");
        var supplierEmail = await GetSettingAsync("Company.Email", "");
        var supplierPhone = await GetSettingAsync("Company.Phone", "");
        var paymentMethod = await GetSettingAsync("Company.PaymentMethod", "Převodem");
        var iban = await GetSettingAsync("Company.Iban", "");
        var swift = await GetSettingAsync("Company.Swift", "");
        var logoPath = await GetSettingAsync("Company.LogoPath", "");
        var visualStyle = await GetSettingAsync("Company.DocumentVisualStyle", "Phoenix");

        var preview = new QuotePreviewWindow(
            SelectedQuote,
            supplierName,
            supplierAddress,
            supplierIco,
            supplierDic,
            supplierEmail,
            supplierPhone,
            paymentMethod,
            iban,
            swift,
            logoPath,
            visualStyle)
        {
            Owner = Application.Current?.MainWindow
        };
        if (preview.ShowDialog() != true) return;
        var dir = await GetExportPathAsync("Export.QuotesPdfPath", "Nabidky");
        var path = await _quotePdf.SaveQuotePdfAsync(SelectedQuote, dir);
        AppDialog.ShowInfo($"PDF uloženo:\n{path}", "Náhled nabídky");
    }

    public void SetSelectedCalculations(IEnumerable<Calculation> calculations) =>
        _selectedCalculations = calculations.Where(c => c is not null).DistinctBy(c => c.Id).ToList();

    private async Task CreateQuoteFromCalculationsInternalAsync(IEnumerable<Calculation> calculations, bool detailedMode)
    {
        var selected = calculations
            .Where(c => c.CustomerId is not null)
            .GroupBy(c => c.CustomerId!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (selected is null || selected.Count() == 0)
        {
            AppDialog.ShowInfo("Vyberte kalkulace se stejným zákazníkem.", "Nová nabídka");
            return;
        }

        var cid = selected.Key;
        var num = await _numbers.NextQuoteNumberAsync();
        var items = selected.OrderBy(c => c.CreatedAt).ToList();
        var q = new Quote
        {
            CustomerId = cid,
            Number = num,
            Title = items.Count == 1 ? items[0].Title : DocumentTitleExcerpt.FromCalculationTitles(items),
            IssueDate = DateTime.UtcNow,
            Status = QuoteStatus.Draft,
            TotalAmount = 0
        };

        foreach (var c in items)
        {
            if (detailedMode)
            {
                QuoteFromCalculationHelper.AddDetailedLines(q, c);
            }
            else
            {
                var label = string.IsNullOrWhiteSpace(c.Title) ? $"Kalkulace #{c.Id}" : c.Title.Trim();
                q.Lines.Add(new QuoteLine
                {
                    SourceCalculationId = c.Id,
                    Description = QuoteFromCalculationHelper.BuildPrintLineDescription(c, label),
                    Quantity = 1,
                    UnitPrice = c.TotalWithMargin,
                    LineTotal = c.TotalWithMargin
                });
            }
        }
        q.TotalAmount = q.Lines.Sum(x => x.LineTotal);

        _db.Quotes.Add(q);
        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedQuote = Quotes.FirstOrDefault(x => x.Id == q.Id);
    }

    private void SetIsEditingNewQuoteSession(bool value)
    {
        if (_isEditingNewQuoteSession == value) return;
        _isEditingNewQuoteSession = value;
        OnPropertyChanged(nameof(EditSaveButtonText));
    }

    private async Task<string> GetSettingAsync(string key, string fallback)
    {
        var v = await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    private async Task LoadUiSettingsAsync()
    {
        _isLoadingSettings = true;
        try
        {
            var value = await _db.AppSettings.AsNoTracking()
                .Where(x => x.Key == "Quotes.CreateAsDetailedCalculation")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            CreateAsDetailedCalculation = value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task SaveUiSettingsAsync(bool value)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Quotes.CreateAsDetailedCalculation");
        if (row is null)
        {
            row = new AppSettingsRow { Key = "Quotes.CreateAsDetailedCalculation" };
            _db.AppSettings.Add(row);
        }
        row.Value = value ? "true" : "false";
        await _db.SaveChangesAsync();
    }

    private async Task<string> GetDataRootPathAsync()
    {
        var custom = await GetSettingAsync("App.DataRootPath", "");
        return string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PrintCalc")
            : custom;
    }

    private async Task<string> GetExportPathAsync(string key, string fallbackFolder)
    {
        var custom = await GetSettingAsync(key, "");
        if (!string.IsNullOrWhiteSpace(custom))
            return custom;
        return Path.Combine(await GetDataRootPathAsync(), fallbackFolder);
    }

}

