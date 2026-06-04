public class RegionsTests
{
    // A miniature Geofabrik index: a continent with countries, a sub-country level (excluded), and a
    // continent-sized leaf (Russia) with no children.
    static readonly GeofabrikEntry[] sampleIndex =
    [
        new("europe", null, "Europe", [], null),
        new("monaco", "europe", "Monaco", ["MC"], "https://download.geofabrik.de/europe/monaco-latest-free.shp.zip"),
        new("germany", "europe", "Germany", ["DE"], "https://download.geofabrik.de/europe/germany-latest-free.shp.zip"),
        new("germany/bayern", "germany", "Bayern", [], "https://download.geofabrik.de/europe/germany/bayern-latest-free.shp.zip"),
        new("russia", null, "Russia", ["RU"], "https://download.geofabrik.de/russia-latest-free.shp.zip"),
    ];

    [Test]
    public Task Region_table() =>
        Verify(Describe(Regions.Build(sampleIndex)));

    static string Describe(IReadOnlyList<Region> regions)
    {
        var builder = new StringBuilder();
        foreach (var region in regions)
        {
            var iso = region.Iso.Length == 0 ? "(none)" : string.Join(", ", region.Iso);
            var shp = region.ShpUrl is null ? "no" : "yes";
            builder.Append(
                $"""
                {region.Key}
                  id: {region.Id}
                  parent: {region.Parent ?? "(none)"}
                  iso: {iso}
                  shp: {shp}

                """);
        }

        return builder.ToString();
    }

    [Test]
    public async Task Includes_continents_and_countries_but_not_subcountry()
    {
        var ids = Regions.Build(sampleIndex).Select(_ => _.Id).ToList();
        await Assert.That(ids).Contains("europe");
        await Assert.That(ids).Contains("monaco");
        await Assert.That(ids).Contains("russia");
        await Assert.That(ids.Contains("germany/bayern")).IsFalse();
    }

    [Test]
    public async Task World_is_synthetic_and_first()
    {
        var regions = Regions.Build(sampleIndex);
        await Assert.That(regions[0].IsWorld).IsTrue();
        await Assert.That(regions[0].Key).IsEqualTo("World");
    }

    [Test]
    public async Task Continent_has_no_parent()
    {
        var europe = Regions.Build(sampleIndex).Single(_ => _.Id == "europe");
        await Assert.That(europe.IsContinent).IsTrue();
    }

    [Test]
    public async Task Country_requires_iso_codes_under_a_continent()
    {
        var monaco = new Region("monaco", "europe", "Monaco", ["MC"], null);
        var alps = new Region("alps", "europe", "Alps", [], null);
        var europe = new Region("europe", null, "Europe", [], null);
        await Assert.That(monaco.IsCountry).IsTrue();
        await Assert.That(alps.IsCountry).IsFalse();
        await Assert.That(europe.IsCountry).IsFalse();
        await Assert.That(Regions.World.IsCountry).IsFalse();
    }

    [Test]
    public async Task Key_is_pascal_cased()
    {
        var region = new Region("north-america", null, "North America", [], null);
        await Assert.That(region.Key).IsEqualTo("NorthAmerica");
    }

    static IReadOnlyList<string> MemberIds(string id)
    {
        var regions = Regions.Build(sampleIndex);
        var region = id == "world" ? Regions.World : regions.Single(_ => _.Id == id);
        return Regions.Members(region, regions).Select(_ => _.Id).OrderBy(_ => _).ToList();
    }

    [Test]
    public async Task Country_is_its_own_member() =>
        await Assert.That(MemberIds("monaco")).IsEquivalentTo(["monaco"]);

    [Test]
    public async Task Continent_merges_its_countries() =>
        await Assert.That(MemberIds("europe")).IsEquivalentTo(["germany", "monaco"]);

    [Test]
    public async Task Childless_continent_is_its_own_member() =>
        await Assert.That(MemberIds("russia")).IsEquivalentTo(["russia"]);

    [Test]
    public async Task Continent_with_own_iso_and_children_includes_itself()
    {
        // Russia is a continent (parent=null) with its own ISO ("RU") AND child federal districts that
        // carry no ISO of their own. Members must include Russia itself, otherwise "RU" drops out of the
        // iso set, no border is found, the bbox is empty and every layer comes back zero. The iso-less
        // federal districts contribute no codes to any layer, so they are not Members.
        GeofabrikEntry[] index =
        [
            new("russia", null, "Russia", ["RU"], "shp"),
            new("russia/central-fed-district", "russia", "Central Federal District", [], "shp"),
            new("russia/siberian-fed-district", "russia", "Siberian Federal District", [], "shp"),
        ];
        var regions = Regions.Build(index);
        var russia = regions.Single(_ => _.Id == "russia");
        var members = Regions.Members(russia, regions);
        await Assert.That(members.Select(_ => _.Id)).IsEquivalentTo(["russia"]);
        await Assert.That(members.SelectMany(_ => _.Iso)).Contains("RU");
    }

    [Test]
    public async Task Country_without_shp_url_is_still_a_member()
    {
        // Regression for the missing-big-countries bug: Geofabrik's index does NOT ship per-region shp
        // extracts for large countries (US, Canada, Brazil, France, Germany, Italy, Japan, Poland, UK,
        // Russia). The earlier code filtered Members by ShpUrl, which silently dropped every one of
        // those from their continent's iso set — World/borders.fgb came back with 180 countries instead
        // of ~190 and North America/borders.fgb held only Mexico, Greenland, PR and VI.
        GeofabrikEntry[] index =
        [
            new("north-america", null, "North America", [], null),
            new("us", "north-america", "United States", ["US"], ShpUrl: null),
            new("canada", "north-america", "Canada", ["CA"], ShpUrl: null),
            new("mexico", "north-america", "Mexico", ["MX"], "https://..."),
        ];
        var regions = Regions.Build(index);
        var northAmerica = regions.Single(_ => _.Id == "north-america");
        var members = Regions.Members(northAmerica, regions);
        await Assert.That(members.Select(_ => _.Id)).IsEquivalentTo(["us", "canada", "mexico"]);
        await Assert.That(members.SelectMany(_ => _.Iso).OrderBy(_ => _))
            .IsEquivalentTo(["CA", "MX", "US"]);

        var worldMembers = Regions.Members(Regions.World, regions);
        await Assert.That(worldMembers.SelectMany(_ => _.Iso).OrderBy(_ => _))
            .IsEquivalentTo(["CA", "MX", "US"]);
    }

    [Test]
    public async Task World_merges_every_extract() =>
        await Assert.That(MemberIds("world")).IsEquivalentTo(["germany", "monaco", "russia"]);

    [Test]
    public async Task Multi_country_extracts_get_missing_iso_codes_patched_in()
    {
        // Regression for the missing-countries bug: Geofabrik's index lists only SOME members of a few
        // named multi-country extracts, even though country-levels ships geometry for all of them. The
        // gcc-states entry omits SA (Saudi Arabia, the largest GCC member); malaysia-singapore-brunei
        // omits SG and BN despite its own name. Without the correction those countries silently drop out
        // of their continent and World — borders, cities and states all missing (the landmass still
        // shows via the bbox-based Land/Ocean layers, which is what made the gap easy to overlook).
        GeofabrikEntry[] index =
        [
            new("asia", null, "Asia", [], null),
            new("gcc-states", "asia", "GCC States", ["QA", "AE", "OM", "BH", "KW"], "shp"),
            new("malaysia-singapore-brunei", "asia", "Malaysia, Singapore, and Brunei", ["MY"], "shp"),
        ];
        var regions = Regions.Build(index);

        await Assert.That(regions.Single(_ => _.Id == "gcc-states").Iso.OrderBy(_ => _))
            .IsEquivalentTo(["AE", "BH", "KW", "OM", "QA", "SA"]);
        await Assert.That(regions.Single(_ => _.Id == "malaysia-singapore-brunei").Iso.OrderBy(_ => _))
            .IsEquivalentTo(["BN", "MY", "SG"]);

        // ...and the patched-in codes reach World's merged iso set, so World/borders includes them.
        var worldIso = Regions.Members(Regions.World, regions).SelectMany(_ => _.Iso).ToList();
        await Assert.That(worldIso).Contains("SA");
        await Assert.That(worldIso).Contains("SG");
        await Assert.That(worldIso).Contains("BN");
    }

    [Test]
    public async Task Iso_correction_does_not_duplicate_a_code_already_present()
    {
        // If Geofabrik later adds the missing code itself, the correction must not double it.
        GeofabrikEntry[] index =
        [
            new("asia", null, "Asia", [], null),
            new("gcc-states", "asia", "GCC States", ["QA", "AE", "OM", "BH", "KW", "SA"], "shp"),
        ];
        var gcc = Regions.Build(index).Single(_ => _.Id == "gcc-states");
        await Assert.That(gcc.Iso.Count(_ => _ == "SA")).IsEqualTo(1);
    }

    [Test]
    public async Task Western_Sahara_is_rendered_as_a_synthetic_region()
    {
        // Geofabrik enumerates no Western Sahara region and assigns its ISO (EH) to no extract, so we add
        // it synthetically — otherwise the Polisario "Free Zone" (country-levels' EH polygon) renders as
        // an empty wedge between Morocco, Algeria and Mauritania. Geometry is resolved from country-levels
        // by ISO at build time, exactly like a real Geofabrik country.
        GeofabrikEntry[] index =
        [
            new("africa", null, "Africa", [], null),
            new("morocco", "africa", "Morocco", ["MA"], "shp"),
        ];
        var regions = Regions.Build(index);

        var ws = regions.Single(_ => _.Id == "western-sahara");
        await Assert.That(ws.Iso).IsEquivalentTo(["EH"]);
        await Assert.That(ws.Key).IsEqualTo("WesternSahara");
        await Assert.That(ws.Parent).IsEqualTo("africa");
        await Assert.That(ws.IsCountry).IsTrue();

        // It also rides into Africa's and World's merged borders, so the wedge is filled there too.
        await Assert.That(Regions.Members(regions.Single(_ => _.Id == "africa"), regions).Select(_ => _.Id))
            .Contains("western-sahara");
        await Assert.That(Regions.Members(Regions.World, regions).SelectMany(_ => _.Iso)).Contains("EH");
    }

    [Test]
    public async Task Synthetic_region_is_only_added_when_its_continent_is_built()
    {
        // A partial index that doesn't include the territory's continent must not sprout it (this is also
        // why the Europe-only sampleIndex tests above are unaffected by the synthetic Western Sahara).
        var regions = Regions.Build([new("europe", null, "Europe", [], null)]);
        await Assert.That(regions.Any(_ => _.Id == "western-sahara")).IsFalse();
    }

    [Test]
    public async Task Synthetic_territory_fails_loudly_once_upstream_covers_it()
    {
        // THE CANARY. The synthetic Western Sahara region is a stopgap for Geofabrik not listing EH. The
        // day Western Sahara stops being an unlisted/disputed territory upstream — Geofabrik assigns EH to
        // an extract (here folded into Morocco) or ships a Western Sahara extract — the stand-in is
        // redundant and would render EH twice, so Build throws. The live data build (PackageBuilder
        // .Generate -> Regions.Load -> Build over the real index) trips this automatically, telling us to
        // drop the synthetic entry. The message names the territory and points at the fix.
        GeofabrikEntry[] index =
        [
            new("africa", null, "Africa", [], null),
            new("morocco", "africa", "Morocco", ["MA", "EH"], "shp"), // upstream now covers EH
        ];
        await Assert.That(() => Regions.Build(index)).Throws<InvalidOperationException>();

        var message = CaptureBuildError(index);
        await Assert.That(message).Contains("Western Sahara");
        await Assert.That(message).Contains("EH");
        await Assert.That(message).Contains("syntheticRegions");
    }

    // Build's exception-assertion pattern (Throws<T>()) doesn't surface the message; capture it so the
    // canary test can assert the message is actionable (names the territory and the fix site).
    static string CaptureBuildError(GeofabrikEntry[] index)
    {
        try
        {
            Regions.Build(index);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
        throw new("Expected an InvalidOperationException; none was thrown.");
    }
}
