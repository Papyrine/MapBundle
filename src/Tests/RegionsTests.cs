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
            builder.AppendLine(region.Key);
            builder.AppendLine($"  id: {region.Id}");
            builder.AppendLine($"  parent: {region.Parent ?? "(none)"}");
            builder.AppendLine($"  iso: {(region.Iso.Length == 0 ? "(none)" : string.Join(", ", region.Iso))}");
            builder.AppendLine($"  shp: {(region.ShpUrl is null ? "no" : "yes")}");
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
    public async Task Country_and_sub_continent_are_distinguished_by_iso()
    {
        var monaco = new Region("monaco", "europe", "Monaco", ["MC"], null);
        var alps = new Region("alps", "europe", "Alps", [], null);
        var europe = new Region("europe", null, "Europe", [], null);
        await Assert.That(monaco.IsCountry).IsTrue();
        await Assert.That(monaco.IsSubContinent).IsFalse();
        await Assert.That(alps.IsSubContinent).IsTrue();
        await Assert.That(alps.IsCountry).IsFalse();
        await Assert.That(europe.IsCountry).IsFalse();
        await Assert.That(europe.IsSubContinent).IsFalse();
        await Assert.That(Regions.World.IsCountry).IsFalse();
        await Assert.That(Regions.World.IsSubContinent).IsFalse();
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
        // iso set, no border is found, the bbox is empty and every layer comes back zero.
        GeofabrikEntry[] index =
        [
            new("russia", null, "Russia", ["RU"], "shp"),
            new("russia/central-fed-district", "russia", "Central Federal District", [], "shp"),
            new("russia/siberian-fed-district", "russia", "Siberian Federal District", [], "shp"),
        ];
        var regions = Regions.Build(index);
        var russia = regions.Single(_ => _.Id == "russia");
        var members = Regions.Members(russia, regions);
        var memberIds = members.Select(_ => _.Id).OrderBy(_ => _).ToList();
        var iso = members.SelectMany(_ => _.Iso).ToList();
        await Assert.That(memberIds)
            .IsEquivalentTo(
        [
            "russia", "russia/central-fed-district", "russia/siberian-fed-district",
        ]);
        await Assert.That(iso).Contains("RU");
    }

    [Test]
    public async Task World_merges_every_extract() =>
        await Assert.That(MemberIds("world")).IsEquivalentTo(["germany", "monaco", "russia"]);
}
