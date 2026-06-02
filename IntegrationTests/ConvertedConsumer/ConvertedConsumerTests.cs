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

    // Regression for the MSB4096 batching bug: this project carries Snapshot.verified.txt, which the
    // SDK default-globs into @(None) with no Region metadata (mimicking a Verify snapshot). The fact
    // that this project builds at all proves _MapBundleConvert no longer batches its <None> Link over
    // the global None list. Belt-and-braces: that unrelated None item must not leak into maps/Monaco.
    [Test]
    public async Task Unrelated_None_items_are_not_staged()
    {
        await Assert.That(Directory.GetFiles(RegionDirectory, "*.verified.txt")).IsEmpty();
    }

    // The opt-in conversion path is settings-aware: the task writes a signature of the consumer's
    // MapBundle* properties beside the build intermediates, so editing one (e.g. the SimplifyTolerance)
    // regenerates maps/Monaco even when the source .fgb is unchanged. Confirm the real build produced
    // that stamp and that it captured this project's settings. (The regeneration behaviour itself is
    // unit-tested by MapConverterTests.Settings_changes_invalidate_timestamp_current_outputs.)
    [Test]
    public async Task Conversion_is_settings_stamped()
    {
        var stamp = FindIntermediateFile(".mapbundle-settings");
        await Assert.That(stamp).IsNotNull();
        var settings = await File.ReadAllTextAsync(stamp!);
        await Assert.That(settings).Contains("GeoJson");
        await Assert.That(settings).Contains("0.0001");
    }

    // Walk up from the test assembly to the project, then search its obj/ for the build intermediate
    // (Config/Tfm vary by how it was built, so we don't hard-code obj/<Config>/<Tfm>/mapbundle).
    static string? FindIntermediateFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ConvertedConsumer.csproj")))
        {
            directory = directory.Parent;
        }

        var obj = directory is null ? null : Path.Combine(directory.FullName, "obj");
        if (obj is null || !Directory.Exists(obj))
        {
            return null;
        }

        return Directory.EnumerateFiles(obj, "*", SearchOption.AllDirectories)
            .FirstOrDefault(_ => Path.GetFileName(_) == name);
    }
}
