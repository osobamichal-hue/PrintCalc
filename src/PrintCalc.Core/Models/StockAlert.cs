using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Models;

public record StockAlert(
    StockAlertKind Kind,
    int FilamentTypeId,
    string FilamentTypeName,
    decimal CurrentKg,
    decimal? MinStockKg,
    int? FilamentStockId,
    string? LotNumber,
    DateTime? ExpirationDate,
    string Message);
