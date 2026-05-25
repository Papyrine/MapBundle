public class BuilderTests
{
    static Feature Attributed(string codeKey, string adm0, string name)
    {
        var feature = new Feature(new Point(new Position(-100, 40)));
        feature.Properties[codeKey] = adm0;
        feature.Properties["NAME"] = name;
        return feature;
    }

    [Test]
    public async Task BuildRegion_selects_by_subregion_and_filters_country_layers()
    {
        using var temp = new TempDirectory();

        var usa = new Country("USA", "United States", "North America", "Northern America",
            new Feature(new Point(new Position(-100, 40))));
        var sources = new MapBundle.Builder.Sources(
            Scale.M10,
            [usa],
            new Dictionary<MapLayer, FeatureCollection>
            {
                [MapLayer.Cities] = new([Attributed("ADM0_A3", "USA", "Denver"), Attributed("ADM0_A3", "MEX", "Mexico City")]),
                // States use a lower-cased code field; matching is case-insensitive.
                [MapLayer.StatesProvinces] = new([Attributed("adm0_a3", "USA", "Colorado"), Attributed("adm0_a3", "MEX", "Jalisco")]),
            });

        PackageBuilder.BuildRegion(Regions.All.Single(_ => _.Key == "AmericasNorthern"), sources, temp);

        var map = Maps.Open(temp).Load("AmericasNorthern");
        await Assert.That(map.Borders.Count).IsEqualTo(1);
        // Only the USA-coded features; "MEX" has no country in the selected set.
        await Assert.That(map.Cities.Count).IsEqualTo(1);
        await Assert.That(map.StatesProvinces.Count).IsEqualTo(1);
    }
}
