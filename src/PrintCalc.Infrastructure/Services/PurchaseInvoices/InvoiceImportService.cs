using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class InvoiceImportService : IInvoiceImportService
{
    private readonly IPdfInvoiceExtractor _pdfExtractor;

    public InvoiceImportService(IPdfInvoiceExtractor pdfExtractor) => _pdfExtractor = pdfExtractor;

    public PurchaseInvoiceImportSource DetectFormat(string fileName, Stream? peekStream = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".pdf") return PurchaseInvoiceImportSource.Pdf;
        if (ext is ".csv" or ".txt") return PurchaseInvoiceImportSource.Csv;
        if (ext is ".xlsx" or ".xls") return PurchaseInvoiceImportSource.Excel;
        if (ext is ".xml" or ".isdoc") return DetectXmlFormat(peekStream);
        if (peekStream is not null && peekStream.CanSeek)
        {
            peekStream.Position = 0;
            var buf = new byte[Math.Min(512, (int)Math.Max(0, peekStream.Length - peekStream.Position))];
            _ = peekStream.Read(buf, 0, buf.Length);
            peekStream.Position = 0;
            var head = System.Text.Encoding.UTF8.GetString(buf);
            if (head.Contains("isdoc", StringComparison.OrdinalIgnoreCase))
                return PurchaseInvoiceImportSource.Isdoc;
            if (head.TrimStart().StartsWith("<"))
                return PurchaseInvoiceImportSource.Xml;
        }
        return PurchaseInvoiceImportSource.Manual;
    }

    public async Task<ParsedPurchaseInvoice> ParseAsync(Stream stream, string fileName, PurchaseInvoiceImportSource? formatHint = null, CancellationToken ct = default)
    {
        var format = formatHint ?? DetectFormat(fileName, stream.CanSeek ? stream : null);

        return format switch
        {
            PurchaseInvoiceImportSource.Pdf => await _pdfExtractor.ExtractAsync(stream, ct),
            PurchaseInvoiceImportSource.Csv => CsvInvoiceParser.Parse(stream),
            PurchaseInvoiceImportSource.Excel => ExcelInvoiceParser.Parse(stream),
            PurchaseInvoiceImportSource.Isdoc => ParseXml(stream),
            PurchaseInvoiceImportSource.Xml => ParseXml(stream),
            _ => throw new InvalidOperationException($"Nepodporovaný formát souboru: {fileName}")
        };
    }

    private static ParsedPurchaseInvoice ParseXml(Stream stream)
    {
        stream.Position = 0;
        if (IsdocInvoiceParser.TryParse(stream, out var isdocResult))
            return isdocResult;

        stream.Position = 0;
        if (GenericXmlInvoiceParser.TryParse(stream, out var generic))
            return generic;

        throw new InvalidOperationException("XML soubor se nepodařilo parsovat jako fakturu.");
    }

    private static PurchaseInvoiceImportSource DetectXmlFormat(Stream? stream)
    {
        if (stream is null || !stream.CanSeek) return PurchaseInvoiceImportSource.Xml;
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var head = reader.ReadToEnd();
        stream.Position = 0;
        return head.Contains("isdoc.cz/namespace", StringComparison.OrdinalIgnoreCase)
            ? PurchaseInvoiceImportSource.Isdoc
            : PurchaseInvoiceImportSource.Xml;
    }
}
