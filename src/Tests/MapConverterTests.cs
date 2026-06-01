using MapBundle.Build;

// Unit tests for the build-time conversion engine (MapConverter) that backs the ConvertMapData MSBuild
// task. The task glue itself is exercised end-to-end by the IntegrationTests solution.
public class MapConverterTests
{
    const string pointGeoJson =
        """{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[1,2]},"properties":{"name":"x"}}]}""";

    static string WriteFgb(string directory, string layerFile)
    {
        Directory.CreateDirectory(directory);
        var features = GeoConverter.Read(new MemoryStream(Encoding.UTF8.GetBytes(pointGeoJson)), GeoFormat.GeoJson);
        var path = Path.Combine(directory, layerFile);
        GeoConverter.Write(features, path, GeoFormat.FlatGeobuf);
        return path;
    }

    [Test]
    public async Task Converts_fgb_to_the_requested_format()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        var output = Path.Combine(temp, "out");

        var produced = MapConverter.Convert(new()
        {
            Sources = [new(source, "Monaco")],
            OutputDirectory = output,
            Format = GeoFormat.GeoJson,
        });

        var geojson = Path.Combine(output, "Monaco", "borders.geojson");
        await Assert.That(produced.Select(_ => _.Path)).Contains(geojson);
        await Assert.That(File.Exists(geojson)).IsTrue();
        // Round-trips back through GeoConvert as real GeoJSON.
        await Assert.That(GeoConverter.Read(geojson, GeoFormat.GeoJson).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Copies_fgb_verbatim_when_format_is_flatgeobuf()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        var meta = Path.Combine(temp, "src", "meta.json");
        File.WriteAllText(meta, "{}");
        var output = Path.Combine(temp, "out");

        MapConverter.Convert(new()
        {
            Sources = [new(source, "Monaco"), new(meta, "Monaco")],
            OutputDirectory = output,
            Format = GeoFormat.FlatGeobuf,
        });

        // The .fgb stays .fgb, and the non-fgb meta.json is copied through untouched.
        await Assert.That(File.Exists(Path.Combine(output, "Monaco", "borders.fgb"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output, "Monaco", "meta.json"))).IsTrue();
    }

    [Test]
    public async Task CopyData_false_emits_no_vector_data()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        var output = Path.Combine(temp, "out");

        var produced = MapConverter.Convert(new()
        {
            Sources = [new(source, "Monaco")],
            OutputDirectory = output,
            Format = GeoFormat.GeoJson,
            CopyData = false,
        });

        await Assert.That(produced.Count).IsEqualTo(0);
        await Assert.That(File.Exists(Path.Combine(output, "Monaco", "borders.geojson"))).IsFalse();
    }

    [Test]
    public async Task Renders_a_png_per_region_stacking_layers()
    {
        using var temp = new TempDirectory();
        var sourceDirectory = Path.Combine(temp, "src");
        var ocean = WriteFgb(sourceDirectory, "ocean.fgb");
        var borders = WriteFgb(sourceDirectory, "borders.fgb");
        var cities = WriteFgb(sourceDirectory, "cities.fgb");
        var output = Path.Combine(temp, "out");

        var produced = MapConverter.Convert(new()
        {
            Sources = [new(ocean, "Monaco"), new(borders, "Monaco"), new(cities, "Monaco")],
            OutputDirectory = output,
            CopyData = false,
            RenderImages = true,
            Image = new() { Width = 128, Labels = true },
        });

        var png = Path.Combine(output, "Monaco", "Monaco.png");
        await Assert.That(produced.Select(_ => _.Path)).Contains(png);
        var header = File.ReadAllBytes(png)[..4];
        await Assert.That(header).IsEquivalentTo(new byte[] { 0x89, (byte) 'P', (byte) 'N', (byte) 'G' });
    }

    [Test]
    public async Task Converts_and_renders_together()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        var output = Path.Combine(temp, "out");

        MapConverter.Convert(new()
        {
            Sources = [new(source, "Monaco")],
            OutputDirectory = output,
            Format = GeoFormat.Kml,
            RenderImages = true,
            Image = new() { Width = 128 },
        });

        await Assert.That(File.Exists(Path.Combine(output, "Monaco", "borders.kml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output, "Monaco", "Monaco.png"))).IsTrue();
    }

    [Test]
    public async Task Up_to_date_outputs_are_not_rewritten()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        var output = Path.Combine(temp, "out");
        ConvertRequest Request() => new()
        {
            Sources = [new(source, "Monaco")],
            OutputDirectory = output,
            Format = GeoFormat.GeoJson,
        };

        MapConverter.Convert(Request());
        var geojson = Path.Combine(output, "Monaco", "borders.geojson");
        var firstWrite = File.GetLastWriteTimeUtc(geojson);

        // A second run with an unchanged source must leave the existing output in place.
        MapConverter.Convert(Request());
        await Assert.That(File.GetLastWriteTimeUtc(geojson)).IsEqualTo(firstWrite);
    }

    [Test]
    public async Task Png_is_rejected_as_a_data_format()
    {
        using var temp = new TempDirectory();
        var source = WriteFgb(Path.Combine(temp, "src"), "borders.fgb");
        await Assert.That(() => MapConverter.Convert(new()
            {
                Sources = [new(source, "Monaco")],
                OutputDirectory = Path.Combine(temp, "out"),
                Format = GeoFormat.Png,
            }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ParseFormat_is_case_insensitive_and_validates()
    {
        await Assert.That(MapConverter.ParseFormat("geojson")).IsEqualTo(GeoFormat.GeoJson);
        await Assert.That(MapConverter.ParseFormat("FlatGeobuf")).IsEqualTo(GeoFormat.FlatGeobuf);
        await Assert.That(() => MapConverter.ParseFormat("nope")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Extension_covers_every_vector_format()
    {
        await Assert.That(MapConverter.Extension(GeoFormat.GeoJson)).IsEqualTo(".geojson");
        await Assert.That(MapConverter.Extension(GeoFormat.TopoJson)).IsEqualTo(".topojson");
        await Assert.That(MapConverter.Extension(GeoFormat.Shapefile)).IsEqualTo(".shp");
        await Assert.That(MapConverter.Extension(GeoFormat.FlatGeobuf)).IsEqualTo(".fgb");
        await Assert.That(MapConverter.Extension(GeoFormat.Kml)).IsEqualTo(".kml");
        await Assert.That(MapConverter.Extension(GeoFormat.Kmz)).IsEqualTo(".kmz");
        await Assert.That(MapConverter.Extension(GeoFormat.Gpx)).IsEqualTo(".gpx");
        await Assert.That(MapConverter.Extension(GeoFormat.Wkt)).IsEqualTo(".wkt");
        await Assert.That(MapConverter.Extension(GeoFormat.Wkb)).IsEqualTo(".wkb");
        await Assert.That(MapConverter.Extension(GeoFormat.Csv)).IsEqualTo(".csv");
        await Assert.That(MapConverter.Extension(GeoFormat.GeoParquet)).IsEqualTo(".parquet");
        await Assert.That(() => MapConverter.Extension(GeoFormat.Png)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ParseColor_handles_rgb_rrggbb_and_rrggbbaa()
    {
        await Assert.That(MapConverter.ParseColor("#f00")).IsEqualTo(new(255, 0, 0));
        await Assert.That(MapConverter.ParseColor("4682b4")).IsEqualTo(new(70, 130, 180));
        await Assert.That(MapConverter.ParseColor("#4682b480")).IsEqualTo(new(70, 130, 180, 128));
        await Assert.That(() => MapConverter.ParseColor("#12")).Throws<ArgumentException>();
        await Assert.That(() => MapConverter.ParseColor("#gggggg")).Throws<ArgumentException>();
    }

    [Test]
    public async Task ParseProjection_and_compression_validate()
    {
        await Assert.That(MapConverter.ParseProjection("webmercator")).IsEqualTo(MapProjection.WebMercator);
        await Assert.That(() => MapConverter.ParseProjection("nope")).Throws<ArgumentException>();
        await Assert.That(MapConverter.ParseCompression("fastest")).IsEqualTo(CompressionLevel.Fastest);
        await Assert.That(() => MapConverter.ParseCompression("nope")).Throws<ArgumentException>();
    }
}
