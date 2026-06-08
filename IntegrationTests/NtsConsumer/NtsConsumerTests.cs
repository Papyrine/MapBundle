// Verifies the raw-files-into-NetTopologySuite sample: the data package's FlatGeobuf layers land in
// maps/Monaco (the default build behaviour), and a consumer reads them directly through the FlatGeobuf
// reader into NTS geometries - then runs topology operations the MapBundle core itself does not expose.
// Building this project is the integration test; these assertions inspect the data the build produced.
public class NtsConsumerTests
{
    static string RegionDirectory => Path.Combine(AppContext.BaseDirectory, "maps", "Monaco");

    // Deserialize a raw .fgb straight into an NTS feature collection - no MapBundle.Maps, no GeoConvert.
    static FeatureCollection ReadLayer(string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(RegionDirectory, fileName));
        return FeatureCollectionConversions.Deserialize(bytes);
    }

    [Test]
    public async Task Raw_layers_deserialize_into_nts_geometries()
    {
        var borders = ReadLayer("borders.fgb");
        await Assert.That(borders.Count).IsGreaterThan(0);

        var geometry = borders[0].Geometry;
        await Assert.That(geometry).IsAssignableTo<Geometry>();
        await Assert.That(geometry.IsValid).IsTrue();
        await Assert.That(geometry.Area).IsGreaterThan(0);
    }

    [Test]
    public async Task Nts_topology_operations_run_on_the_raw_data()
    {
        var borders = ReadLayer("borders.fgb");

        // Union every border polygon, then exercise NTS ops (area, centroid, buffer) on the result -
        // the kind of geometry processing a consumer reaches for NetTopologySuite to do.
        var geometries = borders.Select(_ => _.Geometry).ToArray();
        var union = geometries[0].Factory.CreateGeometryCollection(geometries).Union();

        await Assert.That(union.Area).IsGreaterThan(0);

        // Monaco sits at roughly 7.42 deg E, 43.74 deg N (WGS84 lon/lat).
        var centroid = union.Centroid.Coordinate;
        await Assert.That(centroid.X).IsGreaterThan(7).And.IsLessThan(7.6);
        await Assert.That(centroid.Y).IsGreaterThan(43).And.IsLessThan(44);

        // Buffering outward grows the footprint - proves the geometry is live, processable NTS data.
        var buffered = union.Buffer(0.01);
        await Assert.That(buffered.Area).IsGreaterThan(union.Area);
    }
}
