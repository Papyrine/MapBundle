/// <summary>
/// The global physical layers from osmdata.openstreetmap.de: simplified land and ocean polygons derived
/// from the OSM coastline. The source ships only in EPSG:3857, so features are reprojected to WGS84 and
/// run through <see cref="Geo.MakeValid"/> (NTS <c>Buffer(0)</c>) eagerly at construction time. Each
/// per-region <see cref="Land"/> / <see cref="Ocean"/> / <see cref="Coastline"/> call is then just a
/// bbox intersect + optional clip against the prepared list — previously this reproject + repair pass
/// was redone for every region, which was ~92% of World's 7-minute build (and most of the cost of
/// every other heavy region: NorthAmerica, Europe, Russia all re-walked nearly the whole global set).
/// </summary>
public sealed class OsmData
{
    const string landUrl = "https://osmdata.openstreetmap.de/download/simplified-land-polygons-complete-3857.zip";
    const string oceanUrl = "https://osmdata.openstreetmap.de/download/simplified-water-polygons-split-3857.zip";

    IReadOnlyList<Prepared> land;
    IReadOnlyList<Prepared> ocean;

    // internal so tests can construct an OsmData from synthetic FeatureCollections without
    // hitting the network. Synthetic data is tiny so the eager Prepare pass is instant; the heavy
    // real-world reproject + Buffer(0) pass on ~77k global features is the production cost we're
    // amortising over every region build that follows.
    internal OsmData(FeatureCollection land, FeatureCollection ocean)
    {
        this.land = Prepare(land);
        this.ocean = Prepare(ocean);
    }

    /// <summary>Land polygons within <paramref name="bounds"/> (overflow clipped to the box).</summary>
    public IReadOnlyList<Feature> Land(Envelope bounds) =>
        Polygons(land, bounds);

    /// <summary>Ocean polygons within <paramref name="bounds"/> (overflow clipped to the box).</summary>
    public IReadOnlyList<Feature> Ocean(Envelope bounds) =>
        Polygons(ocean, bounds);

    /// <summary>Coastlines within <paramref name="bounds"/>: the land outlines, clipped to the box as lines.</summary>
    public IReadOnlyList<Feature> Coastline(Envelope bounds) =>
        Lines(land, bounds);

    public static async Task<OsmData> Download(HttpCache httpCache, string directory)
    {
        var land = await Read(httpCache, landUrl, directory);
        var ocean = await Read(httpCache, oceanUrl, directory);
        return new(land, ocean);
    }

    static async Task<FeatureCollection> Read(HttpCache httpCache, string url, string directory)
    {
        var extracted = await Archives.Zip(httpCache, url, directory);
        var shapefile = Archives.Find(extracted, "*.shp") ??
                        throw new($"No shapefile found under '{extracted}'.");
        return GeoConverter.Read(shapefile, GeoFormat.Shapefile);
    }

    // Reproject EPSG:3857 → WGS84 and validate every feature once at load time. The WGS84 bounds
    // are cached so per-region queries are a cheap envelope intersect; the heavy NTS Buffer(0)
    // pass over ~77k global features happens here, not on every BuildRegion call.
    static List<Prepared> Prepare(FeatureCollection source)
    {
        var prepared = new List<Prepared>(source.Features.Count);
        foreach (var feature in source)
        {
            if (feature.Geometry is not { } geometry)
            {
                continue;
            }

            var projected = Geo.MercatorToWgs84(geometry);
            var repaired = Geo.MakeValid(projected);
            prepared.Add(new(feature.Id, repaired, repaired.GetBounds()));
        }

        return prepared;
    }

    static List<Feature> Polygons(IReadOnlyList<Prepared> source, Envelope bounds)
    {
        var result = new List<Feature>();
        if (bounds.IsEmpty)
        {
            return result;
        }

        foreach (var item in source)
        {
            if (!Geo.Intersects(bounds, item.Bounds))
            {
                continue;
            }

            if (Geo.Contains(bounds, item.Bounds))
            {
                result.Add(new(item.Geometry) { Id = item.Id });
            }
            else if (Geo.Clip(item.Geometry, bounds) is { } clipped)
            {
                result.Add(new(clipped) { Id = item.Id });
            }
        }

        return result;
    }

    static List<Feature> Lines(IReadOnlyList<Prepared> source, Envelope bounds)
    {
        var result = new List<Feature>();
        if (bounds.IsEmpty)
        {
            return result;
        }

        foreach (var item in source)
        {
            if (!Geo.Intersects(bounds, item.Bounds))
            {
                continue;
            }

            var contained = Geo.Contains(bounds, item.Bounds);
            foreach (var line in Geo.Outlines(item.Geometry))
            {
                if (contained)
                {
                    result.Add(new(line));
                }
                else if (Geo.Clip(line, bounds) is { } clipped)
                {
                    result.Add(new(clipped));
                }
            }
        }

        return result;
    }

    /// <summary>A source feature after one-shot reproject + validate, with its WGS84 bbox cached for cheap per-region culling.</summary>
    sealed record Prepared(object? Id, Geometry Geometry, Envelope Bounds);
}
