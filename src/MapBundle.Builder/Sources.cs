namespace MapBundle.Builder;

/// <summary>The loaded Natural Earth source layers for a single scale.</summary>
public sealed class Sources(
    Scale scale,
    IReadOnlyList<Country> countries,
    IReadOnlyDictionary<MapLayer, FeatureCollection> layers)
{
    public Scale Scale => scale;

    /// <summary>Admin-0 countries, parsed for region selection; the source of the Borders layer.</summary>
    public IReadOnlyList<Country> Countries => countries;

    /// <summary>All other layers that were available at this scale, keyed by target layer.</summary>
    public IReadOnlyDictionary<MapLayer, FeatureCollection> Layers => layers;

    // Everything except admin-0 countries (which becomes Borders via the parsed Country list).
    static readonly SourceLayer[] extraLayers =
    [
        SourceLayer.PopulatedPlaces,
        SourceLayer.StatesProvinces,
        SourceLayer.UrbanAreas,
        SourceLayer.Rivers,
        SourceLayer.Lakes,
        SourceLayer.MinorIslands,
        SourceLayer.Coastline,
        SourceLayer.Land,
        SourceLayer.Ocean,
    ];

    public static async Task<Sources> Download(DataCache cache, Scale scale, CancellationToken cancellation = default)
    {
        var countriesPath = await Shapefile(cache, SourceLayer.Countries, scale, required: true, cancellation);
        var countries = GeoConverter.Read(countriesPath!, GeoFormat.Shapefile)
            .Select(ToCountry)
            .ToList();

        var layers = new Dictionary<MapLayer, FeatureCollection>();
        foreach (var source in extraLayers)
        {
            var path = await Shapefile(cache, source, scale, required: false, cancellation);
            if (path is not null)
            {
                layers[NaturalEarth.ToMapLayer(source)] = GeoConverter.Read(path, GeoFormat.Shapefile);
            }
        }

        return new(scale, countries, layers);
    }

    static async Task<string?> Shapefile(DataCache cache, SourceLayer layer, Scale scale, bool required, CancellationToken cancellation)
    {
        var (_, name) = NaturalEarth.Source(layer, scale);
        string? shp = null;
        foreach (var component in NaturalEarth.Components)
        {
            var componentRequired = required && component is ".shp" or ".shx" or ".dbf";
            var url = NaturalEarth.ComponentUrl(layer, scale, component);
            var path = await cache.Download(url, name + component, componentRequired, cancellation);
            if (component == ".shp")
            {
                shp = path;
            }
        }

        return shp;
    }

    static Country ToCountry(Feature feature)
    {
        var iso = Props.Text(feature, "ADM0_A3");
        if (iso.Length == 0)
        {
            iso = Props.Text(feature, "ISO_A3");
        }

        return new(
            iso,
            Props.Text(feature, "NAME"),
            Props.Text(feature, "CONTINENT"),
            Props.Text(feature, "SUBREGION"),
            feature);
    }
}
