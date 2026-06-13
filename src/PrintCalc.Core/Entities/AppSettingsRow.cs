namespace PrintCalc.Core.Entities;

/// <summary>Jednoduché klíč-hodnota nastavení (např. cena kWh).</summary>
public class AppSettingsRow
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
