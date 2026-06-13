namespace PrintCalc.Api.Util;

public static class ApiStringUtil
{
    public static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
