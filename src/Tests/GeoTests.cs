public class GeoTests
{
    static Polygon UnitSquare() =>
        new([[new(0, 0), new(0, 1), new(1, 1), new(1, 0), new(0, 0)]]);

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
        var line = new LineString([new(0, 0), new(1, 0.0000001), new(2, 0), new(3, 0)]);
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

    [Test]
    public async Task MakeValid_leaves_a_correctly_oriented_polygon_valid()
    {
        // A CCW square is GeoJSON RFC 7946 — MakeValid should leave it valid (and still CCW).
        var input = new Polygon([[new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0)]]);
        var output = Geo.MakeValid(input);
        var nts = (NetTopologySuite.Geometries.Polygon) Geo.ToNts(output);
        await Assert.That(nts.IsValid).IsTrue();
        await Assert.That(nts.Shell.IsCCW).IsTrue();
    }

    [Test]
    public async Task MakeValid_repairs_a_self_intersecting_bowtie()
    {
        // A bowtie: the diagonal crosses, so the single ring self-intersects. This is the country-levels
        // failure mode that produced the Greenland / Canada / Russia render artifacts.
        var bowtie = new Polygon([[new(0, 0), new(1, 1), new(1, 0), new(0, 1), new(0, 0)]]);
        var repaired = Geo.MakeValid(bowtie);
        await Assert.That(Geo.ToNts(repaired).IsValid).IsTrue();
    }

    [Test]
    public async Task MakeValid_reorients_cw_exterior_to_ccw_for_geojson_rfc()
    {
        // country-levels ships rings in OGC/Shapefile winding (CW exterior). Mapbox-GL/MapLibre-GL
        // (geojson.io) interprets a CW outer ring as a hole-in-the-world and triangulates an inside-out
        // polygon — the fan artifacts. MakeValid must enforce GeoJSON RFC 7946's right-hand rule.
        var cwSquare = new Polygon([[new(0, 0), new(0, 1), new(1, 1), new(1, 0), new(0, 0)]]);
        var oriented = (Polygon) Geo.MakeValid(cwSquare);
        var nts = (NetTopologySuite.Geometries.Polygon) Geo.ToNts(oriented);
        await Assert.That(nts.Shell.IsCCW).IsTrue();
    }

    [Test]
    public async Task MakeValid_orients_holes_clockwise()
    {
        // RFC 7946: holes are CW (negative signed area), opposite to the CCW exterior.
        var cwOuter = new[] { new Position(0d, 0), new(0, 10), new(10, 10), new(10, 0), new(0, 0) };
        var cwHole = new[] { new Position(2d, 2), new(2, 4), new(4, 4), new(4, 2), new(2, 2) };
        var polygon = new Polygon([cwOuter, cwHole]);
        var oriented = (Polygon) Geo.MakeValid(polygon);
        var nts = (NetTopologySuite.Geometries.Polygon) Geo.ToNts(oriented);
        await Assert.That(nts.Shell.IsCCW).IsTrue();
        await Assert.That(nts.Holes[0].IsCCW).IsFalse();
    }

    [Test]
    public async Task Clip_returns_full_geometry_when_inside_bounds()
    {
        var clipped = Geo.Clip(UnitSquare(), new(-1, -1, 2, 2));
        await Assert.That(clipped).IsNotNull();
        // Clipping by a strictly-larger bbox returns the same geometry (modulo coordinate ordering).
        await Assert.That(clipped!.Type).IsEqualTo(GeometryType.Polygon);
    }

    [Test]
    public async Task Clip_returns_null_when_disjoint()
    {
        var clipped = Geo.Clip(UnitSquare(), new(10, 10, 20, 20));
        await Assert.That(clipped).IsNull();
    }

    [Test]
    public async Task Clip_intersects_a_partial_overlap()
    {
        var clipped = Geo.Clip(UnitSquare(), new(0.5, 0.5, 2, 2));
        await Assert.That(clipped).IsNotNull();
        await Assert.That(Geo.ToNts(clipped!).Area).IsLessThan(1.0);
    }

    [Test]
    public async Task Intersects_envelopes()
    {
        // Touching edges count as intersecting.
        await Assert.That(Geo.Intersects(new(0, 0, 1, 1), new(1, 0, 2, 1))).IsTrue();
        await Assert.That(Geo.Intersects(new(0, 0, 1, 1), new(2, 0, 3, 1))).IsFalse();
        await Assert.That(Geo.Intersects(Envelope.Empty, new(0, 0, 1, 1))).IsFalse();
    }

    [Test]
    public async Task Contains_envelopes()
    {
        await Assert.That(Geo.Contains(new(0, 0, 10, 10), new(2, 2, 5, 5))).IsTrue();
        // Touching the outer edge still counts as contained.
        await Assert.That(Geo.Contains(new(0, 0, 10, 10), new(0, 0, 10, 10))).IsTrue();
        // Overlap but not contained.
        await Assert.That(Geo.Contains(new(0, 0, 10, 10), new(5, 5, 15, 15))).IsFalse();
        await Assert.That(Geo.Contains(Envelope.Empty, new(0, 0, 1, 1))).IsFalse();
    }

    [Test]
    public async Task Point_round_trips_through_nts()
    {
        var back = Geo.ToGeo(Geo.ToNts(new Point(new(1.5, 2.5))));
        await Assert.That(back.Type).IsEqualTo(GeometryType.Point);
        await Assert.That(((Point) back).Coordinate.X).IsEqualTo(1.5);
    }

    [Test]
    public async Task LineString_round_trips_through_nts()
    {
        var back = Geo.ToGeo(Geo.ToNts(new LineString([new(0, 0), new(1, 1), new(2, 0)])));
        await Assert.That(back.Type).IsEqualTo(GeometryType.LineString);
        await Assert.That(((LineString) back).Positions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task MultiPoint_round_trips_through_nts()
    {
        var back = Geo.ToGeo(Geo.ToNts(new MultiPoint([new(0, 0), new(1, 1)])));
        await Assert.That(back.Type).IsEqualTo(GeometryType.MultiPoint);
        await Assert.That(((MultiPoint) back).Positions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MultiLineString_round_trips_through_nts()
    {
        var back = Geo.ToGeo(Geo.ToNts(new MultiLineString([
            new([new(0, 0), new(1, 1)]),
            new([new(2, 2), new(3, 3)]),
        ])));
        await Assert.That(back.Type).IsEqualTo(GeometryType.MultiLineString);
        await Assert.That(((MultiLineString) back).LineStrings.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GeometryCollection_round_trips_through_nts()
    {
        var collection = new GeometryCollection([
            new Point(new(0, 0)),
            new LineString([new(0, 0), new(1, 1)]),
        ]);
        var back = Geo.ToGeo(Geo.ToNts(collection));
        await Assert.That(back.Type).IsEqualTo(GeometryType.GeometryCollection);
        await Assert.That(((GeometryCollection) back).Geometries.Count).IsEqualTo(2);
    }
}
