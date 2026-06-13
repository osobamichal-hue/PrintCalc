using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.App.ViewModels;

public partial class PrintersViewModel : ObservableObject
{
    private readonly AppDbContext _db;

    public ObservableCollection<Printer> Items { get; } = new();

    [ObservableProperty] private Printer? selectedPrinter;
    [ObservableProperty] private string newName = "";
    [ObservableProperty] private PrinterKind newKind = PrinterKind.Fff;
    [ObservableProperty] private decimal newHourlyRate = 120m;
    [ObservableProperty] private decimal newKwhPerHour = 0.08m;
    [ObservableProperty] private decimal newStartFeePerPrint = 15m;
    [ObservableProperty] private string newMaxVolumeDescription = "";
    [ObservableProperty] private string newNotes = "";
    [ObservableProperty] private string statusMessage = "";

    public PrintersViewModel(AppDbContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db.ChangeTracker.Clear();
        Items.Clear();
        foreach (var p in await _db.Printers.OrderBy(x => x.Name).ToListAsync())
            Items.Add(p);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            StatusMessage = "Vyplňte název tiskárny.";
            return;
        }
        var p = new Printer
        {
            Name = NewName.Trim(),
            Kind = NewKind,
            HourlyRate = NewHourlyRate,
            KwhPerHour = NewKwhPerHour,
            StartFeePerPrint = NewStartFeePerPrint,
            MaxVolumeDescription = string.IsNullOrWhiteSpace(NewMaxVolumeDescription) ? null : NewMaxVolumeDescription.Trim(),
            Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim()
        };
        _db.Printers.Add(p);
        await _db.SaveChangesAsync();
        Items.Add(p);
        SelectedPrinter = p;
        ClearForm();
        StatusMessage = "Tiskárna byla přidána.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var p = SelectedPrinter;
        if (p is null) return;
        var id = p.Id;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.Calculations
                .Where(c => c.PrinterId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.PrinterId, (int?)null));

            await _db.PrinterFilamentTypes
                .Where(pf => pf.PrinterId == id)
                .ExecuteDeleteAsync();

            var tracked = await _db.Printers.FirstOrDefaultAsync(x => x.Id == id);
            if (tracked is not null)
                _db.Printers.Remove(tracked);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            StatusMessage = "Tiskárnu nelze smazat — je navázána na jiná data.";
            return;
        }

        Items.Remove(p);
        SelectedPrinter = null;
        StatusMessage = "Tiskárna byla smazána.";
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
        StatusMessage = "Seznam tiskáren byl obnoven.";
    }

    [RelayCommand]
    private void ClearForm()
    {
        NewName = "";
        NewKind = PrinterKind.Fff;
        NewHourlyRate = 120m;
        NewKwhPerHour = 0.08m;
        NewStartFeePerPrint = 15m;
        NewMaxVolumeDescription = "";
        NewNotes = "";
    }
}
