using System.Text.Json;

namespace MapBundle.Builder;

/// <summary>One entry from Geofabrik's download index: a region, its parent, ISO codes and shapefile URL.</summary>
public sealed record GeofabrikEntry(string Id, string? Parent, string Name, string[] Iso2, string? ShpUrl);

/// <summary>
/// Reads Geofabrik's machine-readable download index (<c>index-v1-nogeom.json</c>), which lists every
/// downloadable region with its parent, ISO 3166-1 codes and per-region shapefile URL. The region tree
/// (continents and countries) and the per-region OSM extracts are both driven from it.
/// </summary>
public static class Geofabrik
{
    const string IndexUrl = "https://download.geofabrik.de/index-v1-nogeom.json";

    // Layer shapefile base names inside a "<region>-latest-free.shp.zip".
    public const string PlacesLayer = "gis_osm_places_free_1";
    public const string WaterwaysLayer = "gis_osm_waterways_free_1";
    public const string WaterLayer = "gis_osm_water_a_free_1";

    public static async Task<IReadOnlyList<GeofabrikEntry>> Index(HttpCache httpCache, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "geofabrik-index-v1-nogeom.json");
        await httpCache.ToFileAsync(IndexUrl, path);

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
