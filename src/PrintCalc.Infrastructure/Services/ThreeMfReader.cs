using System.IO.Compression;
using System.Xml.Linq;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class ThreeMfReader : IThreeMfReader
{
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

    public ThreeMfMetadata ReadMetadata(string filePath)
    {
        var warnings = new List<string>();
        decimal? grams = null;
        decimal? hours = null;
        string? layerNote = null;
        string? supportNote = null;

        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            foreach (var entry in zip.Entries)
            {
                if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                    !entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                var root = doc.Root;
                if (root is null) continue;

                TryParseProductionStack(doc, ref grams, ref hours, warnings);
                ScanForNumericHints(root, ref grams, ref hours, ref layerNote, ref supportNote, warnings);

                if (entry.FullName.Contains("Metadata", StringComparison.OrdinalIgnoreCase) &&
                    root.Name.LocalName.Equals("coreProperties", StringComparison.OrdinalIgnoreCase))
                {
                    var desc = root.Element(DcNs + "description")?.Value;
                    if (!string.IsNullOrWhiteSpace(desc))
                        TryParseDescription(desc!, ref grams, ref hours);
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Chyba čtení 3MF: {ex.Message}");
        }

        if (grams is null && hours is null)
            warnings.Add("V souboru nebyly spolehlivě nalezeny hmotnost ani čas – doplňte ručně.");

        return new ThreeMfMetadata
        {
            MaterialGrams = grams,
            PrintHours = hours,
            LayerHeightNote = layerNote,
            SupportNote = supportNote,
            Warnings = warnings
        };
    }

    private static void TryParseProductionStack(XDocument doc, ref decimal? grams, ref decimal? hours, List<string> warnings)
    {
        foreach (var elem in doc.Descendants().Where(e => e.Name.LocalName.Equals("metadatagroup", StringComparison.OrdinalIgnoreCase)))
        {
            var nameAttr = elem.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(nameAttr)) continue;
            if (!nameAttr.Contains("slic", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("cura", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("prus", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var prop in elem.Descendants().Where(e => e.Name.LocalName.Equals("metadataproperty", StringComparison.OrdinalIgnoreCase)))
            {
                var n = prop.Attribute("name")?.Value;
                var v = prop.Attribute("value")?.Value;
                if (n is null || v is null) continue;
                TryAssign(n, v, ref grams, ref hours);
            }
        }
    }

    private static void ScanForNumericHints(XElement root, ref decimal? grams, ref decimal? hours, ref string? layerNote, ref string? supportNote, List<string> warnings)
    {
        foreach (var e in root.Descendants())
        {
            var name = e.Name.LocalName;
            var text = (e.Value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) continue;

            if (name.Contains("layer", StringComparison.OrdinalIgnoreCase) && text.Length < 64)
                layerNote ??= text;
            if (name.Contains("support", StringComparison.OrdinalIgnoreCase) && text.Length < 64)
                supportNote ??= text;

            TryAssign(name, text, ref grams, ref hours);

            foreach (var a in e.Attributes())
                TryAssign(a.Name.LocalName, a.Value, ref grams, ref hours);
        }
    }

    private static void TryAssign(string name, string value, ref decimal? grams, ref decimal? hours)
    {
        var key = name.ToLowerInvariant();
        var val = value.Trim();

        if (TryParseWeightToGrams(val, out var gramsParsed) &&
            (key.Contains("weight") || key.Contains("filament") || key.Contains("material") || key.Contains("wt")))
        {
            grams ??= gramsParsed;
        }

        if (TryParseHoursFlexible(val, out var hoursParsed) &&
            (key.Contains("duration") || key.Contains("time") || key.Contains("print") || key.Contains("estimated")))
        {
            hours ??= hoursParsed;
        }

        if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            if (key.Contains("materialweight") || key.Contains("filament weight") || (key.Contains("weight") && (key.Contains("gram") || key.EndsWith(" g"))))
                grams ??= num;
            if (key.Contains("weight") && key.Contains("kg"))
                grams ??= num * 1000m;
            if (key.Contains("duration") || (key.Contains("time") && key.Contains("print")) || key == "estimated_time" || key.Contains("printing_time"))
                hours ??= num > 48 ? num / 3600m : num;
        }

        if ((key.Contains("duration") || key.Contains("time")) && val.Contains(':'))
        {
            if (TryParseHms(val, out var h))
                hours ??= h;
        }
    }

    private static bool TryParseHms(string value, out decimal hours)
    {
        hours = 0;
        var parts = value.Split(':');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var h)) return false;
        if (!int.TryParse(parts[1], out var m)) return false;
        var s = parts.Length > 2 && int.TryParse(parts[2], out var sec) ? sec : 0;
        hours = h + m / 60m + s / 3600m;
        return true;
    }

    private static void TryParseDescription(string desc, ref decimal? grams, ref decimal? hours)
    {
        foreach (var line in desc.Split(';', '\n'))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            TryAssign(key, val, ref grams, ref hours);
        }
    }

    private static bool TryParseWeightToGrams(string value, out decimal grams)
    {
        grams = 0;
        var lower = value.ToLowerInvariant().Replace(" ", "");
        if (lower.Length == 0) return false;

        if (lower.EndsWith("kg"))
        {
            var n = lower[..^2];
            if (decimal.TryParse(n.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var kg))
            {
                grams = kg * 1000m;
                return true;
            }
        }
        else if (lower.EndsWith("g"))
        {
            var n = lower[..^1];
            if (decimal.TryParse(n.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var g))
            {
                grams = g;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseHoursFlexible(string value, out decimal hours)
    {
        hours = 0;
        var v = value.ToLowerInvariant().Trim();
        if (v.Length == 0) return false;

        if (v.Contains(':') && TryParseHms(v, out var hms))
        {
            hours = hms;
            return true;
        }

        // format like 2h33m, 14m43s, 1h
        var h = 0m;
        var m = 0m;
        var s = 0m;
        var ok = false;

        var hMatch = System.Text.RegularExpressions.Regex.Match(v, @"(\d+[.,]?\d*)\s*h");
        if (hMatch.Success && decimal.TryParse(hMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hv))
        {
            h = hv;
            ok = true;
        }
        var mMatch = System.Text.RegularExpressions.Regex.Match(v, @"(\d+[.,]?\d*)\s*m");
        if (mMatch.Success && decimal.TryParse(mMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mv))
        {
            m = mv;
            ok = true;
        }
        var sMatch = System.Text.RegularExpressions.Regex.Match(v, @"(\d+[.,]?\d*)\s*s");
        if (sMatch.Success && decimal.TryParse(sMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sv))
        {
            s = sv;
            ok = true;
        }

        if (ok)
        {
            hours = h + m / 60m + s / 3600m;
            return true;
        }

        return false;
    }
}
