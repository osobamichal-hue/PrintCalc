namespace PrintCalc.Core.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public string? VatId { get; set; }
    public string? Aliases { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
}
