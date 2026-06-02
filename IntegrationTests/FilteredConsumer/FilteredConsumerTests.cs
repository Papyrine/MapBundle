// Verifies the layer-subset build behaviour: MapBundleLayers/MapBundleExcludeLayers (set in the
// .csproj) filter the data package's MapBundleData items before either downstream path runs, so
// maps/Monaco only carries the layers that survive the filter. Building this project is the
// integration test — these assertions inspect what the build produced.
//
// The Monaco fixture ships borders.fgb and cities.fgb. The .csproj whitelists both (mixing the enum
// name "Borders" with the on-disk name "cities" to exercise both forms) then blacklists Cities — so
// the surviving set is exactly { borders.fgb, meta.json }. That gives us positive proof in both
// directions: borders surviving proves the whitelist didn't accidentally drop everything; cities
// being dropped proves the blacklist runs after the whitelist and recognises a casing-shifted name
// the whitelist already let through.
public class FilteredConsumerTests
{
    static string RegionDirectory => Path.Combine(AppContext.BaseDirectory, "maps", "Monaco");

    [Test]
    public async Task Whitelisted_layer_survives()
    {
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "borders.fgb"))).IsTrue();
    }

    [Test]
    public async Task Blacklisted_layer_is_dropped_even_when_whitelisted()
    {
        // Cities is in both lists. Blacklist must win: if cities.fgb is present here the filter is
        // applying the lists in the wrong order, or the blacklist isn't case-folding.
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "cities.fgb"))).IsFalse();
    }

    [Test]
    public async Task Non_layer_files_are_always_kept()
    {
        // meta.json isn't a layer file, so neither the whitelist nor the blacklist touches it. The
        // MapBundle core needs it to enumerate available layers.
        await Assert.That(File.Exists(Path.Combine(RegionDirectory, "meta.json"))).IsTrue();
    }

    [Test]
    public async Task Core_loads_the_surviving_layer()
    {
        var map = Maps.Open().Load("Monaco");
        await Assert.That(map.Borders.Count).IsGreaterThan(0);
    }
}
