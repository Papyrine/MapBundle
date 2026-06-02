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

    // Regression for the MSB4096 batching bug: this project carries Snapshot.verified.txt, which the
    // SDK default-globs into @(None) with no Region metadata (mimicking a Verify snapshot). The fact
    // that this project builds at all proves _MapBundleCopyRaw no longer batches its <None> Link over
    // the global None list. Belt-and-braces: that unrelated None item must not leak into maps/Monaco.
    [Test]
    public async Task Unrelated_None_items_are_not_staged()
    {
        await Assert.That(Directory.GetFiles(RegionDirectory, "*.verified.txt")).IsEmpty();
    }
}
