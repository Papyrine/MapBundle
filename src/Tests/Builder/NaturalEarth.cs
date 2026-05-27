/// <summary>
/// The Natural Earth global vector layers for the three "major feature" themes this repo ships: populated
/// places (cities), river + lake centerlines (rivers) and lakes. Natural Earth is public domain, ships in
/// WGS84 and is global in a single small download per theme, so each layer is fetched once and selected per
/// region on demand: cities by ISO country code, rivers and lakes by bounding-box clip (the latter the same
/// pattern <see cref="OsmData"/> uses for land/ocean). This replaces the multi-gigabyte per-region Geofabrik
/// shapefile extracts that previously fed these layers — Geofabrik's index still drives the region tree,
/// but its bulk extracts are no longer downloaded.
/// </summary>
public sealed class NaturalEarth
{
    // The canonical nvkelso/natural-earth-vector mirror serves each shapefile's components as raw files;
    // it is more reliable than the naturalearthdata.com CDN and works with the HTTP (Replicant) cache.
    const string mirrorRoot = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master";

    // A shapefile is multi-file: GeoConvert needs the .shp/.shx/.dbf siblings; .prj/.cpg are optional.
    static readonly string[] components = [".shp", ".shx", ".dbf", ".prj", ".cpg"];

    // 1:10m geometry is already generalized; a light simplify trims size with no visible change. ~0.0005° ≈ 50 m.
    const double tolerance = 0.0005;

    FeatureCollection places;
    FeatureCollection rivers;
    FeatureCollection lakes;

    // internal so tests can construct a NaturalEarth from synthetic FeatureCollections without
    // hitting the network.
    internal NaturalEarth(FeatureCollection places, FeatureCollection rivers, FeatureCollection lakes)
    {
        this.places = places;
        this.rivers = rivers;
        this.lakes = lakes;
    }

    /// <summary>
    /// Populated places (points) in the given countries, trimmed to name and population. Selected by ISO
    /// 3166-1 alpha-2 (like borders and states) rather than by bounding box, so a small country keeps its
    /// own capital even when Natural Earth places the point just outside the tight border bbox (Monaco's
    /// point sits ~180 m west of its country-levels border), and neighbours' cities don't bleed in.
    /// </summary>
    public IReadOnlyList<Feature> Cities(ISet<string> iso) =>
    [
        .. places
            .Where(_ => iso.Contains(Props.Text(_, "ISO_A2")))
            .Select(_ => Rename(_, ("name", "name"), ("population", "pop_max")))
    ];

    /// <summary>Named river + lake centerlines (lines) clipped to <paramref name="bounds"/> and simplified.</summary>
    public IReadOnlyList<Feature> Rivers(Envelope bounds) =>
    [
        .. Clip(rivers, bounds)
            .Where(Named)
            .Select(_ => Rename(_, ("name", "name")))
    ];

    /// <summary>Named lakes + reservoirs (polygons) clipped to <paramref name="bounds"/> and simplified.</summary>
    public IReadOnlyList<Feature> Lakes(Envelope bounds) =>
    [
        .. Clip(lakes, bounds)
            .Where(Named)
            .Select(_ => Rename(_, ("name", "name")))
    ];

    public static async Task<NaturalEarth> Download(HttpCache httpCache, string directory)
    {
        var places = await Read(httpCache, directory, "cultural", "ne_10m_populated_places");
        var rivers = await Read(httpCache, directory, "physical", "ne_10m_rivers_lake_centerlines");
        var lakes = await Read(httpCache, directory, "physical", "ne_10m_lakes");
        return new(places, rivers, lakes);
    }

    // Downloads each shapefile component (cached by Replicant) as a sibling file so GeoConvert reads them
    // together. The .prj/.cpg sidecars are not present for every layer, so a 404 on those is ignored.
    static async Task<FeatureCollection> Read(HttpCache httpCache, string directory, string category, string name)
    {
        Directory.CreateDirectory(directory);
        string? shapefile = null;
        foreach (var component in components)
        {
            var url = $"{mirrorRoot}/10m_{category}/{name}{component}";
            var path = Path.Combine(directory, name + component);
            try
            {
                await httpCache.ToFileAsync(url, path);
            }
            catch (HttpRequestException) when (component is ".prj" or ".cpg")
            {
                continue;
            }

            if (component == ".shp")
            {
                shapefile = path;
            }
        }

        return GeoConverter.Read(shapefile!, GeoFormat.Shapefile);
    }

    // Lines/polygons trimmed to the box: kept whole when wholly inside, clipped otherwise, then simplified.
    // Natural Earth is already WGS84, so (unlike osmdata) there is no reprojection step.
    static IEnumerable<Feature> Clip(FeatureCollection source, Envelope bounds)
    {
        if (bounds.IsEmpty)
        {
            yield break;
        }

        foreach (var feature in source)
        {
            if (feature.Geometry is not { } geometry)
            {
                continue;
            }

            var featureBounds = geometry.GetBounds();
            if (!Geo.Intersects(bounds, featureBounds))
            {
                continue;
            }

            var trimmed = Geo.Contains(bounds, featureBounds) ? geometry : Geo.Clip(geometry, bounds);
            if (trimmed is null)
            {
                continue;
            }

            var simplified = Geo.Simplify(trimmed, tolerance);
            if (simplified is not null)
            {
                yield return new(simplified) { Id = feature.Id, Properties = feature.Properties };
            }
        }
    }

    static bool Named(Feature feature) =>
        Props.Text(feature, "name").Length > 0;

    // Re-emit a feature carrying only the wanted attributes, each copied (case-insensitively, since DBF
    // column names vary in case) from a source column into a normalised lower-case key.
    static Feature Rename(Feature feature, params (string To, string From)[] map)
    {
        var renamed = new Feature(feature.Geometry) { Id = feature.Id };
        foreach (var (to, from) in map)
        {
            foreach (var pair in feature.Properties)
            {
                if (string.Equals(pair.Key, from, StringComparison.OrdinalIgnoreCase))
                {
                    renamed.Properties[to] = pair.Value;
                    break;
                }
            }
        }

        return renamed;
    }
}
