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
}
