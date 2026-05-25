namespace MapBundle.Builder;

/// <summary>
/// The region catalog. Grouping follows the UN M49 geoscheme (the sub-region values carried in
/// Natural Earth's <c>SUBREGION</c>/<c>CONTINENT</c> attributes), with a few deliberate overrides:
/// <list type="bullet">
///   <item>Mexico is placed in <c>AmericasNorthern</c> rather than M49 "Central America".</item>
///   <item>Iran is placed in <c>AsiaWestern</c> (and excluded from <c>AsiaSouthern</c>).</item>
///   <item><c>MiddleEast</c> = Western Asia + Egypt + Iran. It overlaps AsiaWestern and AfricaNorthern.</item>
///   <item>Russia is assigned whole to <c>EuropeEastern</c> (Natural Earth lists it under Europe); no clipping.</item>
/// </list>
/// </summary>
public static class Regions
{
    public static readonly Region World =
        new("World", "World", [], [], [], [], All: true);

    public static IReadOnlyList<Region> All { get; } = Build();

    static Region Continent(string key, string name, params string[] continents) =>
        new(key, name, [], continents, [], []);

    static Region Sub(string key, string name, params string[] subregions) =>
        new(key, name, subregions, [], [], []);

    static List<Region> Build() =>
    [
        World,

        Continent("Africa", "Africa", "Africa"),
        Sub("AfricaNorthern", "Northern Africa", "Northern Africa"),
        Sub("AfricaWestern", "Western Africa", "Western Africa"),
        Sub("AfricaCentral", "Central Africa", "Middle Africa"),
        Sub("AfricaEastern", "Eastern Africa", "Eastern Africa"),
        Sub("AfricaSouthern", "Southern Africa", "Southern Africa"),

        Continent("Americas", "Americas", "North America", "South America"),
        Sub("AmericasNorthern", "Northern America", "Northern America") with { IncludeIso = ["MEX"] },
        Sub("AmericasCentral", "Central America", "Central America") with { ExcludeIso = ["MEX"] },
        Sub("AmericasSouthern", "South America", "South America"),
        Sub("AmericasCaribbean", "Caribbean", "Caribbean"),

        Continent("Asia", "Asia", "Asia"),
        Sub("AsiaCentral", "Central Asia", "Central Asia"),
        Sub("AsiaEastern", "Eastern Asia", "Eastern Asia"),
        Sub("AsiaSouthEastern", "South-Eastern Asia", "South-Eastern Asia"),
        Sub("AsiaSouthern", "Southern Asia", "Southern Asia") with { ExcludeIso = ["IRN"] },
        Sub("AsiaWestern", "Western Asia", "Western Asia") with { IncludeIso = ["IRN"] },

        Continent("Europe", "Europe", "Europe"),
        Sub("EuropeNorthern", "Northern Europe", "Northern Europe"),
        Sub("EuropeWestern", "Western Europe", "Western Europe"),
        Sub("EuropeSouthern", "Southern Europe", "Southern Europe"),
        Sub("EuropeEastern", "Eastern Europe", "Eastern Europe"),

        Continent("Oceania", "Oceania", "Oceania"),

        new("MiddleEast", "Middle East", ["Western Asia"], [], ["EGY", "IRN"], []),
    ];
}
