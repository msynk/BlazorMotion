namespace BlazorMotion.Engine;

/// <summary>
/// Pure-C# RGBA color parsing and linear interpolation.
/// Handles #hex, rgb(), and rgba() formats.
/// </summary>
internal static class ColorInterpolator
{
    /// <summary>Linearly interpolates between two CSS color strings at progress <paramref name="t"/> (0–1).</summary>
    public static string Lerp(string from, string to, double t)
    {
        var f = Parse(from);
        var tt = Parse(to);
        if (f == null || tt == null) return to;

        int r = (int)Math.Round(f[0] + (tt[0] - f[0]) * t);
        int g = (int)Math.Round(f[1] + (tt[1] - f[1]) * t);
        int b = (int)Math.Round(f[2] + (tt[2] - f[2]) * t);
        double a = f[3] + (tt[3] - f[3]) * t;
        return $"rgba({r},{g},{b},{a:G4})";
    }

    /// <summary>Returns true if the CSS string looks like a color value.</summary>
    public static bool LooksLikeColor(string? value)
        => value != null &&
           (value.StartsWith('#') ||
            value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase));

    // ── Internal ──────────────────────────────────────────────────────────────

    private static double[]? Parse(string c)
    {
        if (string.IsNullOrEmpty(c)) return null;

        if (c.StartsWith('#'))
        {
            var h = c[1..];
            // Expand shorthand #rgb → #rrggbb, #rgba → #rrggbbaa
            if (h.Length == 3 || h.Length == 4)
                h = string.Concat(h.Select(ch => $"{ch}{ch}"));
            if (h.Length < 6) return null;
            return
            [
                Convert.ToInt32(h[..2], 16),
                Convert.ToInt32(h[2..4], 16),
                Convert.ToInt32(h[4..6], 16),
                h.Length >= 8 ? Convert.ToInt32(h[6..8], 16) / 255.0 : 1.0,
            ];
        }

        // rgb() / rgba()
        var m = System.Text.RegularExpressions.Regex.Match(
            c, @"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)");
        if (m.Success)
        {
            return
            [
                double.Parse(m.Groups[1].Value),
                double.Parse(m.Groups[2].Value),
                double.Parse(m.Groups[3].Value),
                m.Groups[4].Success ? double.Parse(m.Groups[4].Value) : 1.0,
            ];
        }
        return null;
    }
}
