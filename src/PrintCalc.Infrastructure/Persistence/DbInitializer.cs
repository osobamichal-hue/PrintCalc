using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using System.Collections.Generic;

namespace PrintCalc.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.AppSettings.AnyAsync(ct))
        {
            db.AppSettings.Add(new AppSettingsRow { Key = "ElectricityPricePerKwh", Value = "7.50" });
            db.AppSettings.Add(new AppSettingsRow { Key = "ModelingHourlyRate", Value = "450" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Name", Value = "Moje 3D firma" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Address", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Ico", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Dic", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Email", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Phone", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Iban", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.Swift", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.BankAccount", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.PaymentMethod", Value = "Převodem" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.LogoPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.DocumentVisualStyle", Value = "Phoenix" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.AppTheme", Value = "Dark" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Company.ColorPalette", Value = "Warm Sunset" });
            db.AppSettings.Add(new AppSettingsRow { Key = "App.DataRootPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Export.CalculationsPdfPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Export.QuotesPdfPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Export.InvoicesPdfPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Export.InvoicesCsvPath", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Finance.CurrencyCode", Value = "CZK" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Finance.CurrencySymbol", Value = "Kč" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Finance.DefaultVatRatePercent", Value = "21" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Ai.Gemini.ApiKey", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Ai.Gemini.Model", Value = "gemini-2.0-flash" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Ai.Fallback.Endpoint", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "Ai.Fallback.Model", Value = "" });
            db.AppSettings.Add(new AppSettingsRow { Key = "PurchaseInvoice.MatchAutoThreshold", Value = "85" });
            db.AppSettings.Add(new AppSettingsRow { Key = "PurchaseInvoice.MatchSuggestThreshold", Value = "50" });
            await db.SaveChangesAsync(ct);
        }
        else if (!await db.AppSettings.AnyAsync(x => x.Key == "ModelingHourlyRate", ct))
        {
            db.AppSettings.Add(new AppSettingsRow { Key = "ModelingHourlyRate", Value = "450" });
            await db.SaveChangesAsync(ct);
        }

        var companyDefaults = new Dictionary<string, string>
        {
            ["Company.Name"] = "Moje 3D firma",
            ["Company.Address"] = "",
            ["Company.Ico"] = "",
            ["Company.Dic"] = "",
            ["Company.Email"] = "",
            ["Company.Phone"] = "",
            ["Company.Iban"] = "",
            ["Company.Swift"] = "",
            ["Company.BankAccount"] = "",
            ["Company.PaymentMethod"] = "Převodem",
            ["Company.LogoPath"] = "",
            ["Company.DocumentVisualStyle"] = "Phoenix",
            ["Company.AppTheme"] = "Dark",
            ["Company.ColorPalette"] = "Warm Sunset",
            ["App.DataRootPath"] = "",
            ["Export.CalculationsPdfPath"] = "",
            ["Export.QuotesPdfPath"] = "",
            ["Export.InvoicesPdfPath"] = "",
            ["Export.InvoicesCsvPath"] = "",
            ["Finance.CurrencyCode"] = "CZK",
            ["Finance.CurrencySymbol"] = "Kč",
            ["Finance.DefaultVatRatePercent"] = "21",
            ["Ai.Gemini.ApiKey"] = "",
            ["Ai.Gemini.Model"] = "gemini-2.0-flash",
            ["Ai.Fallback.Endpoint"] = "",
            ["Ai.Fallback.Model"] = "",
            ["PurchaseInvoice.MatchAutoThreshold"] = "85",
            ["PurchaseInvoice.MatchSuggestThreshold"] = "50"
        };
        var changed = false;
        foreach (var (k, v) in companyDefaults)
        {
            if (!await db.AppSettings.AnyAsync(x => x.Key == k, ct))
            {
                db.AppSettings.Add(new AppSettingsRow { Key = k, Value = v });
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }
}
