namespace MapBundle.Builder;

/// <summary>The loaded Natural Earth source layers for a single scale.</summary>
public sealed class Sources(
    Scale scale,
    IReadOnlyList<Country> countries,
    FeatureCollection places,
    FeatureCollection? rivers,
    FeatureCollection? lakes)
{
    public Scale Scale => scale;
    public IReadOnlyList<Country> Countries => countries;
    public FeatureCollection Places => places;
    public FeatureCollection? Rivers => rivers;
    public FeatureCollection? Lakes => lakes;

    public static async Task<Sources> Download(DataCache cache, Scale scale, CancellationToken cancellation = default)
    {
        var countriesPath = await Shapefile(cache, SourceLayer.Countries, scale, required: true, cancellation);
        var placesPath = await Shapefile(cache, SourceLayer.PopulatedPlaces, scale, required: true, cancellation);
        var riversPath = await Shapefile(cache, SourceLayer.Rivers, scale, required: false, cancellation);
        var lakesPath = await Shapefile(cache, SourceLayer.Lakes, scale, required: false, cancellation);

        var countries = GeoConverter.Read(countriesPath!, GeoFormat.Shapefile)
            .Select(ToCountry)
            .ToList();
        var places = GeoConverter.Read(placesPath!, GeoFormat.Shapefile);
        var rivers = riversPath is null ? null : GeoConverter.Read(riversPath, GeoFormat.Shapefile);
        var lakes = lakesPath is null ? null : GeoConverter.Read(lakesPath, GeoFormat.Shapefile);

        return new(scale, countries, places, rivers, lakes);
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
