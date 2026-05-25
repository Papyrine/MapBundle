namespace MapBundle;

/// <summary>A single region's map data. Layers are read from FlatGeobuf on demand.</summary>
public sealed class Map
{
    readonly string directory;

    internal Map(string region, string directory)
    {
        Region = region;
        this.directory = directory;
    }

    /// <summary>The region key, for example <c>"World"</c> or <c>"EuropeWestern"</c>.</summary>
    public string Region { get; }

    /// <summary>Whether <paramref name="layer"/> is included in this region's data.</summary>
    public bool Has(MapLayer layer) =>
        File.Exists(PathFor(layer));

    /// <summary>Reads <paramref name="layer"/> into a <see cref="FeatureCollection"/>.</summary>
    public FeatureCollection Load(MapLayer layer)
    {
        var path = PathFor(layer);
        if (!File.Exists(path))
        {
            throw new MapBundleException($"Layer '{layer}' is not included for region '{Region}'.");
        }

        return GeoConverter.Read(path, GeoFormat.FlatGeobuf);
    }

    /// <summary>Country boundary polygons.</summary>
    public FeatureCollection Borders => Load(MapLayer.Borders);

    /// <summary>Populated places (cities and towns).</summary>
    public FeatureCollection Cities => Load(MapLayer.Cities);

    /// <summary>River and lake centerlines.</summary>
    public FeatureCollection Rivers => Load(MapLayer.Rivers);

    /// <summary>Lake and reservoir polygons.</summary>
    public FeatureCollection Lakes => Load(MapLayer.Lakes);

    string PathFor(MapLayer layer) =>
        Path.Combine(directory, FileName(layer));

    /// <summary>The canonical FlatGeobuf file name a layer is stored as inside a data package.</summary>
    public static string FileName(MapLayer layer) =>
        layer switch
        {
            MapLayer.Borders => "borders.fgb",
            MapLayer.Cities => "cities.fgb",
            MapLayer.Rivers => "rivers.fgb",
            MapLayer.Lakes => "lakes.fgb",
            _ => throw new MapBundleException($"Unknown layer '{layer}'."),
        };
}
