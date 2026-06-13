using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;

namespace PrintCalc.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<FilamentType> FilamentTypes => Set<FilamentType>();
    public DbSet<FilamentStock> FilamentStocks => Set<FilamentStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrinterFilamentType> PrinterFilamentTypes => Set<PrinterFilamentType>();
    public DbSet<Calculation> Calculations => Set<Calculation>();
    public DbSet<PrintModel> PrintModels => Set<PrintModel>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<AppSettingsRow> AppSettings => Set<AppSettingsRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSettingsRow>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.Value).HasMaxLength(2048);
        });

        modelBuilder.Entity<PrinterFilamentType>(e =>
        {
            e.HasKey(x => new { x.PrinterId, x.FilamentTypeId });
            e.HasOne(x => x.Printer).WithMany(p => p.SupportedFilaments).HasForeignKey(x => x.PrinterId);
            e.HasOne(x => x.FilamentType).WithMany(f => f.PrinterLinks).HasForeignKey(x => x.FilamentTypeId);
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.HasOne(q => q.SourceCalculation).WithMany().HasForeignKey(q => q.SourceCalculationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasOne(o => o.Quote).WithMany().HasForeignKey(o => o.QuoteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasOne(i => i.Order).WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Calculation>(e =>
        {
            e.HasOne(c => c.PrintModel).WithMany().HasForeignKey(c => c.PrintModelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Printer).WithMany(p => p.Calculations).HasForeignKey(c => c.PrinterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuoteLine>(e =>
            e.HasOne(l => l.Quote).WithMany(q => q.Lines).HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<OrderLine>(e => e.HasOne(l => l.Order).WithMany(o => o.Lines).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<InvoiceLine>(e => e.HasOne(l => l.Invoice).WithMany(i => i.Lines).HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade));

        modelBuilder.Entity<PurchaseInvoice>(e =>
        {
            e.HasOne(p => p.Supplier).WithMany(s => s.PurchaseInvoices).HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.Property(p => p.Number).HasMaxLength(64);
            e.Property(p => p.SupplierName).HasMaxLength(256);
            e.Property(p => p.SupplierCompanyId).HasMaxLength(32);
            e.Property(p => p.SupplierVatId).HasMaxLength(32);
            e.Property(p => p.SourceFileName).HasMaxLength(512);
            e.Property(p => p.SourceFilePath).HasMaxLength(1024);
            e.Property(p => p.Notes).HasMaxLength(2048);
        });

        modelBuilder.Entity<PurchaseInvoiceLine>(e =>
        {
            e.HasOne(l => l.PurchaseInvoice).WithMany(p => p.Lines).HasForeignKey(l => l.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.FilamentType).WithMany().HasForeignKey(l => l.FilamentTypeId).OnDelete(DeleteBehavior.SetNull);
            e.Property(l => l.Description).HasMaxLength(512);
            e.Property(l => l.Unit).HasMaxLength(16);
            e.Property(l => l.ProductCode).HasMaxLength(64);
            e.Property(l => l.Ean).HasMaxLength(32);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(256);
            e.Property(s => s.CompanyId).HasMaxLength(32);
            e.Property(s => s.VatId).HasMaxLength(32);
            e.Property(s => s.Aliases).HasMaxLength(512);
            e.Property(s => s.Notes).HasMaxLength(1024);
        });

        modelBuilder.Entity<FilamentStock>()
            .HasOne(s => s.PurchaseInvoiceLine)
            .WithMany()
            .HasForeignKey(s => s.PurchaseInvoiceLineId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.PurchaseInvoiceLine)
            .WithMany()
            .HasForeignKey(m => m.PurchaseInvoiceLineId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.Calculation)
            .WithMany()
            .HasForeignKey(m => m.CalculationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>().Property(c => c.Name).HasMaxLength(256);
        modelBuilder.Entity<Customer>().Property(c => c.PreferredPaymentMethod).HasMaxLength(64);
        modelBuilder.Entity<FilamentType>().Property(f => f.Name).HasMaxLength(128);
        modelBuilder.Entity<FilamentStock>().Property(s => s.LotNumber).HasMaxLength(128);
        modelBuilder.Entity<FilamentStock>().Property(s => s.SupplierName).HasMaxLength(256);
        modelBuilder.Entity<FilamentStock>().Property(s => s.Notes).HasMaxLength(1024);
        modelBuilder.Entity<Printer>().Property(p => p.Name).HasMaxLength(128);
        modelBuilder.Entity<Calculation>().Property(c => c.Title).HasMaxLength(256);
        modelBuilder.Entity<Calculation>().Property(c => c.QuotePrintDescriptionOverride).HasMaxLength(512);
        modelBuilder.Entity<PrintModel>().Property(m => m.Name).HasMaxLength(256);
        modelBuilder.Entity<PrintModel>().Property(m => m.FileType).HasMaxLength(16);
        modelBuilder.Entity<PrintModel>().Property(m => m.FilePath).HasMaxLength(1024);
        modelBuilder.Entity<PrintModel>().Property(m => m.OriginalFileName).HasMaxLength(512);
        modelBuilder.Entity<Quote>().Property(q => q.Number).HasMaxLength(32);
        modelBuilder.Entity<Order>().Property(o => o.Number).HasMaxLength(32);
        modelBuilder.Entity<Invoice>().Property(i => i.Number).HasMaxLength(32);
        modelBuilder.Entity<Invoice>().Property(i => i.PaymentMethod).HasMaxLength(64);

        base.OnModelCreating(modelBuilder);
    }
}
