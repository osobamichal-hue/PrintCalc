using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public decimal TotalAmount { get; set; }
    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
