using System.Text.Json.Nodes;

namespace MapBundle.Builder;

/// <summary>
/// The OSM-derived administrative boundaries from the "country-levels" project (pinned release, simplified
/// WGS84 GeoJSON). <c>iso1</c> files are country borders (admin level 2); <c>iso2</c> files are the
/// state/province subdivisions (ISO 3166-2). Each file is a single GeoJSON Feature keyed by ISO code.
/// </summary>
public sealed class CountryLevels
{
    // Pinned to v2.2.0. "high" is the most detailed tier; "low"/"medium" over-simplify small countries
    // (e.g. Monaco collapses to a triangle). It's a one-time ~49 MB download (cached), and only a single
    // country polygon ends up in each package, so the per-package size cost is negligible.
    const string Url = "https://github.com/hyperknot/country-levels/releases/download/v2.2.0/export_high.tgz";

    readonly Dictionary<string, Feature> borders;
    readonly Dictionary<string, List<Feature>> subdivisions;

    CountryLevels(Dictionary<string, Feature> borders, Dictionary<string, List<Feature>> subdivisions)
    {
        this.borders = borders;
        this.subdivisions = subdivisions;
    }

    /// <summary>The country border feature for an ISO 3166-1 alpha-2 code, if present.</summary>
    public Feature? Border(string iso2) =>
        borders.GetValueOrDefault(iso2.ToUpperInvariant());

    /// <summary>The state/province features whose ISO 3166-2 code belongs to the given country code.</summary>
    public IReadOnlyList<Feature> Subdivisions(string iso2) =>
        subdivisions.GetValueOrDefault(iso2.ToUpperInvariant()) ?? [];

    public static async Task<CountryLevels> Download(HttpCache httpCache, string directory)
    {
        var root = await Archives.TarGz(httpCache, Url, directory);

        var borders = Read(Directory.EnumerateDirectories(root, "iso1", SearchOption.AllDirectories).Single())
            .ToDictionary(_ => Country(_.Key), _ => _.Feature);

        var subdivisions = Read(Directory.EnumerateDirectories(root, "iso2", SearchOption.AllDirectories).Single())
            .GroupBy(_ => Country(_.Key))
            .ToDictionary(_ => _.Key, _ => _.Select(item => item.Feature).ToList());

        return new(borders, subdivisions);
    }

    // The leading ISO 3166-1 code: "MC" -> "MC", "US-CA" -> "US".
    static string Country(string code) =>
        code.Split('-')[0].ToUpperInvariant();

    static IEnumerable<(string Key, Feature Feature)> Read(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.geojson"))
        {
            var feature = ReadFeature(path);
            if (feature is not null)
            {
                yield return (Path.GetFileNameWithoutExtension(path), feature);
            }
        }
    }

    // country-levels Features carry a huge "osm_data" blob (and nested objects GeoConvert won't model).
    // Re-emit a minimal Feature with just the geometry and the handful of attributes we keep, then parse.
    static Feature? ReadFeature(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path));
        if (node?["geometry"] is not { } geometry)
        {
            return null;
        }

        var source = node["properties"]?.AsObject();
        var properties = new JsonObject();
        if (source is not null)
        {
            foreach (var key in (string[]) ["name", "iso1", "iso2", "admin_level", "osm_id"])
            {
                if (source.TryGetPropertyValue(key, out var value))
                {
                    properties[key] = value?.DeepClone();
                }
            }
        }

        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "Feature",
                    ["geometry"] = geometry.DeepClone(),
                    ["properties"] = properties,
                }),
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(collection.ToJsonString()));
        return GeoConverter.Read(stream, GeoFormat.GeoJson).Features.FirstOrDefault();
    }
}
