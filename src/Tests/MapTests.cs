public class MapTests
{
    static void WriteFgb(string directory, MapLayer layer)
    {
        Directory.CreateDirectory(directory);
        const string geojson =
            """{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[1,2]},"properties":{"name":"x"}}]}""";
        var features = GeoConverter.Read(new MemoryStream(Encoding.UTF8.GetBytes(geojson)), GeoFormat.GeoJson);
        GeoConverter.Write(features, Path.Combine(directory, Map.FileName(layer)), GeoFormat.FlatGeobuf);
    }

    [Test]
    public async Task Loads_region_and_all_layers()
    {
        using var temp = new TempDirectory();
        var region = Path.Combine(temp, "World");
        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            WriteFgb(region, layer);
        }

        var catalog = Maps.Open(temp);
        await Assert.That(catalog.Directory).IsNotNull();
        await Assert.That(catalog.Regions.Contains("World")).IsTrue();
        await Assert.That(catalog.Contains("World")).IsTrue();
        await Assert.That(catalog.Contains("Nope")).IsFalse();

        var map = catalog.Load("World");
        await Assert.That(map.Region).IsEqualTo("World");
        await Assert.That(map.Has(MapLayer.Borders)).IsTrue();
        await Assert.That(map.Borders.Count).IsEqualTo(1);
        await Assert.That(map.Cities.Count).IsEqualTo(1);
        await Assert.That(map.Rivers.Count).IsEqualTo(1);
        await Assert.That(map.Lakes.Count).IsEqualTo(1);
        await Assert.That(map.StatesProvinces.Count).IsEqualTo(1);
        await Assert.That(map.Coastline.Count).IsEqualTo(1);
        await Assert.That(map.Land.Count).IsEqualTo(1);
        await Assert.That(map.Ocean.Count).IsEqualTo(1);
        await Assert.That(map.Load(MapLayer.Borders).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Empty_directory_has_no_regions_and_load_throws()
    {
        using var temp = new TempDirectory();
        var catalog = Maps.Open(temp);
        await Assert.That(catalog.Regions.Count).IsEqualTo(0);
        await Assert.That(() => catalog.Load("World")).Throws<MapBundleException>();
    }

    [Test]
    public async Task Missing_layer_throws()
    {
        using var temp = new TempDirectory();
        WriteFgb(Path.Combine(temp, "World"), MapLayer.Borders);
        var map = Maps.Open(temp).Load("World");
        await Assert.That(map.Has(MapLayer.Cities)).IsFalse();
        await Assert.That(() => map.Cities).Throws<MapBundleException>();
    }

    [Test]
    public async Task FileName_covers_every_layer_and_rejects_unknown()
    {
        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            await Assert.That(Map.FileName(layer).EndsWith(".fgb")).IsTrue();
        }

        await Assert.That(() => Map.FileName((MapLayer) 999)).Throws<MapBundleException>();
    }

    [Test]
    public async Task Regions_are_listed_in_order()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp, "World"));
        Directory.CreateDirectory(Path.Combine(temp, "Africa"));
        var regions = Maps.Open(temp).Regions;
        await Assert.That(regions.Count).IsEqualTo(2);
        await Assert.That(regions[0]).IsEqualTo("Africa");
        await Assert.That(regions[1]).IsEqualTo("World");
    }

    [Test]
    public async Task Nonexistent_directory_has_no_regions()
    {
        var catalog = Maps.Open(Path.Combine(Path.GetTempPath(), $"mapbundle-missing-{Guid.NewGuid():N}"));
        await Assert.That(catalog.Regions.Count).IsEqualTo(0);
        await Assert.That(catalog.Contains("World")).IsFalse();
    }

    [Test]
    public async Task Default_catalog_targets_a_maps_folder()
    {
        var catalog = Maps.Open();
        await Assert.That(catalog.Directory.EndsWith("maps")).IsTrue();
    }
}
