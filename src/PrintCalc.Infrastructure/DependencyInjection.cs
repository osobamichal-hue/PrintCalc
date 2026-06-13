using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using PrintCalc.Infrastructure.Services;
using PrintCalc.Infrastructure.Services.Backup;
using PrintCalc.Infrastructure.Services.PurchaseInvoices;

namespace PrintCalc.Infrastructure;

public static class DependencyInjection
{
    /// <param name="configuration">Konfigurace (ConnectionStrings, Database:Provider).</param>
    /// <param name="forWebHost">
    /// Při <c>true</c> (ASP.NET Core) je <see cref="AppDbContext"/> a služby závislé na DB registrovány jako scoped.
    /// Při <c>false</c> (WPF) zůstává chování jako dříve: singleton kontext odpovídající jednovláknové aplikaci.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool forWebHost = false)
    {
        string conn;
        var configured = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(configured))
        {
            var dbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrintCalc");
            Directory.CreateDirectory(dbDir);
            conn = "Data Source=" + Path.Combine(dbDir, "printcalc.db");
        }
        else
        {
            conn = configured;
            if (conn.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var path = conn["Data Source=".Length..].Trim();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }
        }

        void ConfigureDb(DbContextOptionsBuilder options)
        {
            var provider = configuration["Database:Provider"] ?? "Sqlite";
            if (string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                var pg = configuration.GetConnectionString("PostgreSQL");
                if (!string.IsNullOrWhiteSpace(pg))
                    options.UseNpgsql(pg);
                else
                    options.UseSqlite(conn);
            }
            else
            {
                options.UseSqlite(conn);
            }
        }

        if (forWebHost)
            services.AddDbContext<AppDbContext>(ConfigureDb);
        else
            services.AddDbContext<AppDbContext>(ConfigureDb, ServiceLifetime.Singleton, ServiceLifetime.Singleton);

        services.AddSingleton<ICalculationEngine, CalculationEngine>();
        services.AddSingleton<IThreeMfReader, ThreeMfReader>();
        services.AddSingleton<IGcodeReader, GcodeReader>();

        if (forWebHost)
        {
            services.AddHttpClient("Gemini", c => c.Timeout = TimeSpan.FromMinutes(2));
            services.AddHttpClient("AiFallback", c => c.Timeout = TimeSpan.FromMinutes(2));
            services.AddScoped<PurchaseInvoiceFileStorage>();
            services.AddScoped<GeminiInvoiceClient>();
            services.AddScoped<IPdfInvoiceExtractor, PdfInvoiceExtractor>();
            services.AddScoped<IInvoiceImportService, InvoiceImportService>();
            services.AddScoped<IPurchaseInvoiceService, PurchaseInvoiceService>();
            services.AddScoped<IFilamentMatchingService, FilamentMatchingService>();
            services.AddScoped<IStockService, StockService>();
            services.AddScoped<IInvoicePdfService, InvoicePdfService>();
            services.AddScoped<ICalculationPdfService, CalculationPdfService>();
            services.AddScoped<IQuotePdfService, QuotePdfService>();
            services.AddScoped<IAccountingExportService, AccountingExportService>();
            services.AddScoped<IDocumentNumberService, DocumentNumberService>();
            services.AddScoped<IBackupService, BackupService>();
        }
        else
        {
            services.AddSingleton<IStockService, StockService>();
            services.AddSingleton<IInvoicePdfService, InvoicePdfService>();
            services.AddSingleton<ICalculationPdfService, CalculationPdfService>();
            services.AddSingleton<IQuotePdfService, QuotePdfService>();
            services.AddSingleton<IAccountingExportService, AccountingExportService>();
            services.AddSingleton<IDocumentNumberService, DocumentNumberService>();
            services.AddSingleton<IBackupService, BackupService>();
        }

        return services;
    }
}
