/// <summary>
/// The region catalog, built from Geofabrik's download index. We publish the continents (top-level
/// regions) and their direct children (countries); sub-country levels (US states, German Bundesländer)
/// are skipped. <see cref="World"/> is synthetic and merges every continent, and a few disputed
/// territories Geofabrik doesn't enumerate at all (e.g. Western Sahara) are added as synthetic regions —
/// see <c>syntheticRegions</c>.
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

    // Territories country-levels ships borders for but Geofabrik's index does not enumerate AT ALL — no
    // entry, and the ISO assigned to no extract — so without help they never render. Unlike isoCorrections
    // (which patches a code onto an EXISTING extract), these have nothing upstream to attach to, so they
    // are added as whole synthetic regions; the geometry is resolved from country-levels by ISO at build
    // time, exactly like a real Geofabrik country. They are political stopgaps for territories upstream
    // folds into a neighbour, so AddSyntheticRegions throws the moment the live index starts covering one
    // (Western_Sahara is exercised by RegionsTests) — that throw is the canary that the dispute has been
    // settled upstream and the hand-rolled stand-in should be dropped.
    //   western-sahara (EH): country-levels splits Western Sahara into MA (the Moroccan-controlled west,
    //     already rendered via the Morocco region) and EH (the Polisario "Free Zone", ~80.5k km²).
    //     Geofabrik lists neither EH nor a Western Sahara extract, so the Free Zone rendered as an empty
    //     wedge between Morocco, Algeria and Mauritania. Rendering EH closes that gap. Named "Western
    //     Sahara" — the neutral UN/geographic name — not country-levels' recognition-laden "Sahrawi Arab
    //     Democratic Republic".
    static readonly Region[] syntheticRegions =
    [
        new("western-sahara", "africa", "Western Sahara", ["EH"], ShpUrl: null),
    ];

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

        AddSyntheticRegions(regions, index, continents);
        return regions;
    }

    // The entry's ISO codes plus any isoCorrections entry for it, skipping codes already present so an
    // upstream fix doesn't double them. Order is irrelevant downstream: BuildRegion sorts the merged set.
    static string[] CorrectedIso(GeofabrikEntry entry) =>
        isoCorrections.TryGetValue(entry.Id, out var extra)
            ? [.. entry.Iso2, .. extra.Where(_ => !entry.Iso2.Contains(_))]
            : entry.Iso2;

    // Append each syntheticRegions territory whose continent this index actually builds. Before adding one
    // it asserts the live index still doesn't cover it: if Geofabrik has meanwhile assigned the territory's
    // ISO to any extract, or shipped an extract under its id, the synthetic entry is redundant and would
    // render the country twice — so throw with an actionable message. This is the canary the data build
    // trips the day a territory stops being unlisted/disputed upstream.
    static void AddSyntheticRegions(List<Region> regions, IReadOnlyList<GeofabrikEntry> index, HashSet<string> continents)
    {
        var assignedIso = index.SelectMany(CorrectedIso).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = index.Select(_ => _.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var territory in syntheticRegions)
        {
            // Only attach to a continent that is part of this index — keeps a partial index (e.g. the
            // Europe-only one the unit tests use) from sprouting an unrelated Africa region.
            if (!continents.Contains(territory.Parent!))
            {
                continue;
            }

            var iso = territory.Iso.Single();
            if (assignedIso.Contains(iso) || ids.Contains(territory.Id))
            {
                throw new InvalidOperationException(
                    $"Geofabrik's index now covers the synthetic territory '{territory.Name}' ({iso}) — it is " +
                    $"no longer unlisted/disputed upstream. Remove it from Regions.syntheticRegions so the real " +
                    $"index entry is used; otherwise {iso} would render twice.");
            }

            regions.Add(territory);
        }
    }

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
