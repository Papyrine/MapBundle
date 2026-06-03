// Compiled-but-unreferenced samples used by MarkdownSnippets to keep the readme in sync.
// These methods are never invoked; they exist so the snippets are real, compiling code.
static class Snippets
{
    public static void Usage()
    {
        #region usage
        var map = Maps.Open().Load("Monaco");

        var borders = map.Borders;        // country polygons
        var cities = map.Cities;          // populated places
        var states = map.StatesProvinces; // admin-1 polygons
        var rivers = map.Rivers;          // rivers
        // also: map.Lakes, map.Coastline, map.Land, map.Ocean
        #endregion

        _ = (borders, cities, states, rivers);
    }

    public static void Convert()
    {
        #region convert
        var map = Maps.Open().Load("Monaco");

        // Layers come back as GeoConvert FeatureCollections, so GeoConvert can write
        // them out in another format or rasterise them directly — no extra dependency.
        var borders = map.Load(MapLayer.Borders);

        // Convert the layer to GeoJSON (any GeoFormat works: Kml, TopoJson, Shapefile, …).
        GeoConverter.Write(borders, "borders.geojson", GeoFormat.GeoJson);

        // Render the layer to a PNG (pass several collections to stack them bottom-up).
        MapRenderer.RenderPng([borders], "borders.png", new() { Width = 1024 });
        #endregion
    }
}
