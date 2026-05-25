public class GeoTests
{
    static Polygon UnitSquare() =>
        new([[new Position(0, 0), new Position(0, 1), new Position(1, 1), new Position(1, 0), new Position(0, 0)]]);

    [Test]
    public async Task Polygon_round_trips_through_nts()
    {
        var back = Geo.ToGeo(Geo.ToNts(UnitSquare()));
        await Assert.That(back.Type).IsEqualTo(GeometryType.Polygon);
        await Assert.That(((Polygon) back).Rings[0].Count).IsEqualTo(5);
    }

    [Test]
    public async Task MultiPolygon_round_trips_through_nts()
    {
        var multi = new MultiPolygon([UnitSquare()]);
        var back = Geo.ToGeo(Geo.ToNts(multi));
        await Assert.That(back.Type).IsEqualTo(GeometryType.MultiPolygon);
    }

    [Test]
    public async Task Mercator_origin_maps_to_lonlat_origin()
    {
        var point = (Point) Geo.MercatorToWgs84(new Point(0, 0));
        await Assert.That(Math.Abs(point.Coordinate.X)).IsLessThan(1e-6);
        await Assert.That(Math.Abs(point.Coordinate.Y)).IsLessThan(1e-6);
    }

    [Test]
    public async Task Mercator_reprojects_a_known_longitude()
    {
        // A quarter of the Web Mercator world width east of the prime meridian is longitude 90°.
        var point = (Point) Geo.MercatorToWgs84(new Point(20037508.342789244 / 2, 0));
        await Assert.That(Math.Abs(point.Coordinate.X - 90)).IsLessThan(1e-3);
    }

    [Test]
    public async Task Simplify_drops_near_collinear_points()
    {
        var line = new LineString([new Position(0, 0), new Position(1, 0.0000001), new Position(2, 0), new Position(3, 0)]);
        var simplified = (LineString) Geo.Simplify(line, 0.001)!;
        await Assert.That(simplified.Positions.Count).IsLessThan(4);
    }

    [Test]
    public async Task Outlines_of_polygon_is_a_line()
    {
        var outlines = Geo.Outlines(UnitSquare()).ToList();
        await Assert.That(outlines.Count).IsEqualTo(1);
        await Assert.That(outlines[0].Type).IsEqualTo(GeometryType.LineString);
    }
}
