namespace PrintCalc.Core.Entities;

public class PrinterFilamentType
{
    public int PrinterId { get; set; }
    public Printer Printer { get; set; } = null!;
    public int FilamentTypeId { get; set; }
    public FilamentType FilamentType { get; set; } = null!;
}
