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
}
