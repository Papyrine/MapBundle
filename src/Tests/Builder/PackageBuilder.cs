/// <summary>
/// Builds the region data packages: country-levels boundaries (borders, states), Natural Earth global
/// layers (cities, rivers, lakes) and osmdata polygons (land, ocean, and the coastline derived from land),
/// each clipped to the region. Geofabrik's index still drives the region tree, but its bulk per-region
/// extracts are no longer downloaded. Each region's layers are written as FlatGeobuf and packed into a nupkg.
/// </summary>
public class PackageBuilder
{
    [Test]
    [Explicit]
    public Task Generate() =>
        RunAsync();

    [Test]
    [Explicit]
    public Task Slice() =>
        BuildAsync(SliceIds());

    // The regions the Slice test builds: MAPBUNDLE_SLICE (comma-separated ids), or Monaco by default.
    static string[] SliceIds()
    {
        var value = Environment.GetEnvironmentVariable("MAPBUNDLE_SLICE");
        return string.IsNullOrWhiteSpace(value)
            ? ["monaco"]
            : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    const string version = "0.1.0";
    const string projectUrl = "https://github.com/SimonCropp/MapBundle";
    const string tags = "map maps geo geospatial openstreetmap osm flatgeobuf borders cities rivers offline";

    public const string Attribution =
        "© OpenStreetMap contributors, ODbL (boundaries via country-levels, land/ocean via " +
        "osmdata.openstreetmap.de); cities, rivers and lakes made with Natural Earth (public domain).";

    static readonly string root = FindRoot();
    static string OutputDirectory => Path.Combine(root, "nugets");
    static string CacheDirectory => Path.Combine(root, ".cache");
    static string IconPath => Path.Combine(root, "src", "icon.png");
    static string ReadmePath => Path.Combine(root, "readme.md");

    /// <summary>Builds and packs every region package (continents, countries and the merged World).</summary>
    public static Task RunAsync() =>
        BuildAsync(_ => true, writeIndex: true);

    /// <summary>Builds and packs only the regions whose id is listed — used to validate a slice end-to-end.</summary>
    public static Task BuildAsync(params string[] ids)
    {
        var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return BuildAsync(_ => wanted.Contains(_.Id) || wanted.Contains(_.Key), writeIndex: false);
    }

    static async Task BuildAsync(Func<Region, bool> selected, bool writeIndex)
    {
        Directory.CreateDirectory(OutputDirectory);
        var httpDirectory = Path.Combine(CacheDirectory, "http");
        Directory.CreateDirectory(httpDirectory);
        // The OSM servers throttle, and large country extracts over a shared connection can take a long
        // time, so give each request a generous timeout (well past HttpClient's 100s default).
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        await using var httpCache = new HttpCache(httpDirectory, httpClient, maxRetries: 3);

        var regions = await Regions.Load(httpCache, Path.Combine(CacheDirectory, "geofabrik"));
        var countryLevels = await CountryLevels.Download(httpCache, Path.Combine(CacheDirectory, "country-levels"));
        var osmData = await OsmData.Download(httpCache, Path.Combine(CacheDirectory, "osmdata"));
        var naturalEarth = await NaturalEarth.Download(httpCache, Path.Combine(CacheDirectory, "natural-earth"));

        var context = new Context(regions, countryLevels, osmData, naturalEarth);
        var staging = Path.Combine(OutputDirectory, ".staging");

        var chosen = regions.Where(selected).ToList();
        var bundles = new ConcurrentBag<Bundle>();
        var failures = new ConcurrentBag<string>();

        // Network blips on a single region must not sink the whole build: retry a few times, then skip it.
        async Task Build(Region region)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var (directory, counts) = BuildRegion(region, context, staging);
                    // Don't ship empty stubs: a region with no layer data (typically a Geofabrik continent
                    // child that carries no ISO codes — Alps, Russian federal districts, US states…) has
                    // nothing useful to package. Skip it; it won't appear in nugets/ or the bundles index.
                    if (counts.Count == 0)
                    {
                        Console.WriteLine($"  skipped {region.Id} (no layer data)");
                        return;
                    }

                    var package = Pack(region, directory);
                    bundles.Add(new(region, package, directory, counts));
                    Console.WriteLine($"  {Path.GetFileName(package)}");
                    return;
                }
                catch (Exception exception) when (attempt < 4)
                {
                    Console.WriteLine($"  retry {region.Id} (attempt {attempt}): {exception.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (Exception exception)
                {
                    failures.Add(region.Id);
                    Console.WriteLine($"  FAILED {region.Id}: {exception.Message}");
                    return;
                }
            }
        }

        // Every region is independent now: borders come from country-levels, and cities/rivers/lakes/land/
        // ocean are clipped from the already-downloaded global Natural Earth and osmdata layers. So build
        // them all in one parallel pass, at a modest degree to keep memory and CPU in check.
        var options = new ParallelOptions { MaxDegreeOfParallelism = 3 };
        await Parallel.ForEachAsync(chosen, options, async (region, _) => await Build(region));

        if (!failures.IsEmpty)
        {
            Console.WriteLine($"Skipped {failures.Count} region(s) after retries: {string.Join(", ", failures.OrderBy(_ => _))}");
        }

        if (writeIndex)
        {
            WriteBundlesIndex([.. bundles]);
        }
    }

    /// <summary>Filters and writes a region's FlatGeobuf layers (plus meta.json) into a staging folder.</summary>
    static (string Directory, Dictionary<MapLayer, int> Counts) BuildRegion(Region region, Context context, string stagingRoot)
    {
        var directory = Path.Combine(stagingRoot, region.Key);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);

        var members = context.Members(region);
        var iso = members.SelectMany(_ => _.Iso).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var borders = iso
            .Select(context.CountryLevels.Border)
            .OfType<Feature>()
            .ToList();
        var states = iso
            .SelectMany(context.CountryLevels.Subdivisions)
            .ToList();
        var bounds = new FeatureCollection(borders).GetBounds();

        var counts = new Dictionary<MapLayer, int>();
        Write(directory, MapLayer.Borders, borders, counts);
        Write(directory, MapLayer.StatesProvinces, states, counts);

        Write(directory, MapLayer.Cities, context.NaturalEarth.Cities(iso), counts);
        Write(directory, MapLayer.Rivers, context.NaturalEarth.Rivers(bounds), counts);
        Write(directory, MapLayer.Lakes, context.NaturalEarth.Lakes(bounds), counts);

        Write(directory, MapLayer.Land, context.OsmData.Land(bounds), counts);
        Write(directory, MapLayer.Ocean, context.OsmData.Ocean(bounds), counts);
        Write(directory, MapLayer.Coastline, context.OsmData.Coastline(bounds), counts);

        File.WriteAllText(Path.Combine(directory, "meta.json"), Meta(region, counts));
        return (directory, counts);
    }

    static void Write(string directory, MapLayer layer, IReadOnlyList<Feature> features, Dictionary<MapLayer, int> counts)
    {
        if (features.Count == 0)
        {
            return;
        }

        GeoConverter.Write(new(features), Path.Combine(directory, Map.FileName(layer)), GeoFormat.FlatGeobuf);
        counts[layer] = features.Count;
    }

    /// <summary>Packs a staged region folder into a <c>.nupkg</c> and returns its path.</summary>
    static string Pack(Region region, string stagingDirectory)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(stagingDirectory))
        {
            files[$"data/{Path.GetFileName(file)}"] = File.ReadAllBytes(file);
        }

        files[$"buildTransitive/{region.PackageId}.targets"] = Encoding.UTF8.GetBytes(Targets(region));

        if (File.Exists(IconPath))
        {
            files["icon.png"] = File.ReadAllBytes(IconPath);
        }

        if (File.Exists(ReadmePath))
        {
            files["readme.md"] = File.ReadAllBytes(ReadmePath);
        }

        var packagePath = Path.Combine(OutputDirectory, $"{region.PackageId}.{version}.nupkg");
        NuPkgWriter.Write(
            packagePath,
            region.PackageId,
            version,
            Description(region),
            projectUrl,
            tags,
            license: "ODbL-1.0",
            dependencyId: "MapBundle",
            dependencyVersion: version,
            files);
        return packagePath;
    }

    static string Description(Region region) =>
        $"{region.Name} map data — borders, cities, rivers, lakes and coastline — as FlatGeobuf, derived from " +
        "OpenStreetMap (© OpenStreetMap contributors, ODbL). Read it with the MapBundle package.";

    static string Targets(Region region) =>
        $"""
        <Project>
          <ItemGroup>
            <None Include="$(MSBuildThisFileDirectory)..\data\*">
              <Link>maps\{region.Key}\%(Filename)%(Extension)</Link>
              <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
              <Visible>false</Visible>
            </None>
          </ItemGroup>
        </Project>
        """;

    static string Meta(Region region, Dictionary<MapLayer, int> counts)
    {
        var layers = string.Join(",\n", counts
            .OrderBy(_ => _.Key)
            .Select(_ => $"    \"{_.Key}\": {_.Value}"));
        return $$"""
        {
          "region": "{{region.Key}}",
          "name": "{{region.Name}}",
          "source": "OpenStreetMap",
          "attribution": "{{Attribution}}",
          "layers": {
        {{layers}}
          }
        }
        """;
    }

    /// <summary>
    /// Writes <c>src/bundles.include.md</c> as one table per region kind — World, Continents, Countries —
    /// each ordered alphabetically by name. The kind is implicit in the section heading, so the row has
    /// no Type column. Empty sections are skipped.
    /// </summary>
    static void WriteBundlesIndex(IReadOnlyList<Bundle> bundles)
    {
        (string Heading, Func<Bundle, bool> Match)[] sections =
        [
            ("World", _ => _.Region.IsWorld),
            ("Continents", _ => _.Region.IsContinent),
            ("Countries", _ => _.Region.IsCountry),
        ];

        var builder = new StringBuilder();
        builder.AppendLine("Layer icons: 🗺️ Borders · 🏛️ StatesProvinces · 🏙️ Cities · 〰️ Rivers · 💧 Lakes · 🏖️ Coastline · 🟩 Land · 🌊 Ocean");
        builder.AppendLine();
        foreach (var (heading, match) in sections)
        {
            var rows = bundles.Where(match).OrderBy(_ => _.Region.Name, StringComparer.Ordinal).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            builder.Append(
                $"""
                ## {heading}

                | Bundle | NuGet | Data | Layers | Features |
                | --- | --: | --: | --: | --: |

                """);
            foreach (var bundle in rows)
            {
                builder.AppendLine(Row(bundle));
            }

            builder.AppendLine();
        }

        var path = Path.Combine(root, "src", "bundles.include.md");
        File.WriteAllText(path, builder.ToString());
        Console.WriteLine($"Wrote {path} ({bundles.Count} bundles).");
    }

    static string Row(Bundle bundle)
    {
        var id = bundle.Region.PackageId;
        var nuget = Size(new FileInfo(bundle.Package).Length);
        var data = Size(Directory.GetFiles(bundle.Staging, "*.fgb").Sum(_ => new FileInfo(_).Length));
        var layers = string.Join(" ", bundle.Counts.Keys.OrderBy(_ => _).Select(LayerIcon));
        var features = bundle.Counts.Values.Sum();
        return $"| [{id}](https://www.nuget.org/packages/{id}) | {nuget} | {data} | {layers} | {features:N0} |";
    }

    static string LayerIcon(MapLayer layer) =>
        layer switch
        {
            MapLayer.Borders => "🗺️",
            MapLayer.StatesProvinces => "🏛️",
            MapLayer.Cities => "🏙️",
            MapLayer.Rivers => "〰️",
            MapLayer.Lakes => "💧",
            MapLayer.Coastline => "🏖️",
            MapLayer.Land => "🟩",
            MapLayer.Ocean => "🌊",
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };

    static string Size(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024:F1} MB" :
        bytes >= 1024 ? $"{bytes / 1024d:F0} KB" :
        $"{bytes} B";

    static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new MapBundleException("Could not locate the repository root (no global.json above the test binary).");
    }

    /// <summary>A built package: its region, the written <c>.nupkg</c> path, its staging folder and layer feature counts.</summary>
    sealed record Bundle(Region Region, string Package, string Staging, Dictionary<MapLayer, int> Counts);

    /// <summary>Shared state for a build: the loaded global sources, plus the region tree for member lookup.</summary>
    sealed class Context(IReadOnlyList<Region> regions, CountryLevels countryLevels, OsmData osmData, NaturalEarth naturalEarth)
    {
        public CountryLevels CountryLevels => countryLevels;
        public OsmData OsmData => osmData;
        public NaturalEarth NaturalEarth => naturalEarth;

        /// <summary>The regions whose borders make up a region: itself, its member countries, or all of them.</summary>
        public IReadOnlyList<Region> Members(Region region) =>
            Regions.Members(region, regions);
    }
}
