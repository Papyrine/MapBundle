// Direct unit tests for the NaturalEarth source class. Cities filter by ISO; Rivers/Lakes filter by
// bounding box and simplify. None of this used to be tested at the unit level — the only coverage came
// from running the full slice (network-bound, slow, hard to debug). Several of these tests pinned down
// behaviour that the surface API doesn't make obvious.
public class NaturalEarthTests
{
    static Feature Point(double x, double y, IDictionary<string, object?> props) =>
        new(new Point(new Position(x, y)), props);

    static FeatureCollection Places(params Feature[] features) =>
        [.. features];

    [Test]
    public async Task Cities_keeps_only_features_whose_ISO_A2_matches()
    {
        var places = Places(
            Point(7.4, 43.7, new Dictionary<string, object?> { ["ISO_A2"] = "MC", ["NAME"] = "Monaco", ["POP_MAX"] = 38000L }),
            Point(2.3, 48.8, new Dictionary<string, object?> { ["ISO_A2"] = "FR", ["NAME"] = "Paris",  ["POP_MAX"] = 2148000L }));
        var ne = new NaturalEarth(places, [], []);

        var cities = ne.Cities(new HashSet<string>(["MC"]));

        await Assert.That(cities.Count).IsEqualTo(1);
        await Assert.That(cities[0].Properties["name"]).IsEqualTo("Monaco");
        await Assert.That(cities[0].Properties["population"]).IsEqualTo(38000L);
    }

    [Test]
    public async Task Cities_ignores_iso_case_sensitivity_in_the_attribute_lookup()
    {
        // DBF column names vary in case across Natural Earth releases. The Rename helper has to be
        // case-insensitive on the SOURCE key, or the renamed feature comes back with no name/pop.
        var places = Places(
            Point(0, 0, new Dictionary<string, object?> { ["iso_a2"] = "MC", ["name"] = "Monaco", ["pop_max"] = 38000L }));
        var ne = new NaturalEarth(places, [], []);

        var cities = ne.Cities(new HashSet<string>(["MC"]));

        await Assert.That(cities.Count).IsEqualTo(1);
        await Assert.That(cities[0].Properties["name"]).IsEqualTo("Monaco");
    }

    [Test]
    public async Task Cities_strips_all_attributes_except_name_and_population()
    {
        var places = Places(
            Point(0, 0, new Dictionary<string, object?>
            {
                ["ISO_A2"] = "MC",
                ["NAME"] = "Monaco",
                ["POP_MAX"] = 38000L,
                ["TIMEZONE"] = "Europe/Monaco",
                ["FEATURECLA"] = "Populated place",
            }));
        var ne = new NaturalEarth(places, [], []);

        var properties = ne.Cities(new HashSet<string>(["MC"]))[0].Properties;
        await Assert.That(properties.Keys.OrderBy(_ => _).ToList()).IsEquivalentTo(["name", "population"]);
    }

    [Test]
    public async Task Cities_emits_nothing_when_iso_set_empty()
    {
        var places = Places(Point(0, 0, new Dictionary<string, object?> { ["ISO_A2"] = "MC", ["NAME"] = "Monaco" }));
        var ne = new NaturalEarth(places, [], []);
        await Assert.That(ne.Cities(new HashSet<string>()).Count).IsEqualTo(0);
    }

    static Feature Line(IReadOnlyList<(double, double)> coords, IDictionary<string, object?> props) =>
        new(new LineString([.. coords.Select(c => new Position(c.Item1, c.Item2))]), props);

    [Test]
    public async Task Rivers_returns_nothing_for_an_empty_bounds()
    {
        var rivers = new FeatureCollection
        {
            Line([(0, 0), (1, 1)], new Dictionary<string, object?> { ["name"] = "x" }),
        };
        var ne = new NaturalEarth([], rivers, []);
        await Assert.That(ne.Rivers(Envelope.Empty).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Rivers_drops_features_with_no_name()
    {
        var rivers = new FeatureCollection
        {
            Line([(0, 0), (1, 1)], new Dictionary<string, object?> { ["name"] = "Real River" }),
            Line([(0, 0), (1, 1)], new Dictionary<string, object?> { ["name"] = "" }),
            Line([(0, 0), (1, 1)], new Dictionary<string, object?>()),
        };
        var ne = new NaturalEarth([], rivers, []);
        await Assert.That(ne.Rivers(new Envelope(-10, -10, 10, 10)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Rivers_drops_features_outside_the_box()
    {
        var rivers = new FeatureCollection
        {
            Line([(0, 0), (1, 1)],     new Dictionary<string, object?> { ["name"] = "inside" }),
            Line([(50, 50), (60, 60)], new Dictionary<string, object?> { ["name"] = "outside" }),
        };
        var ne = new NaturalEarth([], rivers, []);

        var result = ne.Rivers(new Envelope(-5, -5, 5, 5));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Properties["name"]).IsEqualTo("inside");
    }

    static Feature Square(double cx, double cy, double half, IDictionary<string, object?> props) =>
        new(new Polygon([new Position[]
        {
            new(cx - half, cy - half), new(cx + half, cy - half),
            new(cx + half, cy + half), new(cx - half, cy + half),
            new(cx - half, cy - half),
        }]), props);

    [Test]
    public async Task Lakes_clips_partial_overlap_to_the_box()
    {
        // The lake's bbox is (0..10, 0..10), the region box covers (0..5, 0..5) — the result should be
        // clipped, not kept whole.
        var lakes = new FeatureCollection
        {
            Square(5, 5, 5, new Dictionary<string, object?> { ["name"] = "Big Lake" }),
        };
        var ne = new NaturalEarth([], [], lakes);

        var result = ne.Lakes(new Envelope(0, 0, 5, 5));
        await Assert.That(result.Count).IsEqualTo(1);
        // Clipped area is the quadrant in [0..5, 0..5], so the bbox shouldn't extend past 5.
        var bounds = result[0].Geometry!.GetBounds();
        await Assert.That(bounds.MaxX).IsLessThanOrEqualTo(5);
        await Assert.That(bounds.MaxY).IsLessThanOrEqualTo(5);
    }
}
