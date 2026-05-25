namespace MapBundle.Builder;

/// <summary>The Natural Earth detail scale. Larger denominators are coarser and smaller on disk.</summary>
public enum Scale
{
    /// <summary>1:110m — coarse, smallest.</summary>
    M110,

    /// <summary>1:50m — medium.</summary>
    M50,

    /// <summary>1:10m — fine, largest. The default.</summary>
    M10,
}

/// <summary>A source layer in the Natural Earth dataset.</summary>
public enum SourceLayer
{
    Countries,
    PopulatedPlaces,
    Rivers,
    Lakes,
}

/// <summary>
/// Describes where Natural Earth source data lives and maps source layers to MapBundle layers.
/// Data is downloaded from the canonical <c>nvkelso/natural-earth-vector</c> GitHub mirror, which is
/// more reliable than the naturalearthdata.com CDN and supports ranged/cached fetches.
/// </summary>
public static class NaturalEarth
{
    public const string Attribution =
        "Made with Natural Earth. Free vector and raster map data @ naturalearthdata.com (public domain).";

    const string MirrorRoot = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master";

    /// <summary>Shapefile components to fetch. The first three are required; the rest are best-effort.</summary>
    public static readonly string[] Components = [".shp", ".shx", ".dbf", ".prj", ".cpg"];

    public static string ScaleToken(Scale scale) =>
        scale switch
        {
            Scale.M110 => "110m",
            Scale.M50 => "50m",
            Scale.M10 => "10m",
            _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null),
        };

    public static bool TryParseScale(string text, out Scale scale)
    {
        scale = text.Trim().ToLowerInvariant() switch
        {
            "110m" or "110" => Scale.M110,
            "50m" or "50" => Scale.M50,
            "10m" or "10" => Scale.M10,
            _ => (Scale)(-1),
        };
        return Enum.IsDefined(scale);
    }

    public static MapLayer ToMapLayer(SourceLayer layer) =>
        layer switch
        {
            SourceLayer.Countries => MapLayer.Borders,
            SourceLayer.PopulatedPlaces => MapLayer.Cities,
            SourceLayer.Rivers => MapLayer.Rivers,
            SourceLayer.Lakes => MapLayer.Lakes,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };

    /// <summary>The mirror sub-folder ("cultural"/"physical") and base file name (no extension) for a layer.</summary>
    public static (string Category, string Name) Source(SourceLayer layer, Scale scale)
    {
        var token = ScaleToken(scale);
        return layer switch
        {
            SourceLayer.Countries => ("cultural", $"ne_{token}_admin_0_countries"),
            SourceLayer.PopulatedPlaces => ("cultural", $"ne_{token}_populated_places"),
            SourceLayer.Rivers => ("physical", $"ne_{token}_rivers_lake_centerlines"),
            SourceLayer.Lakes => ("physical", $"ne_{token}_lakes"),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };
    }

    public static string ComponentUrl(SourceLayer layer, Scale scale, string component)
    {
        var token = ScaleToken(scale);
        var (category, name) = Source(layer, scale);
        return $"{MirrorRoot}/{token}_{category}/{name}{component}";
    }
}
