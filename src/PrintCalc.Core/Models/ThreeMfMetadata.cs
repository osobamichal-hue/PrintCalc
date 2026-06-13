namespace PrintCalc.Core.Models;

public class ThreeMfMetadata
{
    public decimal? MaterialGrams { get; set; }
    public decimal? PrintHours { get; set; }
    public string? LayerHeightNote { get; set; }
    public string? SupportNote { get; set; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
