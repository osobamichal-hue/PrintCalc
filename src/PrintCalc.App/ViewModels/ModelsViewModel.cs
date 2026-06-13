using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class ModelsViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly IThreeMfReader _threeMf;
    private readonly IGcodeReader _gcode;

    public ObservableCollection<PrintModel> Items { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    [ObservableProperty] private PrintModel? selectedModel;
    [ObservableProperty] private int? selectedCustomerId;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string statusMessage = "";

    public ModelsViewModel(AppDbContext db, IThreeMfReader threeMf, IGcodeReader gcode)
    {
        _db = db;
        _threeMf = threeMf;
        _gcode = gcode;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db.ChangeTracker.Clear();
        Items.Clear();
        Customers.Clear();
        foreach (var c in await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
            Customers.Add(c);

        var query = _db.PrintModels.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim().ToLower(CultureInfo.InvariantCulture);
            query = query.Where(x =>
                x.Name.ToLower().Contains(s) ||
                (x.FilePath ?? "").ToLower().Contains(s) ||
                x.OriginalFileName.ToLower().Contains(s) ||
                (x.Notes ?? "").ToLower().Contains(s));
        }

        foreach (var m in await query
                     .Select(x => new PrintModel
                     {
                         Id = x.Id,
                         Name = x.Name,
                         FileType = x.FileType,
                         FilePath = x.FilePath,
                         OriginalFileName = x.OriginalFileName,
                         EstimatedMaterialGrams = x.EstimatedMaterialGrams,
                         EstimatedPrintHours = x.EstimatedPrintHours,
                         Notes = x.Notes,
                         CreatedAt = x.CreatedAt,
                         FileContent = Array.Empty<byte>()
                     })
                     .ToListAsync())
            Items.Add(m);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vyberte model (STL/3MF/GCode)",
            Filter = "Modely (*.stl;*.3mf;*.gcode;*.gco)|*.stl;*.3mf;*.gcode;*.gco|Všechny soubory (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        var path = dlg.FileName;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var type = ext.TrimStart('.').ToUpperInvariant();
        var bytes = await File.ReadAllBytesAsync(path);
        decimal? grams = null;
        decimal? hours = null;

        if (ext == ".3mf")
        {
            var meta = _threeMf.ReadMetadata(path);
            grams = meta.MaterialGrams;
            hours = meta.PrintHours;
        }
        else if (ext is ".gcode" or ".gco")
        {
            var meta = _gcode.ReadMetadata(path);
            grams = meta.MaterialGrams;
            hours = meta.PrintHours;
        }

        var item = new PrintModel
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(path),
            FileType = type,
            FilePath = path,
            OriginalFileName = System.IO.Path.GetFileName(path),
            FileContent = bytes,
            EstimatedMaterialGrams = grams,
            EstimatedPrintHours = hours
        };
        _db.PrintModels.Add(item);
        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedModel = Items.FirstOrDefault(x => x.Id == item.Id);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var m = SelectedModel;
        if (m is null) return;
        _db.PrintModels.Remove(m);
        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedModel = null;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    public async Task UpdateModelAsync(int modelId, string name, decimal? materialGrams, decimal? printHours, string? notes)
    {
        var tracked = await _db.PrintModels.FirstOrDefaultAsync(x => x.Id == modelId);
        if (tracked is null)
        {
            StatusMessage = "Vybraný model už neexistuje.";
            return;
        }

        tracked.Name = name.Trim();
        tracked.EstimatedMaterialGrams = materialGrams;
        tracked.EstimatedPrintHours = printHours;
        tracked.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        await _db.SaveChangesAsync();
        await LoadAsync();
        SelectedModel = Items.FirstOrDefault(x => x.Id == modelId);
        StatusMessage = "Model byl upraven.";
    }

    [RelayCommand]
    private async Task CreateCalculationFromModelAsync()
    {
        var m = SelectedModel;
        if (m is null)
        {
            StatusMessage = "Vyberte model v seznamu.";
            return;
        }

        var calc = new Calculation
        {
            CustomerId = SelectedCustomerId,
            PrintModelId = m.Id,
            SourceModelPath = string.IsNullOrWhiteSpace(m.FilePath)
                ? $"DB_MODEL:{m.Id}:{m.OriginalFileName}"
                : m.FilePath,
            MaterialGrams = m.EstimatedMaterialGrams ?? 0,
            PrintHours = m.EstimatedPrintHours ?? 0,
            MarginPercent = 15,
            ElectricityPricePerKwh = 7.5m,
            Title = m.Name,
            CreatedAt = DateTime.UtcNow
        };
        _db.Calculations.Add(calc);
        await _db.SaveChangesAsync();
        StatusMessage = $"Vytvořena kalkulace z modelu: {m.Name}.";
    }

    [RelayCommand]
    private async Task ExportSelectedModelAsync()
    {
        var m = SelectedModel;
        if (m is null)
        {
            StatusMessage = "Vyberte model v seznamu.";
            return;
        }

        var full = await _db.PrintModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.Id);
        if (full is null)
        {
            StatusMessage = "Vybraný model již v databázi neexistuje.";
            return;
        }

        var ext = string.IsNullOrWhiteSpace(full.FileType) ? "bin" : full.FileType.ToLowerInvariant();
        var suggestedName = string.IsNullOrWhiteSpace(full.OriginalFileName)
            ? $"{full.Name}.{ext}"
            : full.OriginalFileName;

        var dlg = new SaveFileDialog
        {
            Title = "Uložit model na disk",
            FileName = suggestedName,
            Filter = "Všechny soubory (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        if (full.FileContent is { Length: > 0 })
        {
            await File.WriteAllBytesAsync(dlg.FileName, full.FileContent);
            StatusMessage = $"Model byl exportován: {dlg.FileName}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(full.FilePath) && File.Exists(full.FilePath))
        {
            File.Copy(full.FilePath, dlg.FileName, overwrite: true);
            StatusMessage = $"Model byl exportován ze zdrojové cesty: {dlg.FileName}";
            return;
        }

        StatusMessage = "Model nemá dostupná data pro export.";
    }

    [RelayCommand]
    private async Task OpenSelectedModelAsync()
    {
        var m = SelectedModel;
        if (m is null)
        {
            StatusMessage = "Vyberte model v seznamu.";
            return;
        }

        var full = await _db.PrintModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.Id);
        if (full is null)
        {
            StatusMessage = "Vybraný model již v databázi neexistuje.";
            return;
        }

        string pathToOpen;
        if (full.FileContent is { Length: > 0 })
        {
            var ext = string.IsNullOrWhiteSpace(full.FileType) ? "bin" : full.FileType.ToLowerInvariant();
            var name = string.IsNullOrWhiteSpace(full.OriginalFileName)
                ? $"{full.Name}.{ext}"
                : full.OriginalFileName;
            var tempDir = Path.Combine(Path.GetTempPath(), "PrintCalc", "Models");
            Directory.CreateDirectory(tempDir);
            pathToOpen = Path.Combine(tempDir, $"{full.Id}_{name}");
            await File.WriteAllBytesAsync(pathToOpen, full.FileContent);
        }
        else if (!string.IsNullOrWhiteSpace(full.FilePath) && File.Exists(full.FilePath))
        {
            pathToOpen = full.FilePath;
        }
        else
        {
            StatusMessage = "Model nemá dostupná data pro otevření.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = pathToOpen,
            UseShellExecute = true
        });
        StatusMessage = $"Model otevřen: {pathToOpen}";
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
}
