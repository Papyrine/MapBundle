// Direct unit tests for PackageBuilder's pipeline plumbing. Repair fixes self-intersecting rings
// (country-levels' simplification artifacts) on the polygon layers before serialization. Ring winding
// is now enforced by GeoConvert's FlatGeobuf writer, not here.
public class PackageBuilderTests
{
    static Feature Bowtie() =>
        new(new Polygon([[new(0, 0), new(1, 1), new(1, 0), new(0, 1), new(0, 0)]]),
            new Dictionary<string, object?> { ["k"] = "v" });

    static Feature Repair(Feature feature) =>
        (Feature) typeof(PackageBuilder)
            .GetMethod("Repair", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [feature])!;

    [Test]
    public async Task Repair_fixes_self_intersecting_polygons()
    {
        // The country-levels failure mode: a Douglas-Peucker simplification that introduces a
        // self-intersection. Repair must call MakeValid (Buffer-0) so the output is topologically valid.
        var repaired = Repair(Bowtie());
        await Assert.That(Geo.ToNts(repaired.Geometry!).IsValid).IsTrue();
    }

    [Test]
    public async Task Repair_passes_through_features_without_geometry()
    {
        // A property-only Feature has no geometry. Repair must not crash.
        var feature = new Feature(geometry: null, new Dictionary<string, object?> { ["k"] = "v" });
        var repaired = Repair(feature);
        await Assert.That(repaired.Geometry).IsNull();
        await Assert.That(repaired.Properties["k"]).IsEqualTo("v");
    }

    [Test]
    public async Task Repair_does_not_mutate_its_input()
    {
        // Regression: when CountryLevels.Border hands the same cached Feature to every region that
        // needs it and BuildAsync runs regions in parallel, a mutating Repair causes thread-thread
        // interference. The repaired geometry must come back on a NEW Feature so the cache stays
        // immutable across the build.
        var original = Bowtie();
        var originalGeometry = original.Geometry;

        var repaired = Repair(original);

        await Assert.That(ReferenceEquals(repaired, original)).IsFalse();
        await Assert.That(ReferenceEquals(original.Geometry, originalGeometry)).IsTrue();
    }
}
