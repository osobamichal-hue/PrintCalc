using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using ModernWpf;
using PrintCalc.Core.Entities;
using PrintCalc.Infrastructure.Persistence;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;

namespace PrintCalc.App.ViewModels;

public partial class CompanySettingsViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private bool _isLoading;

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string ico = "";
    [ObservableProperty] private string dic = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string iban = "";
    [ObservableProperty] private string swift = "";
    [ObservableProperty] private string bankAccount = "";
    [ObservableProperty] private string paymentMethod = "Převodem";
    [ObservableProperty] private string logoPath = "";
    [ObservableProperty] private string documentVisualStyle = "Phoenix";
    [ObservableProperty] private string appTheme = "Dark";
    [ObservableProperty] private string colorPalette = "Warm Sunset";
    [ObservableProperty] private string dataRootPath = "";
    [ObservableProperty] private string exportCalculationsPath = "";
    [ObservableProperty] private string exportQuotesPath = "";
    [ObservableProperty] private string exportInvoicesPdfPath = "";
    [ObservableProperty] private string exportInvoicesCsvPath = "";
    [ObservableProperty] private string defaultCurrencyCode = "CZK";
    [ObservableProperty] private string defaultCurrencySymbol = "Kč";
    [ObservableProperty] private string invoiceNumberPrefix = "INV";
    [ObservableProperty] private string invoiceNumberSeriesList = "INV";
    [ObservableProperty] private bool invoiceNumberUseSeparator = true;
    [ObservableProperty] private int invoiceNumberCounterDigits = 3;
    [ObservableProperty] private decimal defaultVatRatePercent = 21m;
    [ObservableProperty] private bool defaultQuotesDetailedFromCalculation = true;
    [ObservableProperty] private bool defaultOrdersDetailedFromQuotes = true;
    [ObservableProperty] private bool defaultInvoicesDetailedFromOrders = true;
    [ObservableProperty] private string statusMessage = "";
    public ObservableCollection<string> DocumentVisualStyles { get; } = ["Phoenix", "Aurora", "Solaris"];
    public ObservableCollection<string> AppThemes { get; } = ["Light", "Dark"];
    public ObservableCollection<string> ColorPalettes { get; } = ["Warm Sunset", "Rose Latte", "Mint Breeze", "Lavender Mist", "Neon Pop"];
    public ObservableCollection<string> CurrencyCodes { get; } = ["CZK", "EUR", "USD"];

    public CompanySettingsViewModel(AppDbContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        Name = await GetAsync("Company.Name");
        Address = await GetAsync("Company.Address");
        Ico = await GetAsync("Company.Ico");
        Dic = await GetAsync("Company.Dic");
        Email = await GetAsync("Company.Email");
        Phone = await GetAsync("Company.Phone");
        Iban = await GetAsync("Company.Iban");
        Swift = await GetAsync("Company.Swift");
        BankAccount = await GetAsync("Company.BankAccount");
        PaymentMethod = await GetAsync("Company.PaymentMethod");
        LogoPath = await GetAsync("Company.LogoPath");
        DocumentVisualStyle = await GetAsync("Company.DocumentVisualStyle");
        AppTheme = await GetAsync("Company.AppTheme");
        ColorPalette = await GetAsync("Company.ColorPalette");
        DataRootPath = await GetAsync("App.DataRootPath");
        ExportCalculationsPath = await GetAsync("Export.CalculationsPdfPath");
        ExportQuotesPath = await GetAsync("Export.QuotesPdfPath");
        ExportInvoicesPdfPath = await GetAsync("Export.InvoicesPdfPath");
        ExportInvoicesCsvPath = await GetAsync("Export.InvoicesCsvPath");
        DefaultCurrencyCode = await GetAsync("Finance.CurrencyCode");
        DefaultCurrencySymbol = await GetAsync("Finance.CurrencySymbol");
        InvoiceNumberPrefix = await GetAsync("Finance.InvoiceNumberPrefix");
        InvoiceNumberSeriesList = await GetAsync("Finance.InvoiceNumberSeriesList");
        var invoiceUseSeparatorRaw = await GetAsync("Finance.InvoiceNumberUseSeparator");
        var invoiceCounterDigitsRaw = await GetAsync("Finance.InvoiceNumberCounterDigits");
        var vatText = await GetAsync("Finance.DefaultVatRatePercent");
        if (DocumentVisualStyle is "CorporateOrange")
            DocumentVisualStyle = "Phoenix";
        else if (DocumentVisualStyle is "MinimalBlue")
            DocumentVisualStyle = "Aurora";
        else if (string.IsNullOrWhiteSpace(DocumentVisualStyle))
            DocumentVisualStyle = "Phoenix";
        if (string.IsNullOrWhiteSpace(AppTheme))
            AppTheme = "Dark";
        if (string.IsNullOrWhiteSpace(ColorPalette))
            ColorPalette = "Warm Sunset";
        if (string.IsNullOrWhiteSpace(DataRootPath))
            DataRootPath = GetDefaultDataRootPath();
        if (string.IsNullOrWhiteSpace(ExportCalculationsPath))
            ExportCalculationsPath = Path.Combine(DataRootPath, "Kalkulace");
        if (string.IsNullOrWhiteSpace(ExportQuotesPath))
            ExportQuotesPath = Path.Combine(DataRootPath, "Nabidky");
        if (string.IsNullOrWhiteSpace(ExportInvoicesPdfPath))
            ExportInvoicesPdfPath = Path.Combine(DataRootPath, "Faktury");
        if (string.IsNullOrWhiteSpace(ExportInvoicesCsvPath))
            ExportInvoicesCsvPath = Path.Combine(DataRootPath, "Export");
        if (string.IsNullOrWhiteSpace(DefaultCurrencyCode))
            DefaultCurrencyCode = "CZK";
        if (string.IsNullOrWhiteSpace(DefaultCurrencySymbol))
            DefaultCurrencySymbol = "Kč";
        if (string.IsNullOrWhiteSpace(InvoiceNumberPrefix))
            InvoiceNumberPrefix = "INV";
        if (string.IsNullOrWhiteSpace(InvoiceNumberSeriesList))
            InvoiceNumberSeriesList = InvoiceNumberPrefix;
        InvoiceNumberUseSeparator = ParseBoolSetting(invoiceUseSeparatorRaw, defaultValue: true);
        if (!int.TryParse(invoiceCounterDigitsRaw, out var digits))
            digits = 3;
        InvoiceNumberCounterDigits = Math.Clamp(digits, 1, 3);
        if (!decimal.TryParse(vatText.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var vat))
            vat = 21m;
        DefaultVatRatePercent = vat;
        DefaultQuotesDetailedFromCalculation = ParseBoolSetting(await GetAsync("Quotes.CreateAsDetailedCalculation"), defaultValue: true);
        DefaultOrdersDetailedFromQuotes = ParseBoolSetting(await GetAsync("Orders.CreateAsDetailedFromQuotes"), defaultValue: true);
        DefaultInvoicesDetailedFromOrders = ParseBoolSetting(await GetAsync("Invoices.CreateAsDetailedFromOrders"), defaultValue: true);
        _isLoading = false;
        ApplyThemePreview();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SetAsync("Company.Name", Name);
        await SetAsync("Company.Address", Address);
        await SetAsync("Company.Ico", Ico);
        await SetAsync("Company.Dic", Dic);
        await SetAsync("Company.Email", Email);
        await SetAsync("Company.Phone", Phone);
        await SetAsync("Company.Iban", Iban);
        await SetAsync("Company.Swift", Swift);
        await SetAsync("Company.BankAccount", BankAccount);
        await SetAsync("Company.PaymentMethod", PaymentMethod);
        await SetAsync("Company.LogoPath", LogoPath);
        await SetAsync("Company.DocumentVisualStyle", DocumentVisualStyle);
        await SetAsync("Company.AppTheme", AppTheme);
        await SetAsync("Company.ColorPalette", ColorPalette);
        await SetAsync("App.DataRootPath", DataRootPath);
        await SetAsync("Export.CalculationsPdfPath", ExportCalculationsPath);
        await SetAsync("Export.QuotesPdfPath", ExportQuotesPath);
        await SetAsync("Export.InvoicesPdfPath", ExportInvoicesPdfPath);
        await SetAsync("Export.InvoicesCsvPath", ExportInvoicesCsvPath);
        await SetAsync("Finance.CurrencyCode", DefaultCurrencyCode);
        await SetAsync("Finance.CurrencySymbol", DefaultCurrencySymbol);
        await SetAsync("Finance.InvoiceNumberPrefix", InvoiceNumberPrefix);
        await SetAsync("Finance.InvoiceNumberSeriesList", InvoiceNumberSeriesList);
        await SetAsync("Finance.InvoiceNumberUseSeparator", InvoiceNumberUseSeparator ? "true" : "false");
        await SetAsync("Finance.InvoiceNumberCounterDigits", Math.Clamp(InvoiceNumberCounterDigits, 1, 3).ToString());
        await SetAsync("Finance.DefaultVatRatePercent", DefaultVatRatePercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await SetAsync("Quotes.CreateAsDetailedCalculation", DefaultQuotesDetailedFromCalculation ? "true" : "false");
        await SetAsync("Orders.CreateAsDetailedFromQuotes", DefaultOrdersDetailedFromQuotes ? "true" : "false");
        await SetAsync("Invoices.CreateAsDetailedFromOrders", DefaultInvoicesDetailedFromOrders ? "true" : "false");
        await _db.SaveChangesAsync();
        if (Application.Current is App app)
            app.ApplyVisualTheme(AppTheme, ColorPalette);
        else
            ThemeManager.Current.ApplicationTheme = AppTheme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        StatusMessage = "Nastavení bylo uloženo.";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        StatusMessage = "Údaje byly načteny z databáze.";
    }

    [RelayCommand]
    private void BrowseLogo()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vyberte logo firmy",
            Filter = "Obrázky (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Všechny soubory (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            LogoPath = dlg.FileName;
    }

    [RelayCommand]
    private void BrowseDataRoot()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vyberte datovou složku (otevřete libovolný soubor v cílové složce)",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "vyberte_tuto_slozku"
        };
        if (dlg.ShowDialog() == true)
        {
            var folder = Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrWhiteSpace(folder))
                DataRootPath = folder;
        }
    }

    [RelayCommand]
    private void BrowseExportCalculations() => BrowseFolderInto(value => ExportCalculationsPath = value);

    [RelayCommand]
    private void BrowseExportQuotes() => BrowseFolderInto(value => ExportQuotesPath = value);

    [RelayCommand]
    private void BrowseExportInvoicesPdf() => BrowseFolderInto(value => ExportInvoicesPdfPath = value);

    [RelayCommand]
    private void BrowseExportInvoicesCsv() => BrowseFolderInto(value => ExportInvoicesCsvPath = value);

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        var root = await GetDataRootPathAsync();
        Directory.CreateDirectory(root);
        var backupDir = Path.Combine(root, "Backups");
        Directory.CreateDirectory(backupDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var zipPath = Path.Combine(backupDir, $"PrintCalc_Backup_{stamp}.zip");
        var tempDir = Path.Combine(Path.GetTempPath(), "PrintCalcBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var dbPath = _db.Database.GetDbConnection().DataSource;
            var dbBackupPath = Path.Combine(tempDir, "printcalc.db");
            var dbExported = false;
            if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
            {
                _db.ChangeTracker.Clear();
                _db.Database.CloseConnection();
                try
                {
                    var escaped = dbBackupPath.Replace("'", "''");
                    await _db.Database.ExecuteSqlAsync($"VACUUM INTO '{escaped}'");
                    dbExported = File.Exists(dbBackupPath);
                }
                catch
                {
                    // Fallback: přímá kopie DB souboru, pokud VACUUM INTO selže.
                    File.Copy(dbPath, dbBackupPath, true);
                    dbExported = File.Exists(dbBackupPath);
                }
            }

            if (!dbExported)
            {
                throw new InvalidOperationException(
                    "Záloha databáze selhala. Nebyl nalezen zdrojový soubor printcalc.db.");
            }

            var settingsDump = await _db.AppSettings.AsNoTracking()
                .OrderBy(x => x.Key)
                .ToDictionaryAsync(x => x.Key, x => x.Value);
            var settingsJson = JsonSerializer.Serialize(settingsDump, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "appsettings-db.json"), settingsJson);

            var summary = await BuildBackupSummaryAsync(dbBackupPath);
            var manifestJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "backup-manifest.json"), manifestJson);

            var appConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appConfigPath))
                File.Copy(appConfigPath, Path.Combine(tempDir, "appsettings.json"), true);

            var dataExportDir = Path.Combine(tempDir, "data-root");
            CopyDirectory(root, dataExportDir, excludeTopLevelDirectoryNames: ["Backups"]);

            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
            StatusMessage = $"Záloha vytvořena: {zipPath} | DB: {summary.DatabaseSizeBytes / 1024:N0} KB | Zákazníci: {summary.Customers}, Filamenty: {summary.FilamentTypes}, Kalkulace: {summary.Calculations}, Nabídky: {summary.Quotes}, Faktury: {summary.Invoices}";
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    [RelayCommand]
    private async Task RestoreFromBackupAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Vyberte zálohu aplikace",
            Filter = "Záloha PrintCalc (*.zip)|*.zip"
        };
        if (ofd.ShowDialog() != true) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "PrintCalcRestore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            ZipFile.ExtractToDirectory(ofd.FileName, tempDir, true);

            var restoreDbPath = Path.Combine(tempDir, "printcalc.db");
            if (!File.Exists(restoreDbPath))
            {
                throw new InvalidOperationException("Záloha neobsahuje soubor printcalc.db.");
            }

            var currentDbPath = _db.Database.GetDbConnection().DataSource;
            if (!string.IsNullOrWhiteSpace(currentDbPath))
            {
                _db.ChangeTracker.Clear();
                _db.Database.CloseConnection();
                File.Copy(restoreDbPath, currentDbPath, true);
            }

            var restoreDataRoot = Path.Combine(tempDir, "data-root");
            var targetRoot = await GetDataRootPathAsync();
            if (Directory.Exists(restoreDataRoot))
            {
                Directory.CreateDirectory(targetRoot);
                CopyDirectory(restoreDataRoot, targetRoot);
            }

            StatusMessage = "Obnova dokončena. Doporučuji aplikaci restartovat.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Obnova selhala: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private static bool ParseBoolSetting(string? raw, bool defaultValue) =>
        string.IsNullOrWhiteSpace(raw)
            ? defaultValue
            : !raw.Equals("false", StringComparison.OrdinalIgnoreCase);

    private async Task<string> GetAsync(string key) =>
        await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync() ?? "";

    private async Task SetAsync(string key, string value)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (row is null)
        {
            row = new AppSettingsRow { Key = key };
            _db.AppSettings.Add(row);
        }
        row.Value = value?.Trim() ?? "";
    }

    private static string GetDefaultDataRootPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PrintCalc");

    private static void BrowseFolderInto(Action<string> applyAction)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vyberte cílovou složku (otevřete libovolný soubor v cílové složce)",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "vyberte_tuto_slozku"
        };
        if (dlg.ShowDialog() != true)
            return;
        var folder = Path.GetDirectoryName(dlg.FileName);
        if (!string.IsNullOrWhiteSpace(folder))
            applyAction(folder);
    }

    private async Task<string> GetDataRootPathAsync()
    {
        var value = await GetAsync("App.DataRootPath");
        return string.IsNullOrWhiteSpace(value) ? GetDefaultDataRootPath() : value;
    }

    private async Task<BackupSummary> BuildBackupSummaryAsync(string dbBackupPath)
    {
        var dbFile = new FileInfo(dbBackupPath);
        return new BackupSummary
        {
            CreatedAt = DateTime.Now,
            DatabaseFileName = dbFile.Name,
            DatabaseSizeBytes = dbFile.Exists ? dbFile.Length : 0,
            Customers = await _db.Customers.AsNoTracking().CountAsync(),
            FilamentTypes = await _db.FilamentTypes.AsNoTracking().CountAsync(),
            FilamentStockItems = await _db.FilamentStocks.AsNoTracking().CountAsync(),
            Printers = await _db.Printers.AsNoTracking().CountAsync(),
            Models = await _db.PrintModels.AsNoTracking().CountAsync(),
            Calculations = await _db.Calculations.AsNoTracking().CountAsync(),
            Quotes = await _db.Quotes.AsNoTracking().CountAsync(),
            QuoteLines = await _db.QuoteLines.AsNoTracking().CountAsync(),
            Orders = await _db.Orders.AsNoTracking().CountAsync(),
            OrderLines = await _db.OrderLines.AsNoTracking().CountAsync(),
            Invoices = await _db.Invoices.AsNoTracking().CountAsync(),
            InvoiceLines = await _db.InvoiceLines.AsNoTracking().CountAsync()
        };
    }

    private static void CopyDirectory(string sourceDir, string targetDir, IEnumerable<string>? excludeTopLevelDirectoryNames = null)
    {
        if (!Directory.Exists(sourceDir))
            return;
        Directory.CreateDirectory(targetDir);
        var excluded = new HashSet<string>(excludeTopLevelDirectoryNames ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var target = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, target, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (excluded.Contains(name))
                continue;
            var target = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, target);
        }
    }

    partial void OnDefaultQuotesDetailedFromCalculationChanged(bool value)
    {
        if (_isLoading) return;
        _ = PersistBoolSettingAsync("Quotes.CreateAsDetailedCalculation", value);
    }

    partial void OnDefaultOrdersDetailedFromQuotesChanged(bool value)
    {
        if (_isLoading) return;
        _ = PersistBoolSettingAsync("Orders.CreateAsDetailedFromQuotes", value);
    }

    partial void OnDefaultInvoicesDetailedFromOrdersChanged(bool value)
    {
        if (_isLoading) return;
        _ = PersistBoolSettingAsync("Invoices.CreateAsDetailedFromOrders", value);
    }

    private async Task PersistBoolSettingAsync(string key, bool value)
    {
        await SetAsync(key, value ? "true" : "false");
        await _db.SaveChangesAsync();
    }

    partial void OnAppThemeChanged(string value)
    {
        if (_isLoading) return;
        ApplyThemePreview();
    }

    partial void OnColorPaletteChanged(string value)
    {
        if (_isLoading) return;
        ApplyThemePreview();
    }

    private void ApplyThemePreview()
    {
        if (Application.Current is App app)
            app.ApplyVisualTheme(AppTheme, ColorPalette);
    }

    private sealed class BackupSummary
    {
        public DateTime CreatedAt { get; init; }
        public string DatabaseFileName { get; init; } = "";
        public long DatabaseSizeBytes { get; init; }
        public int Customers { get; init; }
        public int FilamentTypes { get; init; }
        public int FilamentStockItems { get; init; }
        public int Printers { get; init; }
        public int Models { get; init; }
        public int Calculations { get; init; }
        public int Quotes { get; init; }
        public int QuoteLines { get; init; }
        public int Orders { get; init; }
        public int OrderLines { get; init; }
        public int Invoices { get; init; }
        public int InvoiceLines { get; init; }
    }
}
