// Verifies MapBundleOutputDirectory on the raw-copy path: when it is the ONLY MapBundle property set
// (no format / simplify / render / filter), MapBundle still writes the verbatim FlatGeobuf layers
// straight into the consumer-specified directory (here $(MSBuildProjectDirectory)\custom\<Region>),
// skipping the historical maps/<Region> auto-stage. This is the configuration that used to be a
// silent no-op — the redirect only took effect when a conversion lever happened to trigger the
// ConvertMapData task. Building this project IS the integration test.
public class RedirectedRawConsumerTests
{
    // Walk up from the test assembly to the .csproj's directory: the redirect uses
    // $(MSBuildProjectDirectory)\custom, which lives in the project's source tree, not in
    // AppContext.BaseDirectory (which is bin/<Config>/<TFM>/).
    static string ProjectDirectory => FindProjectDirectory();

    static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RedirectedRawConsumer.csproj")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException(
            "Could not locate RedirectedRawConsumer.csproj walking up from " + AppContext.BaseDirectory);
    }

    static string RedirectRoot => Path.Combine(ProjectDirectory, "custom");
    static string RedirectedRegionDirectory => Path.Combine(RedirectRoot, "Monaco");
    static string DefaultRegionDirectory => Path.Combine(AppContext.BaseDirectory, "maps", "Monaco");

    [Test]
    public async Task Verbatim_fgb_layers_land_at_the_redirected_location()
    {
        // Monaco's fixture ships borders.fgb and cities.fgb; both should be present at
        // <OutputDirectory>/<Region>/, NOT at the default maps/<Region>/.
        var borders = Path.Combine(RedirectedRegionDirectory, "borders.fgb");
        var cities = Path.Combine(RedirectedRegionDirectory, "cities.fgb");
        await Assert.That(File.Exists(borders)).IsTrue().Because($"expected at {borders}");
        await Assert.That(File.Exists(cities)).IsTrue().Because($"expected at {cities}");
        // FlatGeobuf magic: the file starts with the bytes "fgb" (0x66 0x67 0x62). Cheap signature
        // check that proves a real .fgb was copied verbatim, not a placeholder or zero-byte stub.
        await Assert.That(File.ReadAllBytes(borders)[..3]).IsEquivalentTo(new byte[] { 0x66, 0x67, 0x62 });
    }

    [Test]
    public async Task No_converted_or_rendered_output_is_produced()
    {
        // This consumer set no format/render lever, so the raw path ran: only verbatim .fgb (plus the
        // meta.json sidecar) should appear — never a converted .geojson or a rendered .png. Their
        // absence confirms the redirect was honoured by _MapBundleCopyRaw, not by ConvertMapData.
        await Assert.That(Directory.GetFiles(RedirectedRegionDirectory, "*.geojson")).IsEmpty();
        await Assert.That(Directory.GetFiles(RedirectedRegionDirectory, "*.png")).IsEmpty();
    }

    [Test]
    public async Task Meta_json_sidecar_rides_along()
    {
        // meta.json is part of @(MapBundleData), so the raw copy carries it through to the redirected
        // directory alongside the layers.
        await Assert.That(File.Exists(Path.Combine(RedirectedRegionDirectory, "meta.json"))).IsTrue();
    }

    [Test]
    public async Task Default_maps_directory_does_not_receive_a_copy()
    {
        // The redirect must skip the default <None Link>maps/<Region>/...</Link> auto-stage. Files at
        // maps/Monaco/ would mean the raw path ignored MapBundleOutputDirectory (the old no-op bug) and
        // the consumer is paying double disk on every build. The directory either doesn't exist at all
        // (the usual case) or, if some other target created it, must be empty of MapBundle files.
        if (Directory.Exists(DefaultRegionDirectory))
        {
            await Assert.That(Directory.GetFiles(DefaultRegionDirectory)).IsEmpty()
                .Because($"auto-stage should have been skipped, but {DefaultRegionDirectory} carries files");
        }
    }

    [Test]
    public async Task Core_loads_the_redirected_data_via_the_directory_overload()
    {
        // The documented way to read redirected data: point Maps.Open at the chosen directory (the
        // no-arg Maps.Open() only ever looks in AppContext.BaseDirectory/maps, which the redirect
        // deliberately leaves empty). Format is unchanged (FlatGeobuf), so the .fgb reads cleanly.
        var map = Maps.Open(RedirectRoot).Load("Monaco");
        await Assert.That(map.Borders.Count).IsGreaterThan(0);
        await Assert.That(map.Cities.Count).IsGreaterThan(0);
    }
}
