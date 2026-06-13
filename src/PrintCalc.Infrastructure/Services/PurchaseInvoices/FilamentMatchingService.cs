using FuzzySharp;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class FilamentMatchingService : IFilamentMatchingService
{
    private readonly AppDbContext _db;

    public FilamentMatchingService(AppDbContext db) => _db = db;

    public async Task MatchLinesAsync(int purchaseInvoiceId, CancellationToken ct = default)
    {
        var inv = await _db.PurchaseInvoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == purchaseInvoiceId, ct)
            ?? throw new InvalidOperationException("Faktura nenalezena.");

        if (inv.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Zaúčtovanou fakturu nelze párovat.");

        var types = await _db.FilamentTypes.AsNoTracking().ToListAsync(ct);
        var autoThreshold = await GetIntSettingAsync("PurchaseInvoice.MatchAutoThreshold", 85, ct);
        var suggestThreshold = await GetIntSettingAsync("PurchaseInvoice.MatchSuggestThreshold", 50, ct);

        foreach (var line in inv.Lines)
        {
            if (line.MatchStatus == PurchaseInvoiceLineMatchStatus.ManualMatched && line.FilamentTypeId is not null)
                continue;

            PurchaseInvoiceLineCalculator.ComputeStockFields(line);
            var (score, best) = ScoreBestMatch(line, types);

            line.MatchConfidence = score;
            if (score >= autoThreshold && best is not null)
            {
                line.FilamentTypeId = best.Id;
                line.MatchStatus = PurchaseInvoiceLineMatchStatus.AutoMatched;
            }
            else if (score >= suggestThreshold && best is not null)
            {
                line.FilamentTypeId = best.Id;
                line.MatchStatus = PurchaseInvoiceLineMatchStatus.Suggested;
            }
            else
            {
                line.FilamentTypeId = null;
                line.MatchStatus = PurchaseInvoiceLineMatchStatus.Unmatched;
            }
        }

        inv.Status = inv.Lines.All(l => l.FilamentTypeId is not null)
            ? PurchaseInvoiceStatus.Matched
            : PurchaseInvoiceStatus.ReadyToMatch;

        await _db.SaveChangesAsync(ct);
    }

    public async Task SetManualMatchAsync(int lineId, int filamentTypeId, CancellationToken ct = default)
    {
        var line = await _db.PurchaseInvoiceLines
            .Include(l => l.PurchaseInvoice)
            .FirstOrDefaultAsync(l => l.Id == lineId, ct)
            ?? throw new InvalidOperationException("Řádek faktury nenalezen.");

        if (line.PurchaseInvoice.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Zaúčtovanou fakturu nelze upravovat.");

        _ = await _db.FilamentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == filamentTypeId, ct)
            ?? throw new InvalidOperationException("Typ filamentu nenalezen.");

        line.FilamentTypeId = filamentTypeId;
        line.MatchStatus = PurchaseInvoiceLineMatchStatus.ManualMatched;
        line.MatchConfidence = 100;

        var inv = line.PurchaseInvoice;
        inv.Status = await AllLinesMatchedAsync(inv.Id, ct)
            ? PurchaseInvoiceStatus.Matched
            : PurchaseInvoiceStatus.ReadyToMatch;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CreateFilamentTypeFromLineAsync(int lineId, CancellationToken ct = default)
    {
        var line = await _db.PurchaseInvoiceLines
            .Include(l => l.PurchaseInvoice)
            .FirstOrDefaultAsync(l => l.Id == lineId, ct)
            ?? throw new InvalidOperationException("Řádek faktury nenalezen.");

        if (line.PurchaseInvoice.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Zaúčtovanou fakturu nelze upravovat.");

        var hints = PurchaseInvoiceLineCalculator.ParseDescriptionHints(line.Description);
        var name = BuildFilamentName(line.Description, hints.material, hints.color);

        var type = new FilamentType
        {
            Name = name,
            Manufacturer = hints.manufacturer,
            Color = hints.color,
            DiameterMm = hints.diameterMm,
            Notes = $"Vytvořeno z FA {line.PurchaseInvoice.Number}, řádek {line.Id}"
        };
        _db.FilamentTypes.Add(type);
        await _db.SaveChangesAsync(ct);

        line.FilamentTypeId = type.Id;
        line.MatchStatus = PurchaseInvoiceLineMatchStatus.ManualMatched;
        line.MatchConfidence = 100;

        var inv = line.PurchaseInvoice;
        inv.Status = await AllLinesMatchedAsync(inv.Id, ct)
            ? PurchaseInvoiceStatus.Matched
            : PurchaseInvoiceStatus.ReadyToMatch;

        await _db.SaveChangesAsync(ct);
        return type.Id;
    }

    private static (int score, FilamentType? best) ScoreBestMatch(PurchaseInvoiceLine line, List<FilamentType> types)
    {
        if (types.Count == 0) return (0, null);

        var desc = line.Description.ToUpperInvariant();
        FilamentType? best = null;
        var bestScore = 0;

        foreach (var t in types)
        {
            var score = 0;
            var nameScore = Fuzz.TokenSetRatio(desc, (t.Name + " " + (t.Manufacturer ?? "") + " " + (t.Color ?? "")).ToUpperInvariant());
            score = Math.Max(score, nameScore);

            if (!string.IsNullOrWhiteSpace(t.Manufacturer) && desc.Contains(t.Manufacturer.ToUpperInvariant(), StringComparison.Ordinal))
                score += 15;
            if (!string.IsNullOrWhiteSpace(t.Color) && desc.Contains(t.Color.ToUpperInvariant(), StringComparison.Ordinal))
                score += 10;
            if (desc.Contains(t.DiameterMm.ToString("0.##"), StringComparison.Ordinal))
                score += 5;

            score = Math.Min(100, score);

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return (bestScore, best);
    }

    private static string BuildFilamentName(string description, string? material, string? color)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(material)) parts.Add(material);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color);
        if (parts.Count == 0)
            return description.Length > 80 ? description[..80] : description;
        return string.Join(" ", parts);
    }

    private async Task<bool> AllLinesMatchedAsync(int invoiceId, CancellationToken ct)
    {
        return !await _db.PurchaseInvoiceLines
            .AnyAsync(l => l.PurchaseInvoiceId == invoiceId && l.FilamentTypeId == null, ct);
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var v = await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
        return int.TryParse(v, out var n) ? n : fallback;
    }
}
