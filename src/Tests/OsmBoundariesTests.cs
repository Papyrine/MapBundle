// Direct unit tests for the OsmBoundaries source class — the pinned OSM backfill of subdivisions the
// country-levels ISO 3166-2 snapshot is missing. BuildFeature is exercised offline with synthetic
// Nominatim-shaped GeoJSON; the lookup and the pinned table are checked for shape and determinism.
public class OsmBoundariesTests
{
    // A Nominatim lookup response: a FeatureCollection whose single feature carries a nested "address"
    // object (which GeoConvert won't model) — BuildFeature must take only the geometry and re-key it.
    const string nominatimGeoJson =
        """
        {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"name":"محلي","address":{"state":"x","ISO3166-2-lvl4":"DZ-49"}},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}}]}
        """;

    static OsmBoundaries.Backfill Sample => new("DZ", "DZ-49", "Timimoun", 6528164);

    [Test]
    public async Task BuildFeature_keeps_the_geometry_and_re_keys_the_properties()
    {
        var feature = OsmBoundaries.BuildFeature(nominatimGeoJson, Sample)!;

        // The geometry survives; the nested-address noise is dropped in favour of our minimal schema.
        await Assert.That(feature.Geometry!.Type).IsEqualTo(GeometryType.Polygon);
        await Assert.That(feature.Properties["name"]).IsEqualTo("Timimoun");
        await Assert.That(feature.Properties["iso2"]).IsEqualTo("DZ-49");
        await Assert.That(Convert.ToInt32(feature.Properties["admin_level"])).IsEqualTo(4);
    }

    [Test]
    public async Task BuildFeature_returns_null_when_there_is_no_geometry()
    {
        var empty = """{"type":"FeatureCollection","features":[]}""";
        await Assert.That(OsmBoundaries.BuildFeature(empty, Sample)).IsNull();
    }

    [Test]
    public async Task Subdivisions_is_case_insensitive_and_empty_for_unknown()
    {
        var feature = OsmBoundaries.BuildFeature(nominatimGeoJson, Sample)!;
        var boundaries = new OsmBoundaries(new() { ["DZ"] = [feature] });

        await Assert.That(boundaries.Subdivisions("dz").Count).IsEqualTo(1);
        await Assert.That(boundaries.Subdivisions("DZ")[0].Properties["name"]).IsEqualTo("Timimoun");
        await Assert.That(boundaries.Subdivisions("FR")).IsEmpty();
    }

    [Test]
    public async Task The_pinned_table_has_no_duplicate_codes_and_valid_relation_ids()
    {
        var codes = OsmBoundaries.Backfills.Select(_ => $"{_.Country}/{_.Iso2}").ToList();
        await Assert.That(codes.Distinct().Count()).IsEqualTo(codes.Count);
        await Assert.That(OsmBoundaries.Backfills.All(_ => _.Relation > 0)).IsTrue();
        await Assert.That(OsmBoundaries.Backfills.All(_ => _.Name.Length > 0)).IsTrue();
    }
}
