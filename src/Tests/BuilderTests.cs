public class BuilderTests
{
    static Feature Place(string adm0, string name)
    {
        var feature = new Feature(new Point(new Position(-100, 40)));
        feature.Properties["ADM0_A3"] = adm0;
        feature.Properties["NAME"] = name;
        return feature;
    }

    [Test]
    public async Task BuildRegion_selects_by_subregion_and_filters_cities_by_country()
    {
        using var temp = new TempDirectory();

        var usa = new Country("USA", "United States", "North America", "Northern America",
            new Feature(new Point(new Position(-100, 40))));
        var sources = new MapBundle.Builder.Sources(
            Scale.M10,
            [usa],
            places: new FeatureCollection([Place("USA", "Denver"), Place("MEX", "Mexico City")]),
            rivers: null,
            lakes: null);

        PackageBuilder.BuildRegion(Regions.All.Single(_ => _.Key == "AmericasNorthern"), sources, temp);

        var map = Maps.Open(temp).Load("AmericasNorthern");
        await Assert.That(map.Borders.Count).IsEqualTo(1);
        // Only the USA city; "MEX" has no country in the selected set.
        await Assert.That(map.Cities.Count).IsEqualTo(1);
    }
}
