using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api.Services;

public static class AppSettingsQueries
{
    public static async Task<decimal> GetDecimalAsync(
        AppDbContext db,
        string key,
        decimal defaultValue,
        CancellationToken ct = default)
    {
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return defaultValue;
        return decimal.TryParse(
                   row.Value.Replace(',', '.'),
                   NumberStyles.Any,
                   CultureInfo.InvariantCulture,
                   out var d)
            ? d
            : defaultValue;
    }

    public static async Task<string> GetStringAsync(
        AppDbContext db,
        string key,
        string defaultValue,
        CancellationToken ct = default)
    {
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        return string.IsNullOrWhiteSpace(row?.Value) ? defaultValue : row!.Value;
    }

    public static async Task UpsertAsync(AppDbContext db, string key, string value, CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null)
        {
            db.AppSettings.Add(new AppSettingsRow { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }
}
