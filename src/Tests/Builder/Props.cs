using System.Globalization;

/// <summary>Case-insensitive access to feature attributes (shapefile DBF column names vary in case).</summary>
static class Props
{
    public static string Text(Feature feature, string key)
    {
        foreach (var pair in feature.Properties)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value?.ToString() ?? "";
            }
        }

        return "";
    }

    // Numeric attribute access for ordering (e.g. label priority by population/rank). Parses the
    // text form invariantly so a missing/blank/non-numeric value falls back to 0 rather than throwing.
    public static double Number(Feature feature, string key) =>
        double.TryParse(Text(feature, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
}
