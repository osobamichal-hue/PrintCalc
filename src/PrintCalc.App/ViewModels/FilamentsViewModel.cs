using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using PrintCalc.App.Services;
using PrintCalc.App.Views;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using ZXing.Windows.Compatibility;

namespace PrintCalc.App.ViewModels;

public partial class FilamentsViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly IStockService _stock;

    public ObservableCollection<FilamentType> Types { get; } = new();
    public ObservableCollection<FilamentStock> Stocks { get; } = new();
    public ObservableCollection<StockMovement> Movements { get; } = new();

    [ObservableProperty] private FilamentType? selectedType;
    [ObservableProperty] private FilamentStock? selectedStock;
    [ObservableProperty] private string newTypeName = "PLA";
    [ObservableProperty] private string newManufacturer = "";
    [ObservableProperty] private decimal newDiameterMm = 1.75m;
    [ObservableProperty] private string newColor = "";
    [ObservableProperty] private decimal newDensityGPerCm3 = 1.24m;
    [ObservableProperty] private int? newNozzleTempMinC;
    [ObservableProperty] private int? newNozzleTempMaxC;
    [ObservableProperty] private int? newBedTempMinC;
    [ObservableProperty] private int? newBedTempMaxC;
    [ObservableProperty] private string newNotes = "";
    [ObservableProperty] private decimal newMinStockKg;
    [ObservableProperty] private int? editingTypeId;
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private decimal selectedTypeStockKg;
    [ObservableProperty] private int selectedTypeStockPieces;
    [ObservableProperty] private bool showAllStockCards = true;

    [ObservableProperty] private decimal receiveWeightKg = 1;
    [ObservableProperty] private decimal receivePricePerKg = 400;
    [ObservableProperty] private int? receiveTypeId;
    [ObservableProperty] private string receiveSupplier = "";
    [ObservableProperty] private int receivePieces = 1;
    [ObservableProperty] private string receiveLotNumber = "";
    [ObservableProperty] private DateTime? receiveExpirationDate;
    [ObservableProperty] private string receiveStockNotes = "";
    [ObservableProperty] private bool showOnlyActiveStocks = true;
    [ObservableProperty] private decimal issueWeightKg = 0.1m;
    [ObservableProperty] private string issueOrderReference = "";
    [ObservableProperty] private string issueNote = "";
    [ObservableProperty] private int wizardStepIndex;

    public FilamentsViewModel(AppDbContext db, IStockService stock)
    {
        _db = db;
        _stock = stock;
        _ = LoadTypesAsync();
    }

    private async Task LoadTypesAsync()
    {
        _db.ChangeTracker.Clear();
        Types.Clear();
        foreach (var t in await _db.FilamentTypes.OrderBy(x => x.Name).ToListAsync())
            Types.Add(t);
        if (ReceiveTypeId is not null && Types.All(t => t.Id != ReceiveTypeId.Value))
            ReceiveTypeId = null;
        if (ReceiveTypeId is null && SelectedType is not null)
            ReceiveTypeId = SelectedType.Id;
        await LoadStocksAsync();
        await LoadMovementsAsync();
    }

    private async Task LoadMovementsAsync()
    {
        Movements.Clear();
        var list = await _db.StockMovements.AsNoTracking()
            .Include(x => x.FilamentType)
            .OrderByDescending(x => x.OccurredAt)
            .Take(500)
            .ToListAsync();
        foreach (var m in list)
            Movements.Add(m);
    }

    partial void OnWizardStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevStep));
        OnPropertyChanged(nameof(CanGoNextStep));
        OnPropertyChanged(nameof(WizardStepTitle));
    }

    public bool CanGoPrevStep => WizardStepIndex > 0;
    public bool CanGoNextStep => WizardStepIndex < 1;
    public string WizardStepTitle => WizardStepIndex switch
    {
        0 => "Krok 1/2 - Skladové karty",
        _ => "Krok 2/2 - Sklad"
    };

    [RelayCommand]
    private void PrevStep()
    {
        if (WizardStepIndex > 0) WizardStepIndex--;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (WizardStepIndex < 1) WizardStepIndex++;
    }

    [RelayCommand]
    private void GoToStep(object? index)
    {
        int parsed;
        if (index is int i) parsed = i;
        else if (index is string s && int.TryParse(s, out var si)) parsed = si;
        else return;
        if (parsed < 0 || parsed > 1) return;
        WizardStepIndex = parsed;
    }

    partial void OnSelectedTypeChanged(FilamentType? value)
    {
        if (value is not null)
            ReceiveTypeId = value.Id;
        _ = LoadStocksAsync();
    }
    partial void OnShowAllStockCardsChanged(bool value) => _ = LoadStocksAsync();

    private async Task LoadStocksAsync()
    {
        Stocks.Clear();
        if (!ShowAllStockCards && SelectedType is null)
        {
            SelectedTypeStockKg = 0;
            SelectedTypeStockPieces = 0;
            return;
        }
        var query = _db.FilamentStocks.Include(x => x.FilamentType).AsQueryable();
        if (!ShowAllStockCards && SelectedType is not null)
            query = query.Where(x => x.FilamentTypeId == SelectedType.Id);
        if (ShowOnlyActiveStocks)
            query = query.Where(x => x.RemainingWeightKg > 0);
        var list = await query.OrderByDescending(x => x.ReceivedAt).ToListAsync();
        foreach (var s in list)
            Stocks.Add(s);
        if (SelectedType is null)
        {
            SelectedTypeStockKg = 0;
            SelectedTypeStockPieces = 0;
        }
        else
        {
            var selectedTypeList = await _db.FilamentStocks
                .Where(s => s.FilamentTypeId == SelectedType.Id)
                .ToListAsync();
            SelectedTypeStockKg = Math.Round(selectedTypeList.Sum(s => s.RemainingWeightKg), 3, MidpointRounding.AwayFromZero);
            SelectedTypeStockPieces = selectedTypeList.Sum(s => s.PieceCount);
        }
    }

    [RelayCommand]
    private async Task SaveTypeFromFormAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTypeName))
        {
            StatusMessage = "Vyplňte název typu filamentu.";
            return;
        }

        var editingId = EditingTypeId;
        var isEdit = editingId.HasValue;
        FilamentType t;
        if (isEdit)
        {
            t = await _db.FilamentTypes.FirstOrDefaultAsync(x => x.Id == editingId!.Value)
                ?? throw new InvalidOperationException("Upravovaný typ filamentu nebyl nalezen.");
        }
        else
        {
            t = new FilamentType { AveragePricePerKg = 0 };
            _db.FilamentTypes.Add(t);
        }

        t.Name = NewTypeName.Trim();
        t.Manufacturer = string.IsNullOrWhiteSpace(NewManufacturer) ? null : NewManufacturer.Trim();
        t.DiameterMm = NewDiameterMm;
        t.Color = string.IsNullOrWhiteSpace(NewColor) ? null : NewColor.Trim();
        t.DensityGPerCm3 = NewDensityGPerCm3;
        t.NozzleTempMinC = NewNozzleTempMinC;
        t.NozzleTempMaxC = NewNozzleTempMaxC;
        t.BedTempMinC = NewBedTempMinC;
        t.BedTempMaxC = NewBedTempMaxC;
        t.Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim();
        t.MinStockKg = Math.Max(0, NewMinStockKg);

        await _db.SaveChangesAsync();
        await LoadTypesAsync();
        SelectedType = Types.FirstOrDefault(x => x.Id == t.Id);
        EditingTypeId = null;
        ClearTypeForm();
        StatusMessage = isEdit ? "Typ filamentu byl upraven." : "Typ filamentu byl přidán.";
    }

    [RelayCommand]
    private void OpenSelectedTypeForEdit()
    {
        if (SelectedType is null)
            return;

        NewTypeName = SelectedType.Name;
        NewManufacturer = SelectedType.Manufacturer ?? "";
        NewDiameterMm = SelectedType.DiameterMm;
        NewColor = SelectedType.Color ?? "";
        NewDensityGPerCm3 = SelectedType.DensityGPerCm3;
        NewNozzleTempMinC = SelectedType.NozzleTempMinC;
        NewNozzleTempMaxC = SelectedType.NozzleTempMaxC;
        NewBedTempMinC = SelectedType.BedTempMinC;
        NewBedTempMaxC = SelectedType.BedTempMaxC;
        NewNotes = SelectedType.Notes ?? "";
        NewMinStockKg = SelectedType.MinStockKg;
        EditingTypeId = SelectedType.Id;
        StatusMessage = $"Editace typu: {SelectedType.Name}";
    }

    public string SaveTypeButtonText => EditingTypeId.HasValue ? "Uložit typ" : "Přidat typ";

    partial void OnEditingTypeIdChanged(int? value)
    {
        OnPropertyChanged(nameof(SaveTypeButtonText));
    }

    [RelayCommand]
    private async Task AddTypeAsync()
    {
        await SaveTypeFromFormAsync();
    }

    [RelayCommand]
    private async Task DeleteTypeAsync()
    {
        var t = SelectedType;
        if (t is null) return;
        var id = t.Id;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var movements = await _db.StockMovements.Where(m => m.FilamentTypeId == id).ToListAsync();
            _db.StockMovements.RemoveRange(movements);

            var stocks = await _db.FilamentStocks.Where(s => s.FilamentTypeId == id).ToListAsync();
            _db.FilamentStocks.RemoveRange(stocks);

            var printerLinks = await _db.PrinterFilamentTypes.Where(p => p.FilamentTypeId == id).ToListAsync();
            _db.PrinterFilamentTypes.RemoveRange(printerLinks);

            await _db.Calculations
                .Where(c => c.FilamentTypeId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FilamentTypeId, (int?)null));

            var tracked = await _db.FilamentTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (tracked is not null)
                _db.FilamentTypes.Remove(tracked);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        Types.Remove(t);
        SelectedType = null;
        Stocks.Clear();
        await LoadMovementsAsync();
        StatusMessage = "Typ filamentu byl smazán.";
    }

    [RelayCommand]
    private async Task SaveTypesAsync()
    {
        await _db.SaveChangesAsync();
        StatusMessage = "Změny byly uloženy.";
    }

    [RelayCommand]
    private async Task ReceiveAsync()
    {
        var id = ReceiveTypeId ?? SelectedType?.Id;
        if (id is null)
        {
            StatusMessage = "Vyberte typ filamentu pro příjem.";
            return;
        }
        if (!await _db.FilamentTypes.AsNoTracking().AnyAsync(x => x.Id == id.Value))
            return;
        await _stock.ReceiveAsync(id.Value, ReceiveWeightKg, ReceivePricePerKg,
            string.IsNullOrWhiteSpace(ReceiveSupplier) ? null : ReceiveSupplier,
            ReceivePieces,
            string.IsNullOrWhiteSpace(ReceiveLotNumber) ? null : ReceiveLotNumber.Trim(),
            ReceiveExpirationDate,
            string.IsNullOrWhiteSpace(ReceiveStockNotes) ? null : ReceiveStockNotes.Trim());
        await LoadTypesAsync();
        SelectedType = Types.FirstOrDefault(x => x.Id == id.Value);
        ReceiveTypeId = id.Value;
        StatusMessage = "Skladová položka byla naskladněna.";
        ReceiveLotNumber = "";
        ReceiveExpirationDate = null;
        ReceiveStockNotes = "";
        await LoadMovementsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadTypesAsync();
        StatusMessage = "Data filamentů byla obnovena.";
    }

    [RelayCommand]
    private async Task SaveSelectedStockCardAsync()
    {
        if (SelectedStock is null) return;
        var selectedId = SelectedStock.Id;
        await _db.SaveChangesAsync();
        StatusMessage = "Skladová karta byla uložena.";
        await LoadStocksAsync();
        SelectedStock = Stocks.FirstOrDefault(s => s.Id == selectedId);
    }

    [RelayCommand]
    private async Task IssueAsync()
    {
        var filamentTypeId = SelectedType?.Id ?? SelectedStock?.FilamentTypeId;
        if (filamentTypeId is null)
        {
            StatusMessage = "Vyberte skladovou kartu nebo typ filamentu.";
            return;
        }
        if (IssueWeightKg <= 0)
        {
            StatusMessage = "Zadejte množství pro výdej (kg).";
            return;
        }
        var noteParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(IssueOrderReference))
            noteParts.Add($"Zakázka: {IssueOrderReference.Trim()}");
        if (!string.IsNullOrWhiteSpace(IssueNote))
            noteParts.Add(IssueNote.Trim());
        var note = noteParts.Count == 0 ? "Výdej ze skladu" : string.Join(" | ", noteParts);
        await _stock.IssueAsync(filamentTypeId.Value, IssueWeightKg, note);
        await LoadTypesAsync();
        SelectedType = Types.FirstOrDefault(x => x.Id == filamentTypeId.Value);
        StatusMessage = "Výdej ze skladu byl zapsán.";
        IssueNote = "";
    }

    [RelayCommand]
    private void OpenReceiveForm()
    {
        var window = new ReceiveStockWindow(this)
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenIssueFormFromSelectedStock()
    {
        if (SelectedStock is null && SelectedType is null)
        {
            StatusMessage = "Vyberte skladovou kartu nebo typ filamentu.";
            return;
        }

        if (SelectedStock is not null)
        {
            if (SelectedType?.Id != SelectedStock.FilamentTypeId)
                SelectedType = Types.FirstOrDefault(t => t.Id == SelectedStock.FilamentTypeId);
            if (SelectedStock.RemainingWeightKg > 0)
                IssueWeightKg = Math.Min(SelectedStock.RemainingWeightKg, IssueWeightKg <= 0 ? 0.1m : IssueWeightKg);
            if (string.IsNullOrWhiteSpace(IssueNote))
                IssueNote = $"Šarže: {SelectedStock.LotNumber}";
        }

        StatusMessage = "Vyplňte výdej ze skladu a potvrďte.";
        var window = new IssueStockWindow(this)
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void ClearTypeForm()
    {
        NewTypeName = "PLA";
        NewManufacturer = "";
        NewDiameterMm = 1.75m;
        NewColor = "";
        NewDensityGPerCm3 = 1.24m;
        NewNozzleTempMinC = null;
        NewNozzleTempMaxC = null;
        NewBedTempMinC = null;
        NewBedTempMaxC = null;
        NewNotes = "";
        NewMinStockKg = 0;
        EditingTypeId = null;
    }

    partial void OnShowOnlyActiveStocksChanged(bool value) => _ = LoadStocksAsync();

    partial void OnSelectedStockChanged(FilamentStock? value)
    {
        if (value is null) return;
        if (SelectedType?.Id != value.FilamentTypeId)
            SelectedType = Types.FirstOrDefault(t => t.Id == value.FilamentTypeId);
    }


    [RelayCommand]
    private async Task ImportQrImageAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Vyberte obrázek QR kódu",
            Filter = "Obrázky (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = false
        };
        if (ofd.ShowDialog() != true) return;

        await EnsureSelectedTypeAsync();
        if (SelectedType is null) return;

        string payload;
        try
        {
            using var bitmap = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(ofd.FileName);
            var reader = new BarcodeReader();
            var result = reader.Decode(bitmap);
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                AppDialog.ShowInfo("V obrázku nebyl nalezen QR kód.", "Import QR");
                return;
            }
            payload = result.Text;
        }
        catch (Exception ex)
        {
            AppDialog.ShowError($"Nepodařilo se načíst QR kód.\n{ex.Message}", "Import QR");
            return;
        }

        var changed = ApplyQrPayload(payload, SelectedType);
        await _db.SaveChangesAsync();
        await LoadTypesAsync();
        SelectedType = Types.FirstOrDefault(t => t.Id == SelectedType.Id);

        AppDialog.ShowInfo(
            changed
                ? "QR kód načten a data byla vyplněna."
                : "QR kód načten, ale nebyla rozpoznána mapovatelná data.",
            "Import QR");
    }

    private async Task EnsureSelectedTypeAsync()
    {
        if (SelectedType is not null) return;
        var t = new FilamentType { Name = "PLA", Manufacturer = "", AveragePricePerKg = 0 };
        _db.FilamentTypes.Add(t);
        await _db.SaveChangesAsync();
        Types.Add(t);
        SelectedType = t;
    }

    private bool ApplyQrPayload(string payload, FilamentType type)
    {
        var parsed = ParseKeyValues(payload);
        var changed = false;

        if (TryGet(parsed, out var name, "name", "nazev", "název", "material", "filament"))
        {
            type.Name = name;
            changed = true;
        }
        if (TryGet(parsed, out var manufacturer, "manufacturer", "vyrobce", "výrobce", "brand", "znacka", "značka"))
        {
            type.Manufacturer = manufacturer;
            changed = true;
        }
        if (TryGet(parsed, out var color, "color", "barva"))
        {
            type.Color = color;
            changed = true;
        }
        if (TryGetDecimal(parsed, out var diameter, "diameter", "diametermm", "prumer", "průměr", "ø", "d"))
        {
            type.DiameterMm = diameter;
            changed = true;
        }
        if (TryGetDecimal(parsed, out var density, "density", "hustota", "densitygcm3"))
        {
            type.DensityGPerCm3 = density;
            changed = true;
        }
        if (TryGetInt(parsed, out var nozzleMin, "nozzletempmin", "tryskamin", "nozzlemin", "tempmin", "tmin", "teplotamin", "nozzle", "tryska", "tryskatemp"))
        {
            type.NozzleTempMinC = nozzleMin;
            changed = true;
        }
        if (TryGetInt(parsed, out var nozzleMax, "nozzletempmax", "tryskamax", "nozzlemax"))
        {
            type.NozzleTempMaxC = nozzleMax;
            changed = true;
        }
        if (TryGetInt(parsed, out var bedMin, "bedtempmin", "bedmin", "podlozkamin", "bed", "podlozka", "bedtemp"))
        {
            type.BedTempMinC = bedMin;
            changed = true;
        }
        if (TryGetInt(parsed, out var bedMax, "bedtempmax", "bedmax", "podlozkamax", "teplotamax", "tmax"))
        {
            type.BedTempMaxC = bedMax;
            changed = true;
        }
        if (TryGetDecimal(parsed, out var avgPrice, "pricekg", "cena", "cenakg", "price", "price_per_kg", "costkg"))
        {
            type.AveragePricePerKg = avgPrice;
            ReceivePricePerKg = avgPrice;
            changed = true;
        }
        if (TryGetDecimal(parsed, out var weight, "weight", "hmotnost", "weightkg", "kg", "netweight"))
        {
            ReceiveWeightKg = weight;
            changed = true;
        }
        if (TryGet(parsed, out var supplier, "supplier", "dodavatel", "vendor"))
        {
            ReceiveSupplier = supplier;
            changed = true;
        }
        if (TryGetInt(parsed, out var pieces, "pieces", "ks", "spools", "count"))
        {
            ReceivePieces = pieces;
            changed = true;
        }

        if (!changed)
        {
            type.Notes = payload.Length > 1000 ? payload[..1000] : payload;
        }
        return changed;
    }

    private static Dictionary<string, string> ParseKeyValues(string payload)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = payload
            .Replace("\r", "\n")
            .Split(new[] { '\n', ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var idx = part.IndexOf(':');
            if (idx < 1) idx = part.IndexOf('=');
            if (idx < 1) continue;

            var key = NormalizeKey(part[..idx]);
            var value = part[(idx + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
            map[key] = value;
        }

        return map;
    }

    private static bool TryGet(Dictionary<string, string> map, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(NormalizeKey(key), out var v))
            {
                value = v.Trim();
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool TryGetDecimal(Dictionary<string, string> map, out decimal value, params string[] keys)
    {
        value = 0;
        if (!TryGet(map, out var raw, keys)) return false;
        raw = raw.Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("mm", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("g/cm3", "", StringComparison.OrdinalIgnoreCase)
                 .Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("cs-CZ"), out value)
               || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetInt(Dictionary<string, string> map, out int value, params string[] keys)
    {
        value = 0;
        if (!TryGet(map, out var raw, keys)) return false;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.GetCultureInfo("cs-CZ"), out value)
               || int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeKey(string input)
    {
        return input.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(":", "");
    }
}
