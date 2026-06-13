using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class Printer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PrinterKind Kind { get; set; } = PrinterKind.Fff;
    /// <summary>
    /// Hodinová sazba stroje (Kč/h). Do jedné částky obvykle patří provoz, odpis/amortizace stroje a související režie;
    /// spotřeba elektřiny se v kalkulaci počítá zvlášť z polí kWh/h a globální ceny kWh.
    /// </summary>
    public decimal HourlyRate { get; set; }
    public decimal KwhPerHour { get; set; }
    /// <summary>Pevný poplatek za každý zahájený tisk (příprava, nahřátí, podložka…), Kč. Doporučeno 10–20.</summary>
    public decimal StartFeePerPrint { get; set; }
    public string? MaxVolumeDescription { get; set; }
    public string? Notes { get; set; }

    public ICollection<PrinterFilamentType> SupportedFilaments { get; set; } = new List<PrinterFilamentType>();
    public ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
}
