/// <summary>
/// The global physical layers from osmdata.openstreetmap.de: simplified land and ocean polygons derived
/// from the OSM coastline. They ship only in EPSG:3857, so features are clipped to a region (by lon/lat
/// bounding box) and reprojected to WGS84 on demand. Coastline is the land outlines, clipped as lines so
/// the region's bounding-box edges don't become fake shoreline.
/// </summary>
public sealed class OsmData
{
    const string landUrl = "https://osmdata.openstreetmap.de/download/simplified-land-polygons-complete-3857.zip";
    const string oceanUrl = "https://osmdata.openstreetmap.de/download/simplified-water-polygons-split-3857.zip";

    FeatureCollection land;
    FeatureCollection ocean;

    // internal so tests can construct an OsmData from synthetic FeatureCollections without
    // hitting the network.
    internal OsmData(FeatureCollection land, FeatureCollection ocean)
    {
        this.land = land;
        this.ocean = ocean;
    }

    /// <summary>Land polygons within <paramref name="bounds"/> (reprojected to WGS84; overflow clipped to the box).</summary>
    public IReadOnlyList<Feature> Land(Envelope bounds) =>
        Polygons(land, bounds);

    /// <summary>Ocean polygons within <paramref name="bounds"/> (reprojected to WGS84; overflow clipped to the box).</summary>
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

    static List<Feature> Polygons(FeatureCollection source, Envelope bounds)
    {
        var result = new List<Feature>();
        foreach (var (feature, projected, contained) in Reproject(source, bounds))
        {
            if (contained)
            {
                result.Add(new(projected) { Id = feature.Id });
            }
            else if (Geo.Clip(projected, bounds) is { } clipped)
            {
                result.Add(new(clipped) { Id = feature.Id });
            }
        }

        return result;
    }

    static List<Feature> Lines(FeatureCollection source, Envelope bounds)
    {
        var result = new List<Feature>();
        foreach (var (_, projected, contained) in Reproject(source, bounds))
        {
            foreach (var line in Geo.Outlines(projected))
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

    // Source is EPSG:3857. Test each feature's (cheap) Mercator bounds against the region in WGS84, then
    // reproject only the survivors, noting whether each falls wholly inside the region.
    static IEnumerable<(Feature Feature, Geometry Projected, bool Contained)> Reproject(FeatureCollection source, Envelope bounds)
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

            var featureBounds = Geo.MercatorToWgs84(geometry.GetBounds());
            if (Geo.Intersects(bounds, featureBounds))
            {
                yield return (feature, Geo.MercatorToWgs84(geometry), Geo.Contains(bounds, featureBounds));
            }
        }
    }
}
