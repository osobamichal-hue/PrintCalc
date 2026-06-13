using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class Invoice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Number { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string? PaymentMethod { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string? Notes { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
