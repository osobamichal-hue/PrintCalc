using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Models;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class GeminiInvoiceClient
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public GeminiInvoiceClient(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ParsedPurchaseInvoice?> TryExtractAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        var apiKey = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "Ai.Gemini.ApiKey")
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var model = await GetSettingAsync("Ai.Gemini.Model", "gemini-2.0-flash", ct);
        var base64 = Convert.ToBase64String(pdfBytes);

        var prompt = """
            Extrahuj data z české faktury (přijaté FA) z PDF. Vrať POUZE validní JSON bez markdown:
            {
              "number": "string",
              "issueDate": "YYYY-MM-DD",
              "dueDate": "YYYY-MM-DD or null",
              "supplierName": "string",
              "supplierCompanyId": "string or null",
              "supplierVatId": "string or null",
              "totalAmount": number,
              "lines": [
                {
                  "description": "string",
                  "quantity": number,
                  "unit": "ks|kg|g",
                  "unitPrice": number,
                  "taxRatePercent": number,
                  "lineTotal": number,
                  "productCode": "string or null",
                  "ean": "string or null"
                }
              ]
            }
            """;

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "application/pdf",
                                data = base64
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1
            }
        };

        var client = _httpClientFactory.CreateClient("Gemini");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";
        using var resp = await client.PostAsJsonAsync(url, body, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var text = ExtractGeminiText(json);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<GeminiInvoiceDto>(text, JsonOptions);
            return dto?.ToParsed();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ParsedPurchaseInvoice?> TryFallbackExtractAsync(string pdfText, CancellationToken ct = default)
    {
        var endpoint = await GetSettingAsync("Ai.Fallback.Endpoint", "", ct);
        var model = await GetSettingAsync("Ai.Fallback.Model", "", ct);
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model)) return null;

        var prompt = $"""
            Extrahuj data z české faktury. Vrať POUZE JSON ve stejném formátu jako u Gemini.
            Text faktury:
            {pdfText[..Math.Min(pdfText.Length, 12000)]}
            """;

        var body = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.1
        };

        var client = _httpClientFactory.CreateClient("AiFallback");
        using var resp = await client.PostAsJsonAsync(endpoint.TrimEnd('/') + "/v1/chat/completions", body, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) return null;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start >= 0 && end > start)
                content = content[start..(end + 1)];
        }

        try
        {
            var dto = JsonSerializer.Deserialize<GeminiInvoiceDto>(content, JsonOptions);
            return dto?.ToParsed();
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetSettingAsync(string key, string fallback, CancellationToken ct)
    {
        var v = await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
    }

    private static string? ExtractGeminiText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var t))
                sb.Append(t.GetString());
        }
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private sealed class GeminiInvoiceDto
    {
        public string? Number { get; set; }
        public string? IssueDate { get; set; }
        public string? DueDate { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierCompanyId { get; set; }
        public string? SupplierVatId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<GeminiLineDto>? Lines { get; set; }

        public ParsedPurchaseInvoice ToParsed()
        {
            DateTime issue = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(IssueDate))
                DateTime.TryParse(IssueDate, out issue);

            DateTime? due = null;
            if (!string.IsNullOrWhiteSpace(DueDate) && DateTime.TryParse(DueDate, out var d))
                due = d;

            var result = new ParsedPurchaseInvoice
            {
                Number = Number ?? "",
                IssueDate = issue,
                DueDate = due,
                SupplierName = SupplierName ?? "",
                SupplierCompanyId = SupplierCompanyId,
                SupplierVatId = SupplierVatId,
                TotalAmount = TotalAmount
            };

            foreach (var ln in Lines ?? [])
            {
                result.Lines.Add(new ParsedPurchaseInvoiceLine
                {
                    Description = ln.Description ?? "",
                    Quantity = ln.Quantity <= 0 ? 1 : ln.Quantity,
                    Unit = ln.Unit ?? "ks",
                    UnitPrice = ln.UnitPrice,
                    TaxRatePercent = ln.TaxRatePercent <= 0 ? 21 : ln.TaxRatePercent,
                    LineTotal = ln.LineTotal > 0 ? ln.LineTotal : ln.UnitPrice * ln.Quantity,
                    ProductCode = ln.ProductCode,
                    Ean = ln.Ean
                });
            }

            if (result.TotalAmount <= 0)
                result.TotalAmount = result.Lines.Sum(l => l.LineTotal);

            return result;
        }
    }

    private sealed class GeminiLineDto
    {
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRatePercent { get; set; } = 21;
        public decimal LineTotal { get; set; }
        public string? ProductCode { get; set; }
        public string? Ean { get; set; }
    }
}
