namespace PrintCalc.Core.Enums;

public enum PurchaseInvoiceStatus
{
    Draft = 0,
    ReadyToMatch = 1,
    Matched = 2,
    Posted = 3,
    Cancelled = 4
}

public enum PurchaseInvoiceImportSource
{
    Manual = 0,
    Isdoc = 1,
    Xml = 2,
    Csv = 3,
    Excel = 4,
    Pdf = 5
}

public enum PurchaseInvoiceLineMatchStatus
{
    Unmatched = 0,
    Suggested = 1,
    AutoMatched = 2,
    ManualMatched = 3
}
