/// <summary>
/// The region catalog, built from Geofabrik's download index. We publish the continents (top-level
/// regions) and their direct children (countries); sub-country levels (US states, German Bundesländer)
/// are skipped. <see cref="World"/> is synthetic and merges every continent.
/// </summary>
public static class Regions
{
    public static readonly Region World = new("world", null, "World", [], null, IsWorld: true);

    // Geofabrik's index assigns the ISO 3166-1 codes that drive every per-region layer lookup, but a few
    // of its named multi-country extracts list only SOME of their members — even though country-levels
    // (the actual borders/states source) ships geometry for all of them. An omitted code silently drops
    // that whole country out of its continent and World: no border, no cities, no states. The gap is easy
    // to miss because the country's landmass still appears via the bbox-based Land/Ocean layers — only the
    // outlined-and-filled border and its label vanish. Patch the missing codes back in, keyed by extract
    // id. (If Geofabrik fixes its index, CorrectedIso dedupes so the entry stays correct.)
    //   gcc-states                 lists QA/AE/OM/BH/KW but not SA — Saudi Arabia, the largest GCC member.
    //   malaysia-singapore-brunei  lists MY but not SG (Singapore) or BN (Brunei), despite its own name.
    static readonly Dictionary<string, string[]> isoCorrections = new(StringComparer.Ordinal)
    {
        ["gcc-states"] = ["SA"],
        ["malaysia-singapore-brunei"] = ["SG", "BN"],
    };

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
            .Select(_ => new Region(_.Id, _.Parent, _.Name, CorrectedIso(_), _.ShpUrl)));
        return regions;
    }

    // The entry's ISO codes plus any isoCorrections entry for it, skipping codes already present so an
    // upstream fix doesn't double them. Order is irrelevant downstream: BuildRegion sorts the merged set.
    static string[] CorrectedIso(GeofabrikEntry entry) =>
        isoCorrections.TryGetValue(entry.Id, out var extra)
            ? [.. entry.Iso2, .. extra.Where(_ => !entry.Iso2.Contains(_))]
            : entry.Iso2;

    /// <summary>
    /// The regions whose ISO codes describe a region (used to look up borders, states and cities).
    /// World merges every region with ISO codes. A continent merges its ISO-carrying children — plus
    /// the continent itself when it is also a country (Russia has its own ISO "RU" alongside child
    /// federal districts that carry no ISO of their own; without including Russia in its own members,
    /// RU would drop out of the iso set and Russia would come back with no borders, bounds or layers).
    /// A country (or a continent that is just a country, like Antarctica) is its own sole member.
    /// Iso-less children of a continent (US states under <c>north-america</c>, Russian federal districts
    /// under <c>russia</c>) are filtered out: they carry no ISO so they contribute nothing to any layer.
    /// </summary>
    public static IReadOnlyList<Region> Members(Region region, IReadOnlyList<Region> regions)
    {
        if (region.IsWorld)
        {
            return [.. regions.Where(_ => _.Iso.Length > 0)];
        }

        if (region.IsContinent)
        {
            var children = regions.Where(_ => _.Parent == region.Id && _.Iso.Length > 0);
            return region.Iso.Length > 0 ? [region, .. children] : [.. children];
        }

        return region.Iso.Length > 0 ? [region] : [];
    }
}
