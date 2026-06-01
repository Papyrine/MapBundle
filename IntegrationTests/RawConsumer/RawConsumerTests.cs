// Verifies the default build behaviour: the data package's FlatGeobuf layers are copied into
// maps/Monaco beside this test app, and the MapBundle core reads them. Building this project is the
// integration test — these assertions inspect what the build produced.
public class RawConsumerTests
{
    static string RegionDirectory => Path.Combine(AppContext.BaseDirectory, "maps", "Monaco");

    [Test]
    public async Task FlatGeobuf_layers_are_copied()
    {
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "borders.fgb"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "cities.fgb"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "meta.json"))).IsTrue();
    }

    [Test]
    public async Task No_converted_or_rendered_output_is_produced()
    {
        await Assert.That(Directory.GetFiles(RegionDirectory, "*.geojson")).IsEmpty();
        await Assert.That(Directory.GetFiles(RegionDirectory, "*.png")).IsEmpty();
    }

    [Test]
    public async Task Core_loads_the_copied_data()
    {
        var map = Maps.Open().Load("Monaco");
        await Assert.That(map.Borders.Count).IsGreaterThan(0);
        await Assert.That(map.Cities.Count).IsGreaterThan(0);
    }
}
