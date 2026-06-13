using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class PurchaseInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCompanyId { get; set; }
    public string? SupplierVatId { get; set; }

    public decimal TotalAmount { get; set; }
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;
    public PurchaseInvoiceImportSource ImportSource { get; set; } = PurchaseInvoiceImportSource.Manual;
    public string? SourceFileName { get; set; }
    public string? SourceFilePath { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}
