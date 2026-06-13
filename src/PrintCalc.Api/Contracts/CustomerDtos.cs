namespace PrintCalc.Api.Contracts;

public record CustomerDto(
    int Id,
    string Name,
    string? CompanyId,
    string? VatId,
    string? Street,
    string? City,
    string? Zip,
    string? Email,
    string? Phone,
    int? InvoiceDueDays,
    string? PreferredPaymentMethod,
    DateTime CreatedAt);

public record CustomerWriteDto(
    string Name,
    string? CompanyId,
    string? VatId,
    string? Street,
    string? City,
    string? Zip,
    string? Email,
    string? Phone,
    int? InvoiceDueDays,
    string? PreferredPaymentMethod);
