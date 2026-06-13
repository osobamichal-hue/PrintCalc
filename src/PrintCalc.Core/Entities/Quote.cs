using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class Quote
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public int? SourceCalculationId { get; set; }
    public Calculation? SourceCalculation { get; set; }

    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}
