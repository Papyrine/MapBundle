namespace MapBundle.Builder;

/// <summary>
/// Builds the region data packages from OpenStreetMap sources: Geofabrik per-region extracts (cities,
/// rivers, lakes), country-levels boundaries (borders, states) and osmdata polygons (land, ocean, and the
/// coastline derived from land). Each region's layers are written as FlatGeobuf and packed into a nupkg.
/// </summary>
public static class PackageBuilder
{
    const string Version = "0.1.0";
    const string ProjectUrl = "https://github.com/SimonCropp/MapBundle";
    const string Tags = "map maps geo geospatial openstreetmap osm flatgeobuf borders cities rivers offline";

    public const string Attribution =
        "© OpenStreetMap contributors, ODbL. Boundaries via country-levels; land/ocean via osmdata.openstreetmap.de.";

    // Simplification tolerance (degrees) for the detailed Geofabrik line/polygon layers. ~0.0005° ≈ 50 m.
    const double WaterwayTolerance = 0.0005;
    const double LakeTolerance = 0.0005;

    static readonly string Root = FindRoot();
    static string OutputDirectory => Path.Combine(Root, "nugets");
    static string CacheDirectory => Path.Combine(Root, ".cache");
    static string IconPath => Path.Combine(Root, "src", "icon.png");
    static string ReadmePath => Path.Combine(Root, "readme.md");

    /// <summary>Builds and packs every region package (continents, countries and the merged World).</summary>
    public static Task RunAsync() =>
        BuildAsync(_ => true);

    /// <summary>Builds and packs only the regions whose id is listed — used to validate a slice end-to-end.</summary>
    public static Task BuildAsync(params string[] ids)
    {
        var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return BuildAsync(_ => wanted.Contains(_.Id) || wanted.Contains(_.Key));
    }

    static async Task BuildAsync(Func<Region, bool> selected)
    {
        Directory.CreateDirectory(OutputDirectory);
        var httpDirectory = Path.Combine(CacheDirectory, "http");
        Directory.CreateDirectory(httpDirectory);
        // osmdata.openstreetmap.de throttles hard, so a download can outlast HttpClient's default 100s
        // timeout. Give it room and retry transient drops.
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        await using var httpCache = new HttpCache(httpDirectory, httpClient, maxRetries: 3);

        var regions = await Regions.Load(httpCache, Path.Combine(CacheDirectory, "geofabrik"));
        var countryLevels = await CountryLevels.Download(httpCache, Path.Combine(CacheDirectory, "country-levels"));
        var osmData = await OsmData.Download(httpCache, Path.Combine(CacheDirectory, "osmdata"));

        var context = new Context(httpCache, regions, countryLevels, osmData);
        var staging = Path.Combine(OutputDirectory, ".staging");

        foreach (var region in regions.Where(selected))
        {
            var directory = await BuildRegion(region, context, staging);
            var package = Pack(region, directory);
            Console.WriteLine($"  {Path.GetFileName(package)}");
        }
    }

    /// <summary>Filters and writes a region's FlatGeobuf layers (plus meta.json) into a staging folder.</summary>
    static async Task<string> BuildRegion(Region region, Context context, string stagingRoot)
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

        var geofabrik = new List<Dictionary<MapLayer, List<Feature>>>();
        foreach (var member in members)
        {
            geofabrik.Add(await context.Geofabrik(member));
        }

        Write(directory, MapLayer.Cities, Merge(geofabrik, MapLayer.Cities), counts);
        Write(directory, MapLayer.Rivers, Merge(geofabrik, MapLayer.Rivers), counts);
        Write(directory, MapLayer.Lakes, Merge(geofabrik, MapLayer.Lakes), counts);

        Write(directory, MapLayer.Land, context.OsmData.Land(bounds), counts);
        Write(directory, MapLayer.Ocean, context.OsmData.Ocean(bounds), counts);
        Write(directory, MapLayer.Coastline, context.OsmData.Coastline(bounds), counts);

        File.WriteAllText(Path.Combine(directory, "meta.json"), Meta(region, counts));
        return directory;
    }

    /// <summary>Reads, filters, simplifies and trims a single Geofabrik extract into the cities/rivers/lakes layers.</summary>
    static Dictionary<MapLayer, List<Feature>> ReadGeofabrik(HttpCache httpCache, Region extract, string directory)
    {
        var layers = new Dictionary<MapLayer, List<Feature>>
        {
            [MapLayer.Cities] = [],
            [MapLayer.Rivers] = [],
            [MapLayer.Lakes] = [],
        };

        if (extract.ShpUrl is null)
        {
            return layers;
        }

        var root = Archives.Zip(httpCache, extract.ShpUrl, directory).GetAwaiter().GetResult();

        // Major populated places (cities and towns); points, no simplification needed.
        layers[MapLayer.Cities] =
        [
            .. Layer(root, Geofabrik.PlacesLayer)
                .Where(_ => Fclass(_) is "city" or "town")
                .Select(_ => Trim(_, "osm_id", "name", "fclass", "population"))
        ];

        // Major waterways (rivers only — not streams/canals/drains).
        layers[MapLayer.Rivers] =
        [
            .. Layer(root, Geofabrik.WaterwaysLayer)
                .Where(_ => Fclass(_) is "river")
                .Select(_ => Simplify(_, WaterwayTolerance))
                .OfType<Feature>()
                .Select(_ => Trim(_, "osm_id", "name", "fclass"))
        ];

        // Lakes and reservoirs.
        layers[MapLayer.Lakes] =
        [
            .. Layer(root, Geofabrik.WaterLayer)
                .Where(_ => Fclass(_) is "water" or "reservoir")
                .Select(_ => Simplify(_, LakeTolerance))
                .OfType<Feature>()
                .Select(_ => Trim(_, "osm_id", "name", "fclass"))
        ];

        return layers;
    }

    static IEnumerable<Feature> Layer(string root, string name)
    {
        var path = Archives.Find(root, $"{name}.shp");
        return path is null ? [] : GeoConverter.Read(path, GeoFormat.Shapefile).Features;
    }

    static List<Feature> Merge(IEnumerable<Dictionary<MapLayer, List<Feature>>> sources, MapLayer layer) =>
        [.. sources.SelectMany(_ => _[layer])];

    static Feature? Simplify(Feature feature, double tolerance)
    {
        if (feature.Geometry is not { } geometry)
        {
            return null;
        }

        var simplified = Geo.Simplify(geometry, tolerance);
        return simplified is null ? null : new(simplified) { Id = feature.Id, Properties = feature.Properties };
    }

    static string Fclass(Feature feature) =>
        Props.Text(feature, "fclass");

    static Feature Trim(Feature feature, params string[] keep)
    {
        var wanted = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        var trimmed = new Feature(feature.Geometry) { Id = feature.Id };
        foreach (var pair in feature.Properties)
        {
            if (wanted.Contains(pair.Key))
            {
                trimmed.Properties[pair.Key] = pair.Value;
            }
        }

        return trimmed;
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

        var packagePath = Path.Combine(OutputDirectory, $"{region.PackageId}.{Version}.nupkg");
        NuPkgWriter.Write(
            packagePath,
            region.PackageId,
            Version,
            Description(region),
            ProjectUrl,
            Tags,
            license: "ODbL-1.0",
            dependencyId: "MapBundle",
            dependencyVersion: Version,
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

    /// <summary>Shared state for a build: the loaded sources plus a per-extract Geofabrik layer cache.</summary>
    sealed class Context(HttpCache httpCache, IReadOnlyList<Region> regions, CountryLevels countryLevels, OsmData osmData)
    {
        readonly Dictionary<string, Dictionary<MapLayer, List<Feature>>> geofabrik = new(StringComparer.Ordinal);

        public CountryLevels CountryLevels => countryLevels;
        public OsmData OsmData => osmData;

        /// <summary>The Geofabrik extracts whose cities/rivers/lakes make up a region (itself, its countries, or all).</summary>
        public IReadOnlyList<Region> Members(Region region) =>
            Regions.Members(region, regions);

        public async Task<Dictionary<MapLayer, List<Feature>>> Geofabrik(Region extract)
        {
            if (geofabrik.TryGetValue(extract.Id, out var cached))
            {
                return cached;
            }

            var directory = Path.Combine(CacheDirectory, "geofabrik", "shp");
            var layers = await Task.Run(() => ReadGeofabrik(httpCache, extract, directory));
            geofabrik[extract.Id] = layers;
            return layers;
        }
    }
}
