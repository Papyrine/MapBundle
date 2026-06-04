using System.Text.Json.Nodes;

/// <summary>
/// The OSM-derived administrative boundaries from the "country-levels" project (pinned release, simplified
/// WGS84 GeoJSON). <c>iso1</c> files are country borders (admin level 2); <c>iso2</c> files are the
/// state/province subdivisions (ISO 3166-2). Each file is a single GeoJSON Feature keyed by ISO code.
/// </summary>
public sealed class CountryLevels
{
    // Borders are the headline layer (one polygon per country), so take them from the detailed "high" tier
    // — "low"/"medium" collapse small countries like Monaco to a triangle. Subdivisions are far more
    // numerous (~9k worldwide), so take them from "medium" to keep package sizes reasonable. Pinned v2.2.0.
    const string bordersUrl = "https://github.com/hyperknot/country-levels/releases/download/v2.2.0/export_high.tgz";
    const string subdivisionsUrl = "https://github.com/hyperknot/country-levels/releases/download/v2.2.0/export_medium.tgz";

    Dictionary<string, Feature> borders;
    Dictionary<string, List<Feature>> subdivisions;

    // internal so tests can construct a CountryLevels from synthetic dictionaries without
    // hitting the network or unpacking the country-levels release.
    internal CountryLevels(Dictionary<string, Feature> borders, Dictionary<string, List<Feature>> subdivisions)
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
        var high = await Archives.TarGz(httpCache, bordersUrl, directory);
        var medium = await Archives.TarGz(httpCache, subdivisionsUrl, directory);

        var borders = Read(Folder(high, "iso1"))
            .ToDictionary(_ => Country(_.Key), _ => _.Feature);

        var subdivisions = Read(Folder(medium, "iso2"))
            .GroupBy(_ => Country(_.Key))
            .ToDictionary(_ => _.Key, _ => _.Select(_ => _.Feature).ToList());

        return new(borders, subdivisions);
    }

    static string Folder(string root, string name) =>
        Directory.EnumerateDirectories(root, name, SearchOption.AllDirectories).Single();

    // The leading ISO 3166-1 code: "MC" -> "MC", "US-CA" -> "US".
    static string Country(string code) =>
        code.Split('-')[0].ToUpperInvariant();

    // Thousands of files, so read them in parallel (each parse is independent and CPU-bound). PLINQ yields
    // results in completion order, not source order, so the list is sorted by ISO code before returning —
    // otherwise the output order is non-deterministic run-to-run. Borders are keyed into a dictionary so
    // that was harmless for them, but SUBDIVISIONS are kept as per-country lists (StatesProvinces), and an
    // unstable order there made the StatesProvinces .fgb bytes AND its semi-transparent-fill preview PNG
    // change on every build — the committed maps/*.StatesProvinces.png churned each render. Sorting here
    // fixes both. internal so the determinism is unit-tested directly (CountryLevelsTests).
    internal static List<(string Key, Feature Feature)> Read(string directory) =>
    [
        .. Directory.EnumerateFiles(directory, "*.geojson", SearchOption.AllDirectories)
            .AsParallel()
            .Select(path => (Key: Path.GetFileNameWithoutExtension(path), Feature: ReadFeature(path)))
            .Where(_ => _.Feature is not null)
            .Select(_ => (_.Key, _.Feature!))
            .OrderBy(_ => _.Key, StringComparer.Ordinal)
    ];

    // country-levels Features carry a huge "osm_data" blob (and nested objects GeoConvert won't model).
    // Re-emit a minimal Feature with just the geometry and the handful of attributes we keep, then parse.
    // Eager Geo.MakeValid here too: country-levels' Douglas-Peucker simplification leaves self-
    // intersecting rings on heavily-indented coastlines (Greenland, Canadian arctic, …) that need
    // NTS Buffer(0) to repair. Previously every region called Repair per feature — for World that
    // was ~97% of BuildRegion's wall-clock (357 s on 190 country borders, averaging ~1.9 s each
    // and topping out at tens of seconds for Russia / Canada / USA). Doing it once at parse time
    // via the parallel AsParallel pipeline amortises the cost across CPU cores and across every
    // BuildRegion call that follows.
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
        var feature = GeoConverter.Read(stream, GeoFormat.GeoJson).Features.FirstOrDefault();
        if (feature?.Geometry is { } parsed)
        {
            feature = new(Geo.MakeValid(parsed), feature.Properties) { Id = feature.Id };
        }

        return feature;
    }
}
