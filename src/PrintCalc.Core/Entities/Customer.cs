namespace PrintCalc.Core.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public string? VatId { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Výchozí splatnost faktur ve dnech (např. 14).</summary>
    public int? InvoiceDueDays { get; set; }
    /// <summary>Výchozí platební metoda na dokladech (např. Převodem / Hotově).</summary>
    public string? PreferredPaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
