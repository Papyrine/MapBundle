// Direct unit tests for the CountryLevels source class — borders by ISO 3166-1 alpha-2, subdivisions
// grouped by the leading alpha-2 from the ISO 3166-2 code. The ISO lookup is case-insensitive so
// tests pin that down.
public class CountryLevelsTests
{
    static Feature Polygon(string name, IDictionary<string, object?> props) =>
        new(
            new Polygon(
            [
                [
                    new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0)
                ]
            ]),
            props)
        {
            Id = name
        };

    [Test]
    public async Task Border_finds_a_country_by_alpha2_code()
    {
        var borders = new Dictionary<string, Feature>
        {
            ["MC"] = Polygon(
                "Monaco",
                new Dictionary<string, object?>
                {
                    ["iso1"] = "MC"
                }),
            ["FR"] = Polygon(
                "France",
                new Dictionary<string, object?>
                {
                    ["iso1"] = "FR"
                }),
        };
        var levels = new CountryLevels(borders, []);

        await Assert.That(levels.Border("MC")!.Id).IsEqualTo("Monaco");
        await Assert.That(levels.Border("FR")!.Id).IsEqualTo("France");
    }

    [Test]
    public async Task Border_lookup_uppercases_the_input()
    {
        // Members iterate ISO codes from the Geofabrik index as-is; some sources lowercase them, so
        // Border has to fold the input to upper-invariant to match the stored keys.
        var borders = new Dictionary<string, Feature>
        {
            ["MC"] = Polygon("Monaco", new Dictionary<string, object?>()),
        };
        var levels = new CountryLevels(borders, []);

        await Assert.That(levels.Border("mc")).IsNotNull();
        await Assert.That(levels.Border("Mc")).IsNotNull();
    }

    [Test]
    public async Task Border_returns_null_for_an_unknown_code()
    {
        var levels = new CountryLevels([], []);
        await Assert.That(levels.Border("ZZ")).IsNull();
    }

    [Test]
    public async Task Subdivisions_returned_for_a_country_code()
    {
        var subdivisions = new Dictionary<string, List<Feature>>
        {
            ["US"] =
            [
                Polygon("California", new Dictionary<string, object?>()),
                Polygon("Texas", new Dictionary<string, object?>()),
            ],
        };
        var levels = new CountryLevels([], subdivisions);

        var result = levels.Subdivisions("US");
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Subdivisions_returns_empty_list_for_unknown_country()
    {
        var levels = new CountryLevels([], []);
        var result = levels.Subdivisions("ZZ");
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Subdivisions_lookup_uppercases_the_input()
    {
        var subdivisions = new Dictionary<string, List<Feature>>
        {
            ["US"] = [Polygon("California", new Dictionary<string, object?>())],
        };
        var levels = new CountryLevels([], subdivisions);

        await Assert.That(levels.Subdivisions("us").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Read_returns_features_in_deterministic_iso_order()
    {
        // Regression for the "maps/*.png change on every render" bug. Read parses the geojson files with
        // PLINQ (AsParallel), which yields results in COMPLETION order, not source order. Borders go into
        // a keyed dictionary so that was harmless, but subdivisions are kept as ordered per-country lists,
        // so an unstable order changed the StatesProvinces feature order — and therefore its .fgb bytes
        // and its semi-transparent-fill preview PNG — on every build. Read now sorts by ISO code; pin that
        // contract: the output is sorted, and identical across runs, regardless of parse/enumeration order.
        using var temp = new TempDirectory();
        // Enough files, in a deliberately unsorted sequence, that an unordered parallel parse would not
        // come back sorted by chance.
        string[] codes =
        [
            "US-TX", "CA-QC", "US-AK", "US-CA", "CA-ON", "US-NY", "BR-SP", "AU-NSW",
            "US-WA", "CA-BC", "FR-IDF", "DE-BY", "ZA-GP", "IN-MH", "JP-13", "MX-CMX",
        ];
        foreach (var code in codes)
        {
            await File.WriteAllTextAsync(
                Path.Combine(temp, $"{code}.geojson"),
                """{"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]},"properties":{"iso2":"CODE"}}"""
                    .Replace("CODE", code));
        }

        var expected = string.Join(",", codes.OrderBy(_ => _, StringComparer.Ordinal));
        var first = string.Join(",", CountryLevels.Read(temp).Select(_ => _.Key));
        var second = string.Join(",", CountryLevels.Read(temp).Select(_ => _.Key));

        await Assert.That(first).IsEqualTo(expected);
        await Assert.That(second).IsEqualTo(expected);
    }
}
