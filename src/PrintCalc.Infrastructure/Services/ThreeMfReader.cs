using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class ThreeMfReader : IThreeMfReader
{
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private readonly IGcodeReader _gcode;

    public ThreeMfReader(IGcodeReader gcode) => _gcode = gcode;

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
                if (entry.Length == 0) continue;
                var name = entry.FullName.Replace('\\', '/');

                if (IsEmbeddedGcode(name))
                {
                    MergeFromGcode(entry, ref grams, ref hours, warnings);
                    continue;
                }

                if (!IsMetadataEntry(name)) continue;

                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    if (name.Contains("slice_info", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("sliceinfo", StringComparison.OrdinalIgnoreCase))
                    {
                        TryParseSliceInfo(text, ref grams, ref hours, warnings);
                        continue;
                    }

                    if (text.TrimStart().StartsWith('{'))
                    {
                        TryParseJsonMetadata(text, ref grams, ref hours);
                        continue;
                    }

                    if (text.TrimStart().StartsWith('<'))
                    {
                        var doc = XDocument.Parse(text);
                        var root = doc.Root;
                        if (root is null) continue;

                        TryParseProductionStack(doc, ref grams, ref hours, warnings);
                        TryParseSliceInfoXml(doc, ref grams, ref hours);
                        ScanForNumericHints(root, ref grams, ref hours, ref layerNote, ref supportNote, warnings);

                        if (name.Contains("Metadata", StringComparison.OrdinalIgnoreCase) &&
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
                    warnings.Add($"3MF {entry.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Chyba čtení 3MF: {ex.Message}");
        }

        return new ThreeMfMetadata
        {
            MaterialGrams = grams,
            PrintHours = hours,
            LayerHeightNote = layerNote,
            SupportNote = supportNote,
            Warnings = warnings
        };
    }

    private static bool IsEmbeddedGcode(string path) =>
        path.Contains("Metadata/", StringComparison.OrdinalIgnoreCase) &&
        (path.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".gco", StringComparison.OrdinalIgnoreCase));

    private static bool IsMetadataEntry(string path) =>
        path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".config", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".model", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/Metadata/", StringComparison.OrdinalIgnoreCase);

    private void MergeFromGcode(ZipArchiveEntry entry, ref decimal? grams, ref decimal? hours, List<string> warnings)
    {
        var temp = Path.Combine(Path.GetTempPath(), "pc-3mf-gcode-" + Guid.NewGuid().ToString("N") + ".gcode");
        try
        {
            entry.ExtractToFile(temp, overwrite: true);
            var meta = _gcode.ReadMetadata(temp);
            grams ??= meta.MaterialGrams;
            hours ??= meta.PrintHours;
            warnings.AddRange(meta.Warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"GCode v 3MF ({entry.Name}): {ex.Message}");
        }
        finally
        {
            try { File.Delete(temp); } catch { /* ignore */ }
        }
    }

    private static void TryParseSliceInfo(string text, ref decimal? grams, ref decimal? hours, List<string> warnings)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            TryParseJsonMetadata(text, ref grams, ref hours);
            return;
        }

        if (!trimmed.StartsWith('<')) return;

        try
        {
            var doc = XDocument.Parse(text);
            TryParseSliceInfoXml(doc, ref grams, ref hours);
            string? unusedLayer = null;
            string? unusedSupport = null;
            ScanForNumericHints(doc.Root!, ref grams, ref hours, ref unusedLayer, ref unusedSupport, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"slice_info: {ex.Message}");
        }
    }

    private static void TryParseSliceInfoXml(XDocument doc, ref decimal? grams, ref decimal? hours)
    {
        foreach (var meta in doc.Descendants().Where(e => e.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase)))
        {
            var key = meta.Attribute("key")?.Value ?? meta.Attribute("name")?.Value;
            var val = meta.Attribute("value")?.Value ?? meta.Value;
            if (key is null || val is null) continue;
            TryAssignSliceKey(key, val, ref grams, ref hours);
        }

        foreach (var header in doc.Descendants().Where(e => e.Name.LocalName.Equals("header_item", StringComparison.OrdinalIgnoreCase)))
        {
            var key = header.Attribute("key")?.Value;
            var val = header.Attribute("value")?.Value;
            if (key is null || val is null) continue;
            TryAssignSliceKey(key, val, ref grams, ref hours);
        }

        decimal? sumG = null;
        foreach (var filament in doc.Descendants().Where(e => e.Name.LocalName.Equals("filament", StringComparison.OrdinalIgnoreCase)))
        {
            var usedG = filament.Attribute("used_g")?.Value;
            if (usedG is not null &&
                decimal.TryParse(usedG.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var g))
            {
                sumG = (sumG ?? 0) + g;
            }
        }

        if (sumG is > 0) grams ??= sumG;
    }

    private static void TryParseJsonMetadata(string json, ref decimal? grams, ref decimal? hours)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            WalkJson(doc.RootElement, ref grams, ref hours);
        }
        catch
        {
            /* not JSON */
        }
    }

    private static void WalkJson(JsonElement el, ref decimal? grams, ref decimal? hours, string? keyHint = null)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    WalkJson(prop.Value, ref grams, ref hours, prop.Name);
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    WalkJson(item, ref grams, ref hours, keyHint);
                break;
            case JsonValueKind.Number:
                if (keyHint is not null && decimal.TryParse(el.GetRawText(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    TryAssignSliceKey(keyHint, n.ToString(System.Globalization.CultureInfo.InvariantCulture), ref grams, ref hours);
                break;
            case JsonValueKind.String:
                if (keyHint is not null)
                    TryAssignSliceKey(keyHint, el.GetString() ?? "", ref grams, ref hours);
                break;
        }
    }

    private static void TryAssignSliceKey(string name, string value, ref decimal? grams, ref decimal? hours)
    {
        var key = name.ToLowerInvariant().Trim();
        var val = value.Trim();
        if (val.Length == 0) return;

        if (key is "weight" or "material_weight" or "filament_weight" or "total_weight" or "used_g")
        {
            if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var w))
                grams ??= w;
            return;
        }

        if (key is "prediction" or "print_time" or "estimated_time" or "time" or "duration")
        {
            if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sec))
                hours ??= sec > 48 ? sec / 3600m : sec;
            return;
        }

        TryAssign(name, value, ref grams, ref hours);
    }

    private static void TryParseProductionStack(XDocument doc, ref decimal? grams, ref decimal? hours, List<string> warnings)
    {
        foreach (var elem in doc.Descendants().Where(e => e.Name.LocalName.Equals("metadatagroup", StringComparison.OrdinalIgnoreCase)))
        {
            var nameAttr = elem.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(nameAttr)) continue;
            if (!nameAttr.Contains("slic", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("cura", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("prus", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("creality", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("cxengine", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("crslice", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("bambu", StringComparison.OrdinalIgnoreCase) &&
                !nameAttr.Contains("orca", StringComparison.OrdinalIgnoreCase))
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

        if (key is "weight" or "material_weight" or "filament_weight" or "total_weight")
        {
            if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fw))
                grams ??= fw;
        }

        if (TryParseHoursFlexible(val, out var hoursParsed) &&
            (key.Contains("duration") || key.Contains("time") || key.Contains("print") || key.Contains("estimated") || key is "prediction"))
        {
            hours ??= hoursParsed;
        }

        if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            if (key.Contains("materialweight") || key.Contains("filament weight") || (key.Contains("weight") && (key.Contains("gram") || key.EndsWith(" g"))))
                grams ??= num;
            if (key.Contains("weight") && key.Contains("kg"))
                grams ??= num * 1000m;
            if (key is "prediction" or "print_time" or "estimated_time")
                hours ??= num > 48 ? num / 3600m : num;
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
        else if (lower.EndsWith('g') && !lower.EndsWith("kg"))
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
