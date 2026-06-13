using PrintCalc.Core.Models;
using PrintCalc.Core.Services;
using UglyToad.PdfPig;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class PdfInvoiceExtractor : IPdfInvoiceExtractor
{
    private readonly GeminiInvoiceClient _gemini;

    public PdfInvoiceExtractor(GeminiInvoiceClient gemini) => _gemini = gemini;

    public async Task<ParsedPurchaseInvoice> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var fromGemini = await _gemini.TryExtractAsync(bytes, ct);
        if (fromGemini is not null && fromGemini.Lines.Count > 0)
            return fromGemini;

        var pdfText = ExtractText(bytes);
        var fromFallback = await _gemini.TryFallbackExtractAsync(pdfText, ct);
        if (fromFallback is not null && fromFallback.Lines.Count > 0)
            return fromFallback;

        using var heuristicStream = new MemoryStream(bytes);
        var heuristic = HeuristicPdfInvoiceParser.Parse(heuristicStream);
        if (heuristic.Lines.Count > 0 || !string.IsNullOrWhiteSpace(heuristic.Number))
            return heuristic;

        throw new InvalidOperationException("Nepodařilo se vytěžit data z PDF. Zkuste ISDOC/XML nebo doplňte Gemini API klíč v nastavení.");
    }

    private static string ExtractText(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }
}
