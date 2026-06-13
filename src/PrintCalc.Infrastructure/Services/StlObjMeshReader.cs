using System.Globalization;
using System.IO.Compression;
using System.Text;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class StlObjMeshReader : IMeshReader
{
    public MeshGeometryResult ReadGeometry(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".stl" => ReadStl(filePath),
            ".obj" => ReadObj(filePath),
            ".3mf" => ReadThreeMfMesh(filePath),
            _ => new MeshGeometryResult { Warnings = { $"Nepodporovaný formát pro geometrii: {ext}" } }
        };
    }

    private static MeshGeometryResult ReadThreeMfMesh(string filePath)
    {
        var result = new MeshGeometryResult();
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var modelEntries = zip.Entries
                .Where(e => e.Length > 0 &&
                    (e.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.Equals("3dmodel.model", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (modelEntries.Count == 0)
            {
                result.Warnings.Add("3MF neobsahuje soubor 3dmodel.model.");
                return result;
            }

            double totalVolume = 0;
            double totalArea = 0;
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            var totalTriangles = 0;

            foreach (var entry in modelEntries)
            {
                using var stream = entry.Open();
                var doc = System.Xml.Linq.XDocument.Load(stream);
                foreach (var mesh in doc.Descendants().Where(e => e.Name.LocalName.Equals("mesh", StringComparison.OrdinalIgnoreCase)))
                {
                    var verts = mesh.Descendants()
                        .Where(e => e.Name.LocalName.Equals("vertex", StringComparison.OrdinalIgnoreCase))
                        .Select(v =>
                        {
                            var x = ParseDouble(v.Attribute("x")?.Value);
                            var y = ParseDouble(v.Attribute("y")?.Value);
                            var z = ParseDouble(v.Attribute("z")?.Value);
                            return (x, y, z);
                        })
                        .Where(v => v.Item1 is not null && v.Item2 is not null && v.Item3 is not null)
                        .Select(v => (X: v.Item1!.Value, Y: v.Item2!.Value, Z: v.Item3!.Value))
                        .ToList();

                    foreach (var tri in mesh.Descendants().Where(e => e.Name.LocalName.Equals("triangle", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!int.TryParse(tri.Attribute("v1")?.Value, out var i1)) continue;
                        if (!int.TryParse(tri.Attribute("v2")?.Value, out var i2)) continue;
                        if (!int.TryParse(tri.Attribute("v3")?.Value, out var i3)) continue;
                        if (i1 < 0 || i2 < 0 || i3 < 0 || i1 >= verts.Count || i2 >= verts.Count || i3 >= verts.Count) continue;

                        var v1 = verts[i1];
                        var v2 = verts[i2];
                        var v3 = verts[i3];
                        totalVolume += SignedTetraVolume(v1, v2, v3);
                        totalArea += TriangleArea(v1, v2, v3);
                        UpdateBounds(v1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                        UpdateBounds(v2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                        UpdateBounds(v3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                        totalTriangles++;
                    }
                }
            }

            result.TriangleCount = totalTriangles;
            if (totalTriangles == 0)
            {
                result.Warnings.Add("3MF mesh neobsahuje trojúhelníky.");
                return result;
            }

            return Finalize(result, totalVolume, totalArea, minX, minY, minZ, maxX, maxY, maxZ);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Chyba čtení 3MF geometrie: {ex.Message}");
            return result;
        }
    }

    private static double? ParseDouble(string? s) =>
        s is not null && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static MeshGeometryResult ReadStl(string filePath)
    {
        if (IsBinaryStl(filePath))
            return ParseBinaryStl(filePath);
        return ParseAsciiStl(filePath);
    }

    private static bool IsBinaryStl(string filePath)
    {
        var len = new FileInfo(filePath).Length;
        if (len < 84) return false;

        using var fs = File.OpenRead(filePath);
        var header = new byte[80];
        _ = fs.Read(header, 0, 80);
        if (len >= 84)
        {
            var triCount = new BinaryReader(fs).ReadUInt32();
            var expected = 84L + 50L * triCount;
            if (len == expected || (len > expected && len < expected + 512))
                return true;
        }

        var start = Encoding.ASCII.GetString(header).TrimStart();
        return !start.StartsWith("solid", StringComparison.OrdinalIgnoreCase);
    }

    private static MeshGeometryResult ParseBinaryStl(string filePath)
    {
        var result = new MeshGeometryResult();
        using var fs = File.OpenRead(filePath);
        using var br = new BinaryReader(fs);
        br.ReadBytes(80);
        var count = br.ReadUInt32();
        result.TriangleCount = (int)count;

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        double volume = 0;
        double area = 0;

        for (var i = 0; i < count; i++)
        {
            br.ReadSingle(); br.ReadSingle(); br.ReadSingle();
            var v1 = ReadVertex(br);
            var v2 = ReadVertex(br);
            var v3 = ReadVertex(br);
            br.ReadUInt16();

            volume += SignedTetraVolume(v1, v2, v3);
            area += TriangleArea(v1, v2, v3);
            UpdateBounds(v1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            UpdateBounds(v2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            UpdateBounds(v3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
        }

        return Finalize(result, volume, area, minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static (double X, double Y, double Z) ReadVertex(BinaryReader br) =>
        (br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

    private static MeshGeometryResult ParseAsciiStl(string filePath)
    {
        var result = new MeshGeometryResult();
        var vertices = new List<(double X, double Y, double Z)>();
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        double volume = 0;
        double area = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.GetCultureInfo("cs-CZ"), out x)) continue;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.GetCultureInfo("cs-CZ"), out y)) continue;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) &&
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.GetCultureInfo("cs-CZ"), out z)) continue;
            vertices.Add((x, y, z));
            if (vertices.Count == 3)
            {
                var v1 = vertices[0];
                var v2 = vertices[1];
                var v3 = vertices[2];
                volume += SignedTetraVolume(v1, v2, v3);
                area += TriangleArea(v1, v2, v3);
                UpdateBounds(v1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(v2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(v3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                result.TriangleCount++;
                vertices.Clear();
            }
        }

        if (result.TriangleCount == 0)
            result.Warnings.Add("STL neobsahuje žádné trojúhelníky.");

        return Finalize(result, volume, area, minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static MeshGeometryResult ReadObj(string filePath)
    {
        var result = new MeshGeometryResult();
        var verts = new List<(double X, double Y, double Z)>();
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        double volume = 0;
        double area = 0;

        foreach (var raw in File.ReadLines(filePath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    verts.Add((x, y, z));
                }
                continue;
            }

            if (!line.StartsWith("f ", StringComparison.Ordinal)) continue;
            var faceParts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            if (faceParts.Length < 3) continue;

            var indices = faceParts.Select(ParseFaceIndex).Where(i => i > 0).ToArray();
            if (indices.Length < 3) continue;

            for (var t = 1; t < indices.Length - 1; t++)
            {
                var v1 = verts[indices[0] - 1];
                var v2 = verts[indices[t] - 1];
                var v3 = verts[indices[t + 1] - 1];
                volume += SignedTetraVolume(v1, v2, v3);
                area += TriangleArea(v1, v2, v3);
                UpdateBounds(v1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(v2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(v3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                result.TriangleCount++;
            }
        }

        if (result.TriangleCount == 0)
            result.Warnings.Add("OBJ neobsahuje žádné plochy (f).");

        return Finalize(result, volume, area, minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static int ParseFaceIndex(string token)
    {
        var idxPart = token.Split('/')[0];
        return int.TryParse(idxPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) ? idx : 0;
    }

    private static double SignedTetraVolume((double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c) =>
        (a.X * (b.Y * c.Z - c.Y * b.Z) - a.Y * (b.X * c.Z - c.X * b.Z) + a.Z * (b.X * c.Y - c.X * b.Y)) / 6.0;

    private static double TriangleArea((double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c)
    {
        var abX = b.X - a.X; var abY = b.Y - a.Y; var abZ = b.Z - a.Z;
        var acX = c.X - a.X; var acY = c.Y - a.Y; var acZ = c.Z - a.Z;
        var cx = abY * acZ - abZ * acY;
        var cy = abZ * acX - abX * acZ;
        var cz = abX * acY - abY * acX;
        return 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
    }

    private static void UpdateBounds(
        (double X, double Y, double Z) v,
        ref double minX, ref double minY, ref double minZ,
        ref double maxX, ref double maxY, ref double maxZ)
    {
        minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
        maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
    }

    private static MeshGeometryResult Finalize(
        MeshGeometryResult result,
        double volume, double area,
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ)
    {
        if (result.TriangleCount == 0) return result;

        var volMm3 = Math.Abs(volume);
        result.VolumeCm3 = Round3(volMm3 / 1000.0);
        result.SurfaceCm2 = Round3(area / 100.0);
        result.BboxXmm = Round3(maxX - minX);
        result.BboxYmm = Round3(maxY - minY);
        result.BboxZmm = Round3(maxZ - minZ);
        return result;
    }

    private static decimal Round3(double v) => Math.Round((decimal)v, 3, MidpointRounding.AwayFromZero);
}
