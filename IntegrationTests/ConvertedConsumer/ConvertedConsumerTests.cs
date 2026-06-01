// Verifies the opt-in build behaviour: the FlatGeobuf layers are converted to GeoJSON and a styled
// PNG is rendered into maps/Monaco at build time (no .fgb is copied). Building this project is the
// integration test — these assertions inspect what the build produced.
public class ConvertedConsumerTests
{
    static string RegionDirectory => Path.Combine(AppContext.BaseDirectory, "maps", "Monaco");

    [Test]
    public async Task Layers_are_converted_to_geojson()
    {
        var borders = Path.Combine(RegionDirectory, "borders.geojson");
        var cities = Path.Combine(RegionDirectory, "cities.geojson");
        await Assert.That(File.Exists(borders)).IsTrue();
        await Assert.That(File.Exists(cities)).IsTrue();
        // The converted file is real GeoJSON GeoConvert can read back.
        await Assert.That(GeoConverter.Read(borders, GeoFormat.GeoJson).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Meta_is_carried_through()
    {
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "meta.json"))).IsTrue();
    }

    [Test]
    public async Task FlatGeobuf_is_not_copied()
    {
        await Assert.That(Directory.GetFiles(RegionDirectory, "*.fgb")).IsEmpty();
    }

    [Test]
    public async Task A_preview_image_is_rendered()
    {
        var png = Path.Combine(RegionDirectory, "Monaco.png");
        await Assert.That(File.Exists(png)).IsTrue();
        var header = File.ReadAllBytes(png)[..4];
        await Assert.That(header).IsEquivalentTo(new byte[] { 0x89, (byte) 'P', (byte) 'N', (byte) 'G' });
    }
}
