namespace MapBundle.Builder;

/// <summary>
/// The region catalog, built from Geofabrik's download index. We publish the continents (top-level
/// regions) and their direct children (countries); sub-country levels (US states, German Bundesländer)
/// are skipped. <see cref="World"/> is synthetic and merges every continent.
/// </summary>
public static class Regions
{
    public static readonly Region World = new("world", null, "World", [], null, IsWorld: true);

    /// <summary>Downloads the Geofabrik index and builds the region tree.</summary>
    public static async Task<IReadOnlyList<Region>> Load(HttpCache httpCache, string directory) =>
        Build(await Geofabrik.Index(httpCache, directory));

    /// <summary>Builds the region tree from a Geofabrik index (pure; no network).</summary>
    public static IReadOnlyList<Region> Build(IReadOnlyList<GeofabrikEntry> index)
    {
        var continents = index
            .Where(_ => _.Parent is null)
            .Select(_ => _.Id)
            .ToHashSet();

        List<Region> regions = [World];
        regions.AddRange(index
            .Where(_ => _.Parent is null || continents.Contains(_.Parent))
            .Select(_ => new Region(_.Id, _.Parent, _.Name, _.Iso2, _.ShpUrl)));
        return regions;
    }

    /// <summary>
    /// The Geofabrik extracts whose cities/rivers/lakes make up a region: World merges every extract, a
    /// continent merges its countries (or is itself, if it has none), a country (or childless continent)
    /// is itself.
    /// </summary>
    public static IReadOnlyList<Region> Members(Region region, IReadOnlyList<Region> regions)
    {
        var parents = regions
            .Select(_ => _.Parent)
            .OfType<string>()
            .ToHashSet();

        if (region.IsWorld)
        {
            return [.. regions.Where(_ => IsExtract(_, parents))];
        }

        if (region.IsContinent && parents.Contains(region.Id))
        {
            return [.. regions.Where(_ => _.Parent == region.Id && IsExtract(_, parents))];
        }

        return IsExtract(region, parents) ? [region] : [];
    }

    // A region we actually download a Geofabrik extract for: a country, or a continent with no countries.
    static bool IsExtract(Region region, HashSet<string> parents) =>
        region.ShpUrl is not null &&
        (region.Parent is not null || (region.IsContinent && !parents.Contains(region.Id)));
}
