using System.Globalization;
using System.Text.RegularExpressions;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class GcodeReader : IGcodeReader
{
    public ThreeMfMetadata ReadMetadata(string filePath)
    {
        decimal? grams = null;
        decimal? hours = null;
        var warnings = new List<string>();
        decimal maxElapsedSec = 0m;
        decimal? lastRemainingMin = null;
        decimal? maxRemainingMin = null;

        try
        {
            foreach (var raw in File.ReadLines(filePath))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (!line.StartsWith(";")) continue;

                // Common slicer hints: ;TIME:9180
                var timeMatch = Regex.Match(line, @"^;TIME:(\d+)$", RegexOptions.IgnoreCase);
                if (timeMatch.Success && decimal.TryParse(timeMatch.Groups[1].Value, out var sec))
                    hours ??= sec / 3600m;

                var elapsedMatch = Regex.Match(line, @"^;TIME_ELAPSED:(\d+[.,]?\d*)$", RegexOptions.IgnoreCase);
                if (elapsedMatch.Success &&
                    decimal.TryParse(elapsedMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var elapsed))
                {
                    if (elapsed > maxElapsedSec) maxElapsedSec = elapsed;
                }

                // Prusa/Orca/Bambu style progress line: M73 Pxx R152
                var m73Match = Regex.Match(line, @"^M73\s+P\d+\s+R(\d+)", RegexOptions.IgnoreCase);
                if (m73Match.Success && decimal.TryParse(m73Match.Groups[1].Value, out var rem))
                {
                    lastRemainingMin = rem;
                    if (maxRemainingMin is null || rem > maxRemainingMin) maxRemainingMin = rem;
                }

                // e.g. "; estimated printing time (normal mode) = 2h33m"
                if (line.Contains("estimated printing time", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf('=');
                    var candidate = idx >= 0 ? line[(idx + 1)..].Trim() : line;
                    if (TryParseDuration(candidate, out var h))
                        hours ??= h;
                }

                // e.g. "; Filament used: 32.71m, 98.34g"
                if (line.Contains("filament", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("material", StringComparison.OrdinalIgnoreCase))
                {
                    var gMatch = Regex.Match(line, @"(\d+[.,]?\d*)\s*g\b", RegexOptions.IgnoreCase);
                    if (gMatch.Success &&
                        decimal.TryParse(gMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var g))
                    {
                        grams ??= g;
                    }

                    var kgMatch = Regex.Match(line, @"(\d+[.,]?\d*)\s*kg\b", RegexOptions.IgnoreCase);
                    if (kgMatch.Success &&
                        decimal.TryParse(kgMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var kg))
                    {
                        grams ??= kg * 1000m;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Chyba čtení GCode: {ex.Message}");
        }

        if (hours is null)
        {
            if (maxElapsedSec > 0 && lastRemainingMin is { } remNow)
                hours = (maxElapsedSec / 3600m) + (remNow / 60m);
            else if (maxRemainingMin is { } remStart)
                hours = remStart / 60m;
        }

        if (grams is null && hours is null)
            warnings.Add("V GCode nebyly nalezeny metadata času/hmotnosti. Zadejte ručně.");
        else if (grams is null)
            warnings.Add("V GCode nebyla nalezena hmotnost filamentu (g). Doplňte ručně.");

        return new ThreeMfMetadata
        {
            MaterialGrams = grams,
            PrintHours = hours,
            Warnings = warnings
        };
    }

    private static bool TryParseDuration(string text, out decimal hours)
    {
        hours = 0;
        var v = text.Trim().ToLowerInvariant();
        if (v.Length == 0) return false;

        if (v.Contains(':'))
        {
            var p = v.Split(':');
            if (p.Length >= 2 &&
                int.TryParse(p[0], out var h) &&
                int.TryParse(p[1], out var m))
            {
                var s = p.Length > 2 && int.TryParse(p[2], out var sec) ? sec : 0;
                hours = h + m / 60m + s / 3600m;
                return true;
            }
        }

        var hm = Regex.Match(v, @"(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?\s*(?:(\d+)\s*s)?");
        if (hm.Success)
        {
            var h = hm.Groups[1].Success ? int.Parse(hm.Groups[1].Value) : 0;
            var m = hm.Groups[2].Success ? int.Parse(hm.Groups[2].Value) : 0;
            var s = hm.Groups[3].Success ? int.Parse(hm.Groups[3].Value) : 0;
            if (h > 0 || m > 0 || s > 0)
            {
                hours = h + m / 60m + s / 3600m;
                return true;
            }
        }

        return false;
    }
}
