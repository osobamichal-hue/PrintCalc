namespace PrintCalc.Core.Enums;

public enum QuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3
}

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    InProduction = 2,
    Completed = 3,
    Cancelled = 4
}

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}
