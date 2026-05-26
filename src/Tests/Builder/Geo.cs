using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;

/// <summary>
/// Geometry helpers for the builder. GeoConvert ships its own geometry model with no simplify,
/// reproject or topology operations, so here we bridge losslessly to NetTopologySuite for those.
/// NetTopologySuite is a build-only dependency (this project is not shipped).
/// </summary>
static class Geo
{
    static readonly Nts.GeometryFactory factory = new();

    // Spherical Web Mercator (EPSG:3857) radius. The simplified osmdata polygons ship only in 3857.
    const double mercatorRadius = 6378137d;
    const double rad2Deg = 180d / Math.PI;

    /// <summary>Converts a GeoConvert geometry to its NetTopologySuite equivalent (2D).</summary>
    public static Nts.Geometry ToNts(Geometry geometry) =>
        geometry switch
        {
            Point point => factory.CreatePoint(Coord(point.Coordinate)),
            MultiPoint multi => factory.CreateMultiPointFromCoords(Coords(multi.Positions)),
            LineString line => factory.CreateLineString(Coords(line.Positions)),
            Polygon polygon => ToNtsPolygon(polygon),
            MultiLineString multi => factory.CreateMultiLineString([.. multi.LineStrings.Select(_ => factory.CreateLineString(Coords(_.Positions)))]),
            MultiPolygon multi => factory.CreateMultiPolygon([.. multi.Polygons.Select(ToNtsPolygon)]),
            GeometryCollection collection => factory.CreateGeometryCollection([.. collection.Geometries.Select(ToNts)]),
            _ => throw new($"Unsupported geometry: {geometry.Type}"),
        };

    /// <summary>Converts a NetTopologySuite geometry back to GeoConvert (2D). Multi types must be matched before the GeometryCollection base.</summary>
    public static Geometry ToGeo(Nts.Geometry geometry) =>
        geometry switch
        {
            Nts.Point point => new Point(Pos(point.Coordinate)),
            Nts.MultiPoint multi => new MultiPoint([.. multi.Coordinates.Select(Pos)]),
            Nts.LineString line => new LineString(Positions(line.Coordinates)),
            Nts.Polygon polygon => ToGeoPolygon(polygon),
            Nts.MultiLineString multi => new MultiLineString([.. Parts(multi).Cast<Nts.LineString>().Select(_ => new LineString(Positions(_.Coordinates)))]),
            Nts.MultiPolygon multi => new MultiPolygon([.. Parts(multi).Cast<Nts.Polygon>().Select(ToGeoPolygon)]),
            Nts.GeometryCollection collection => new GeometryCollection([.. Parts(collection).Select(ToGeo)]),
            _ => throw new($"Unsupported geometry: {geometry.GeometryType}"),
        };

    /// <summary>
    /// Repairs a geometry for web rendering by fixing self-intersecting rings via the buffer-zero idiom.
    /// country-levels' Douglas-Peucker simplification introduces self-intersections in heavily-indented
    /// coastlines (Greenland, the Canadian arctic, …); the bytes parse fine through strict FlatBuffers
    /// verifiers but triangulating GPU renderers (Mapbox-GL / MapLibre-GL via earcut) draw fan-shaped
    /// artifacts across each invalid part. Returns the input unchanged when it is already valid or when
    /// NTS rejects the geometry. Ring winding (CCW exterior / CW holes per GeoJSON RFC 7946) is no
    /// longer enforced here — that lives in GeoConvert's FlatGeobuf writer now.
    /// </summary>
    public static Geometry MakeValid(Geometry geometry)
    {
        Nts.Geometry nts;
        try
        {
            nts = ToNts(geometry);
        }
        catch (ArgumentException)
        {
            return geometry;
        }

        if (nts.IsValid)
        {
            return geometry;
        }

        try
        {
            var repaired = nts.Buffer(0);
            return repaired.IsEmpty ? geometry : ToGeo(repaired);
        }
        catch (Exception exception) when (exception is Nts.TopologyException or ArgumentException)
        {
            return geometry;
        }
    }

    /// <summary>
    /// Simplifies a geometry with the topology-preserving Douglas-Peucker variant, returning null when
    /// the result collapses to empty. Invalid OSM geometries that NTS rejects are returned unchanged.
    /// </summary>
    public static Geometry? Simplify(Geometry geometry, double toleranceDegrees)
    {
        if (toleranceDegrees <= 0)
        {
            return geometry;
        }

        try
        {
            var simplified = TopologyPreservingSimplifier.Simplify(ToNts(geometry), toleranceDegrees);
            return simplified.IsEmpty ? null : ToGeo(simplified);
        }
        catch (Exception exception) when (exception is Nts.TopologyException or ArgumentException)
        {
            return geometry;
        }
    }

    /// <summary>Reprojects a geometry from EPSG:3857 (spherical Web Mercator) to EPSG:4326 (lon/lat).</summary>
    public static Geometry MercatorToWgs84(Geometry geometry) =>
        MapPositions(geometry, _ => MercatorToWgs84(_.X, _.Y));

    /// <summary>Reprojects an EPSG:3857 bounding box to EPSG:4326 (used to clip Mercator sources by a lon/lat region).</summary>
    public static Envelope MercatorToWgs84(Envelope mercator)
    {
        if (mercator.IsEmpty)
        {
            return Envelope.Empty;
        }

        var min = MercatorToWgs84(mercator.MinX, mercator.MinY);
        var max = MercatorToWgs84(mercator.MaxX, mercator.MaxY);
        return new(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>Whether two bounding boxes overlap (touching edges count as overlapping).</summary>
    public static bool Intersects(Envelope a, Envelope b) =>
        !a.IsEmpty &&
        !b.IsEmpty &&
        b.MinX <= a.MaxX &&
        b.MaxX >= a.MinX &&
        b.MinY <= a.MaxY &&
        b.MaxY >= a.MinY;

    /// <summary>Whether <paramref name="outer"/> fully contains <paramref name="inner"/>.</summary>
    public static bool Contains(Envelope outer, Envelope inner) =>
        !outer.IsEmpty &&
        !inner.IsEmpty &&
        inner.MinX >= outer.MinX &&
        inner.MaxX <= outer.MaxX &&
        inner.MinY >= outer.MinY &&
        inner.MaxY <= outer.MaxY;

    /// <summary>
    /// Clips a geometry to a bounding box. Needed because the simplified osmdata land polygons are
    /// unsplit (a single feature can be a whole continent), so a region must trim them to its extent.
    /// Returns null when nothing is left; returns the original if NTS rejects the (invalid) geometry.
    /// </summary>
    public static Geometry? Clip(Geometry geometry, Envelope bounds)
    {
        try
        {
            var rectangle = factory.ToGeometry(new(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY));
            var clipped = ToNts(geometry).Intersection(rectangle);
            return clipped.IsEmpty ? null : ToGeo(clipped);
        }
        catch (Exception exception) when (exception is Nts.TopologyException or ArgumentException)
        {
            return geometry;
        }
    }

    static Position MercatorToWgs84(double x, double y)
    {
        var lon = x / mercatorRadius * rad2Deg;
        var lat = (2 * Math.Atan(Math.Exp(y / mercatorRadius)) - Math.PI / 2) * rad2Deg;
        return new(lon, lat);
    }

    /// <summary>The ring outlines of a (multi)polygon as line strings — used to derive coastlines from land.</summary>
    public static IEnumerable<Geometry> Outlines(Geometry geometry)
    {
        switch (geometry)
        {
            case Polygon polygon:
                foreach (var ring in polygon.Rings)
                {
                    yield return new LineString(ring);
                }

                break;
            case MultiPolygon multi:
                foreach (var line in multi.Polygons.SelectMany(Outlines))
                {
                    yield return line;
                }

                break;
            case GeometryCollection collection:
                foreach (var line in collection.Geometries.SelectMany(Outlines))
                {
                    yield return line;
                }

                break;
        }
    }

    static Geometry MapPositions(Geometry geometry, Func<Position, Position> map) =>
        geometry switch
        {
            Point point => new Point(map(point.Coordinate)),
            MultiPoint multi => new MultiPoint([.. multi.Positions.Select(map)]),
            LineString line => new LineString([.. line.Positions.Select(map)]),
            Polygon polygon => new Polygon([.. polygon.Rings.Select(ring => (IReadOnlyList<Position>) [.. ring.Select(map)])]),
            MultiLineString multi => new MultiLineString([.. multi.LineStrings.Select(_ => new LineString([.. _.Positions.Select(map)]))]),
            MultiPolygon multi => new MultiPolygon([.. multi.Polygons.Select(_ => (Polygon) MapPositions(_, map))]),
            GeometryCollection collection => new GeometryCollection([.. collection.Geometries.Select(_ => MapPositions(_, map))]),
            _ => geometry,
        };

    static Nts.Polygon ToNtsPolygon(Polygon polygon)
    {
        var shell = factory.CreateLinearRing(Coords(polygon.Rings[0]));
        var holes = polygon.Rings
            .Skip(1)
            .Select(_ => factory.CreateLinearRing(Coords(_)))
            .ToArray();
        return factory.CreatePolygon(shell, holes);
    }

    static Polygon ToGeoPolygon(Nts.Polygon polygon)
    {
        var rings = new List<IReadOnlyList<Position>> { Positions(polygon.ExteriorRing.Coordinates) };
        rings.AddRange(polygon.InteriorRings.Select(_ => Positions(_.Coordinates)));
        return new(rings);
    }

    static IEnumerable<Nts.Geometry> Parts(Nts.GeometryCollection collection)
    {
        for (var index = 0; index < collection.NumGeometries; index++)
        {
            yield return collection.GetGeometryN(index);
        }
    }

    static Nts.Coordinate Coord(Position position) => new(position.X, position.Y);

    static Nts.Coordinate[] Coords(IReadOnlyList<Position> positions) => [.. positions.Select(Coord)];

    static Position Pos(Nts.Coordinate coordinate) => new(coordinate.X, coordinate.Y);

    static IReadOnlyList<Position> Positions(Nts.Coordinate[] coordinates) => [.. coordinates.Select(Pos)];
}
