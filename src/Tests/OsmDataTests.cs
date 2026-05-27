// Direct unit tests for OsmData. Land/Ocean/Coastline pipeline: source is in EPSG:3857, must be
// pre-filtered cheaply against a WGS84 region bounds, then reprojected, then clipped if it spills past
// the box. Coastline derives lines from the land outlines (including hole rings).
public class OsmDataTests
{
    static Feature MercatorSquare(double cx, double cy, double half) =>
        new(new Polygon(
        [
            [
                new(cx - half, cy - half), new(cx + half, cy - half),
                new(cx + half, cy + half), new(cx - half, cy + half),
                new(cx - half, cy - half)
            ]
        ]));

    [Test]
    public async Task Land_reprojects_from_mercator_to_wgs84()
    {
        // A Mercator square centered at (0, 0) maps to a WGS84 square centered at (0°, 0°). Use a
        // 1m square so the lat/lon delta is tiny.
        var land = new FeatureCollection
        {
            MercatorSquare(0, 0, 1)
        };
        var osm = new OsmData(land, []);

        var result = osm.Land(new(-1, -1, 1, 1));

        await Assert.That(result.Count).IsEqualTo(1);
        var bounds = result[0].Geometry!.GetBounds();
        // EPSG:4326 longitude/latitude near zero, magnitude well below a degree.
        await Assert.That(Math.Abs(bounds.MinX)).IsLessThan(1.0);
        await Assert.That(Math.Abs(bounds.MaxY)).IsLessThan(1.0);
    }

    [Test]
    public async Task Land_drops_features_disjoint_from_bounds()
    {
        // ~10° Mercator square far from the equator vs a tight bbox at (0, 0).
        var land = new FeatureCollection
        {
            MercatorSquare(10_000_000, 0, 100_000)
        };
        var osm = new OsmData(land, []);
        var result = osm.Land(new(-1, -1, 1, 1));
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Land_returns_empty_when_bounds_empty()
    {
        var land = new FeatureCollection
        {
            MercatorSquare(0, 0, 1)
        };
        var osm = new OsmData(land, []);
        await Assert.That(osm.Land(Envelope.Empty).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Ocean_uses_the_ocean_collection()
    {
        var land = new FeatureCollection
        {
            MercatorSquare(0, 0, 1)
        };
        var ocean = new FeatureCollection
        {
            MercatorSquare(0, 0, 2)
        };
        var osm = new OsmData(land, ocean);

        var landResult = osm.Land(new(-1, -1, 1, 1));
        var oceanResult = osm.Ocean(new(-1, -1, 1, 1));

        await Assert.That(landResult.Count).IsEqualTo(1);
        await Assert.That(oceanResult.Count).IsEqualTo(1);
        // Ocean square is bigger, so its WGS84 bbox is also larger.
        await Assert.That(oceanResult[0].Geometry!.GetBounds().MaxX)
            .IsGreaterThan(landResult[0].Geometry!.GetBounds().MaxX);
    }

    [Test]
    public async Task Coastline_emits_line_strings_from_polygon_outlines()
    {
        // One polygon → one outline (the exterior ring), as a line.
        var land = new FeatureCollection
        {
            MercatorSquare(0, 0, 1)
        };
        var osm = new OsmData(land, []);
        var lines = osm.Coastline(new(-1, -1, 1, 1));
        await Assert.That(lines.Count).IsEqualTo(1);
        var type = lines[0].Geometry!.Type;
        await Assert.That(type is GeometryType.LineString or GeometryType.MultiLineString).IsTrue();
    }
}
