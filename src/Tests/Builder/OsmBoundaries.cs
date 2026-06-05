using System.Text.Json.Nodes;

/// <summary>
/// A small, pinned backfill of administrative subdivisions that the <see cref="CountryLevels"/> source
/// is missing because its ISO 3166-2 snapshot (release v2.2.0) predates a territorial reform. Each entry
/// is fetched from OpenStreetMap by boundary-relation id (Nominatim's <c>lookup</c>, which returns ready
/// GeoJSON), re-keyed to the same minimal schema country-levels uses (name / iso2 / admin_level), and
/// merged into the StatesProvinces layer for its country.
/// <para>
/// This mirrors the spirit of <see cref="Regions"/>' <c>isoCorrections</c> / <c>syntheticRegions</c>:
/// a named, tested patch over an upstream gap. The current entries are the seven Algerian wilayas created
/// in the 2019 reform that country-levels omits (it ships only 51 of 58, and even mis-codes El M'Ghair
/// and In Guezzam), which left a large gap in the south-centre of <c>Algeria.StatesProvinces</c>. The
/// codes here are the official ISO 3166-2:2019 values (which match OSM, not country-levels' older set).
/// </para>
/// </summary>
public sealed class OsmBoundaries
{
    // Nominatim returns one administrative boundary as a GeoJSON FeatureCollection with the full polygon
    // (polygon_geojson=1). Looking up a single, pinned relation id (cached by Replicant) is well within
    // its usage policy; the shared HttpClient sends an identifying User-Agent (see PackageBuilder).
    const string lookupUrl = "https://nominatim.openstreetmap.org/lookup?osm_ids=R{0}&format=geojson&polygon_geojson=1";

    // The pinned backfill table, in a stable order so the merged StatesProvinces feature order (and thus
    // the .fgb bytes and preview PNG) is deterministic run-to-run.
    internal static readonly IReadOnlyList<Backfill> Backfills =
    [
        new("DZ", "DZ-49", "Timimoun", 6528164),
        new("DZ", "DZ-50", "Bordj Badji Mokhtar", 6528163),
        new("DZ", "DZ-52", "Béni Abbès", 6824843),
        new("DZ", "DZ-53", "In Salah", 6824900),
        new("DZ", "DZ-55", "Touggourt", 6822397),
        new("DZ", "DZ-56", "Djanet", 6825876),
        new("DZ", "DZ-58", "El Meniaa", 6825901),
    ];

    readonly Dictionary<string, List<Feature>> byCountry;

    // internal so tests can construct an OsmBoundaries from synthetic features without hitting the network.
    internal OsmBoundaries(Dictionary<string, List<Feature>> byCountry) =>
        this.byCountry = byCountry;

    /// <summary>The backfilled subdivision features for a country (ISO 3166-1 alpha-2), if any.</summary>
    public IReadOnlyList<Feature> Subdivisions(string countryIso) =>
        byCountry.GetValueOrDefault(countryIso.ToUpperInvariant()) ?? [];

    public static async Task<OsmBoundaries> Download(HttpCache httpCache, string directory)
    {
        Directory.CreateDirectory(directory);

        var byCountry = new Dictionary<string, List<Feature>>(StringComparer.OrdinalIgnoreCase);
        foreach (var backfill in Backfills)
        {
            var url = string.Format(lookupUrl, backfill.Relation);
            var path = Path.Combine(directory, $"{backfill.Relation}.geojson");
            await httpCache.ToFileAsync(url, path);

            var feature = BuildFeature(File.ReadAllText(path), backfill);
            if (feature is null)
            {
                continue;
            }

            if (!byCountry.TryGetValue(backfill.Country, out var list))
            {
                list = [];
                byCountry[backfill.Country] = list;
            }

            list.Add(feature);
        }

        return new(byCountry);
    }

    // Extracts just the geometry from Nominatim's GeoJSON and re-emits a minimal Feature carrying our own
    // name / iso2 / admin_level — the same shape CountryLevels produces, so the merged layer is uniform.
    // (Nominatim's own properties include a nested "address" object GeoConvert won't model, which is why
    // we rebuild rather than read the response feature directly, exactly like CountryLevels.ReadFeature.)
    // internal + string-in so it is unit-tested offline. Geo.MakeValid repairs any self-intersections.
    internal static Feature? BuildFeature(string geoJson, Backfill backfill)
    {
        var node = JsonNode.Parse(geoJson);
        // Indexing an empty JsonArray throws, so check Count before reaching for the first feature.
        if (node?["features"] is not JsonArray { Count: > 0 } features ||
            features[0]?["geometry"] is not { } geometry)
        {
            return null;
        }

        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "Feature",
                    ["geometry"] = geometry.DeepClone(),
                    ["properties"] = new JsonObject
                    {
                        ["name"] = backfill.Name,
                        ["iso2"] = backfill.Iso2,
                        ["admin_level"] = 4,
                    },
                }),
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(collection.ToJsonString()));
        var feature = GeoConverter.Read(stream, GeoFormat.GeoJson).Features.FirstOrDefault();
        if (feature?.Geometry is not { } parsed)
        {
            return null;
        }

        return new(Geo.MakeValid(parsed), feature.Properties) { Id = feature.Id };
    }

    /// <summary>One backfilled subdivision: its country, ISO 3166-2 code, display name and OSM relation id.</summary>
    internal readonly record struct Backfill(string Country, string Iso2, string Name, long Relation);
}
