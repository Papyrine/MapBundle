public class RegionsTests
{
    [Test]
    public Task Region_table() =>
        Verify(Describe());

    static string Describe()
    {
        var builder = new StringBuilder();
        foreach (var region in Regions.All)
        {
            builder.AppendLine(region.PackageId);
            if (region.All)
            {
                builder.AppendLine("  all countries");
            }

            if (region.Continents.Length > 0)
            {
                builder.AppendLine($"  continents: {string.Join(", ", region.Continents)}");
            }

            if (region.Subregions.Length > 0)
            {
                builder.AppendLine($"  subregions: {string.Join(", ", region.Subregions)}");
            }

            if (region.IncludeIso.Length > 0)
            {
                builder.AppendLine($"  include: {string.Join(", ", region.IncludeIso)}");
            }

            if (region.ExcludeIso.Length > 0)
            {
                builder.AppendLine($"  exclude: {string.Join(", ", region.ExcludeIso)}");
            }
        }

        return builder.ToString();
    }

    static Country Country(string iso, string continent, string subregion) =>
        new(iso, iso, continent, subregion, new());

    static Region Region(string key) =>
        Regions.All.Single(_ => _.Key == key);

    [Test]
    public async Task Mexico_is_grouped_north_not_central()
    {
        var mexico = Country("MEX", "North America", "Central America");
        await Assert.That(Region("AmericasNorthern").Selects(mexico)).IsTrue();
        await Assert.That(Region("AmericasCentral").Selects(mexico)).IsFalse();
    }

    [Test]
    public async Task Iran_is_western_and_middle_east_not_southern()
    {
        var iran = Country("IRN", "Asia", "Southern Asia");
        await Assert.That(Region("AsiaWestern").Selects(iran)).IsTrue();
        await Assert.That(Region("MiddleEast").Selects(iran)).IsTrue();
        await Assert.That(Region("AsiaSouthern").Selects(iran)).IsFalse();
    }

    [Test]
    public async Task Continent_and_subregion_both_match()
    {
        var kenya = Country("KEN", "Africa", "Eastern Africa");
        await Assert.That(Region("Africa").Selects(kenya)).IsTrue();
        await Assert.That(Region("AfricaEastern").Selects(kenya)).IsTrue();
        await Assert.That(Region("AfricaNorthern").Selects(kenya)).IsFalse();
    }

    [Test]
    public async Task World_selects_anything() =>
        await Assert.That(Regions.World.Selects(Country("ZZZ", "Nowhere", "Nowhere"))).IsTrue();
}
