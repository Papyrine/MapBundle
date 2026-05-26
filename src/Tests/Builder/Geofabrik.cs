/// <summary>
/// Reads Geofabrik's machine-readable download index (<c>index-v1-nogeom.json</c>), which lists every
/// downloadable region with its parent and ISO 3166-1 codes. This drives the region tree (continents and
/// countries); the per-region shapefile URL is retained on each entry but the bulk extracts are no longer
/// downloaded (cities/rivers/lakes now come from the global Natural Earth layers — see <see cref="NaturalEarth"/>).
/// </summary>
public static class Geofabrik
{
    const string indexUrl = "https://download.geofabrik.de/index-v1-nogeom.json";

    public static async Task<IReadOnlyList<GeofabrikEntry>> Index(HttpCache httpCache, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "geofabrik-index-v1-nogeom.json");
        await httpCache.ToFileAsync(indexUrl, path);

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);

        return
        [
            .. document.RootElement
                .GetProperty("features")
                .EnumerateArray()
                .Select(_ => _.GetProperty("properties"))
                .Select(_ => new GeofabrikEntry(
                    _.GetProperty("id").GetString()!,
                    _.TryGetProperty("parent", out var parent) ? parent.GetString() : null,
                    _.GetProperty("name").GetString()!,
                    Iso(_),
                    Shp(_)))
        ];
    }

    static string[] Iso(JsonElement properties) =>
        properties.TryGetProperty("iso3166-1:alpha2", out var iso) && iso.ValueKind == JsonValueKind.Array
            ? [.. iso.EnumerateArray().Select(_ => _.GetString()!)]
            : [];

    static string? Shp(JsonElement properties) =>
        properties.TryGetProperty("urls", out var urls) && urls.TryGetProperty("shp", out var shp)
            ? shp.GetString()
            : null;
}
