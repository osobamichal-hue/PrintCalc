using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernWpf;
using PrintCalc.App.Services;
using PrintCalc.App.ViewModels;
using PrintCalc.App.Views;
using PrintCalc.Infrastructure;
using PrintCalc.Infrastructure.Persistence;
using QuestPDF.Infrastructure;
using System.Windows.Media;

namespace PrintCalc.App;

public partial class App : Application
{
    public void ApplyVisualTheme(string? appTheme, string? colorPalette = null)
    {
        var isLight = appTheme == "Light";
        ThemeManager.Current.ApplicationTheme = isLight ? ApplicationTheme.Light : ApplicationTheme.Dark;

        var palette = string.IsNullOrWhiteSpace(colorPalette) ? "Warm Sunset" : colorPalette;
        var (primary, primaryHover, primaryPressed) = GetPaletteColors(palette, isLight);

        // Pozadi nastavujeme jako gradient, aby byl moderni vzhled viditelny v cele aplikaci.
        Resources["AppBgBrush"] = isLight
            ? new LinearGradientBrush(
                (System.Windows.Media.Color)ColorConverter.ConvertFromString("#EEF1FF")!,
                (System.Windows.Media.Color)ColorConverter.ConvertFromString("#DDF8FF")!,
                35)
            : new LinearGradientBrush(
                (System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E1B2D")!,
                (System.Windows.Media.Color)ColorConverter.ConvertFromString("#182635")!,
                35);

        SetBrushColor("SurfaceBrush", isLight ? "#CCFFFFFF" : "#BF2A2E3E");
        SetBrushColor("SurfaceStrongBrush", isLight ? "#FFFFFFFF" : "#FF32384C");
        SetBrushColor("PrimaryBrush", primary);
        SetBrushColor("PrimaryHoverBrush", primaryHover);
        SetBrushColor("PrimaryPressedBrush", primaryPressed);
        SetBrushColor("BorderBrushSoft", isLight ? "#CAD8F2" : "#4C5272");
        SetBrushColor("TextBrush", isLight ? "#1A1A2B" : "#F2F1FF");
        SetBrushColor("MenuSurfaceBrush", isLight ? "#FFFFFFFF" : "#FF2A2E42");
        SetBrushColor("MenuHoverBrush", isLight ? "#E8E9FF" : "#FF454A66");
    }

    private void SetBrushColor(string key, string colorHex)
    {
        var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(colorHex)!;
        if (Resources[key] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
                Resources[key] = new SolidColorBrush(color);
            else
                brush.Color = color;
        }
        else
        {
            Resources[key] = new SolidColorBrush(color);
        }
    }

    private static (string Primary, string Hover, string Pressed) GetPaletteColors(string palette, bool isLight)
    {
        return palette switch
        {
            "Rose Latte" => isLight ? ("#D14A77", "#DD5A87", "#B83C68") : ("#E56A95", "#F07EAA", "#CD5682"),
            "Mint Breeze" => isLight ? ("#00897B", "#0F9F90", "#007569") : ("#1BAA9A", "#2BC0AF", "#119183"),
            "Lavender Mist" => isLight ? ("#5E35B1", "#6F45C3", "#4C2E96") : ("#7A57CB", "#8C69DB", "#6545B0"),
            "Neon Pop" => isLight ? ("#2962FF", "#3B74FF", "#1F4ED8") : ("#5A84FF", "#7194FF", "#436CE4"),
            // Warm Sunset mapujeme na moderni violet/cyan, aby default uz nevypadal zastarale.
            _ => isLight ? ("#5E35B1", "#6F45C3", "#4C2E96") : ("#7A57CB", "#8C69DB", "#6545B0")
        };
    }

    private IHost? _host;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Dispatcher.Invoke(() =>
            {
                AppDialog.ShowError(
                    args.Exception?.ToString() ?? "Neznámá chyba úlohy na pozadí.",
                    "PrintCalc – chyba");
            });
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            _host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.SetBasePath(AppContext.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddInfrastructure(ctx.Configuration);
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<MainWindow>();

                    services.AddTransient<CustomersViewModel>();
                    services.AddTransient<CustomersView>();
                    services.AddTransient<FilamentsViewModel>();
                    services.AddTransient<FilamentsView>();
                    services.AddTransient<PrintersViewModel>();
                    services.AddTransient<PrintersView>();
                    services.AddTransient<ModelsViewModel>();
                    services.AddTransient<ModelsView>();
                    services.AddTransient<CalculationViewModel>();
                    services.AddTransient<CalculationView>();
                    services.AddTransient<QuotesViewModel>();
                    services.AddTransient<QuotesView>();
                    services.AddTransient<OrdersViewModel>();
                    services.AddTransient<OrdersView>();
                    services.AddTransient<InvoicesViewModel>();
                    services.AddTransient<InvoicesView>();
                    services.AddTransient<CompanySettingsViewModel>();
                    services.AddTransient<CompanySettingsView>();
                    services.AddTransient<HelpAboutViewModel>();
                    services.AddTransient<HelpAboutView>();
                })
                .Build();

            await _host.StartAsync();

            var db = _host.Services.GetRequiredService<AppDbContext>();
            await DbInitializer.InitializeAsync(db);
            var appTheme = await db.AppSettings
                .Where(x => x.Key == "Company.AppTheme")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            var colorPalette = await db.AppSettings
                .Where(x => x.Key == "Company.ColorPalette")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            ApplyVisualTheme(appTheme, colorPalette);

            var main = _host.Services.GetRequiredService<MainWindow>();
            main.Show();
        }
        catch (Exception ex)
        {
            AppDialog.ShowError(
                "Aplikaci se nepodařilo spustit. Zkontrolujte, že spouštíte z výstupní složky buildu (nebo použijte „dotnet run“ z kořene řešení) a že existuje soubor appsettings.json vedle .exe.\n\n"
                + ex.Message
                + "\n\n"
                + ex,
                "PrintCalc – chyba při startu");
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        AppDialog.ShowError(
            args.Exception.ToString(),
            "PrintCalc – neošetřená chyba");
        args.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                AppDialog.ShowError(
                    ex.ToString(),
                    "PrintCalc – kritická chyba");
            });
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                await _host.StopAsync();
            }
            catch
            {
                /* ignore */
            }
        }

        base.OnExit(e);
    }
}
