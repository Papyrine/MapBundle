/// <summary>
/// Builds a small, real <c>MapBundle.Monaco</c> data package into <c>nugets/</c> — without downloading
/// any source data — so the IntegrationTests solution can consume it and exercise the build-time
/// targets / conversion task. It goes through the production packaging code (<see cref="NuPkgWriter"/>
/// and <see cref="PackageBuilder.Targets"/>), so the package layout is identical to a real region;
/// only the geometry is synthetic. Run explicitly (and is invoked from CI before the integration build):
///   Tests --treenode-filter "/*/*/IntegrationFixture/BuildMonacoPackage"
/// </summary>
public class IntegrationFixture
{
    // A trivial polygon and point around Monaco — enough geometry for conversion and a real render.
    const string bordersGeoJson =
        """{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[7.40,43.72],[7.44,43.72],[7.44,43.75],[7.40,43.75],[7.40,43.72]]]},"properties":{"name":"Monaco"}}]}""";

    const string citiesGeoJson =
        """{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[7.42,43.73]},"properties":{"name":"Monaco"}}]}""";

    [Test]
    [Explicit]
    public async Task BuildMonacoPackage()
    {
        var root = Path.GetFullPath(Path.Combine(ProjectFiles.SolutionDirectory, "../"));
        var nugets = Path.Combine(root, "nugets");
        Directory.CreateDirectory(nugets);

        var staging = Path.Combine(nugets, ".staging", "Monaco");
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);

        WriteFgb(staging, "borders.fgb", bordersGeoJson);
        WriteFgb(staging, "cities.fgb", citiesGeoJson);
        await File.WriteAllTextAsync(Path.Combine(staging, "meta.json"), "{\n  \"region\": \"Monaco\"\n}\n");

        var region = new Region("monaco", "europe", "Monaco", ["MC"], ShpUrl: null);

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(staging))
        {
            files[$"data/{Path.GetFileName(file)}"] = await File.ReadAllBytesAsync(file);
        }

        files[$"buildTransitive/{region.PackageId}.targets"] = Encoding.UTF8.GetBytes(PackageBuilder.Targets(region));

        var version = CoreVersion.Value;
        var package = Path.Combine(nugets, $"{region.PackageId}.{version}.nupkg");
        NuPkgWriter.Write(
            package,
            region.PackageId,
            version,
            description: "Monaco map data (integration-test fixture).",
            projectUrl: "https://github.com/Papyrine/MapBundle",
            tags: "map maps geo",
            license: "ODbL-1.0",
            dependencyId: "MapBundle",
            dependencyVersion: version,
            files);

        await Assert.That(File.Exists(package)).IsTrue();
        Console.WriteLine($"Wrote {package}");
    }

    static void WriteFgb(string directory, string layerFile, string geoJson)
    {
        var features = GeoConverter.Read(new MemoryStream(Encoding.UTF8.GetBytes(geoJson)), GeoFormat.GeoJson);
        GeoConverter.Write(features, Path.Combine(directory, layerFile), GeoFormat.FlatGeobuf);
    }
}
