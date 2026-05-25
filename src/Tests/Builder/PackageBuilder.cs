namespace MapBundle.Builder;

/// <summary>Filters the source layers for a region, writes the FlatGeobuf files, and packs the nupkg.</summary>
public static class PackageBuilder
{
    const string Tags = "map maps geo geospatial naturalearth flatgeobuf borders cities rivers offline";

    // Fixed build settings. Paths are anchored to the repo root, not the process working directory:
    // the test host sets the cwd to the test binary's folder, so relative paths would land in bin/.
    const string Version = "0.1.0";
    const string ProjectUrl = "https://github.com/SimonCropp/MapBundle";
    static readonly string Root = FindRoot();
    static string OutputDirectory => Path.Combine(Root, "nugets");
    static string CacheDirectory => Path.Combine(Root, ".cache");
    static string IconPath => Path.Combine(Root, "src", "icon.png");
    static string ReadmePath => Path.Combine(Root, "readme.md");

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

    /// <summary>Downloads Natural Earth (cached via Replicant) at 1:10m, then builds and packs every region package.</summary>
    public static async Task RunAsync()
    {
        var httpDirectory = Path.Combine(CacheDirectory, "http");
        Directory.CreateDirectory(httpDirectory);
        await using var httpCache = new HttpCache(httpDirectory);

        var sources = await Sources.Download(httpCache, Path.Combine(CacheDirectory, "source"), Scale.M10);
        Console.WriteLine($"Loaded {sources.Countries.Count} countries and {sources.Layers.Count} other layers.");

        Directory.CreateDirectory(OutputDirectory);
        var staging = Path.Combine(OutputDirectory, ".staging");

        foreach (var region in Regions.All)
        {
            var directory = BuildRegion(region, sources, staging);
            var package = Pack(region, directory);
            Console.WriteLine($"  {Path.GetFileName(package)}");
        }
    }

    // Natural Earth ships dozens of attribute columns per feature; keep only a small, useful subset per
    // layer (matching is case-insensitive, so the case here need not match the source exactly).
    static string[] KeysFor(MapLayer layer) =>
        layer switch
        {
            MapLayer.Borders => ["NAME", "NAME_LONG", "ISO_A2", "ADM0_A3", "CONTINENT", "SUBREGION", "POP_EST", "ECONOMY"],
            MapLayer.Cities => ["NAME", "NAME_EN", "ADM0_A3", "ADM0NAME", "ADM1NAME", "FEATURECLA", "SCALERANK", "POP_MAX", "POP_MIN"],
            MapLayer.StatesProvinces => ["name", "name_en", "adm0_a3", "iso_3166_2", "type_en"],
            MapLayer.UrbanAreas => ["scalerank", "area_sqkm"],
            MapLayer.Rivers => ["name", "name_en", "featurecla", "scalerank"],
            MapLayer.Lakes => ["name", "name_en", "featurecla"],
            MapLayer.MinorIslands => ["featurecla", "name"],
            MapLayer.Coastline => ["featurecla", "scalerank", "min_zoom"],
            MapLayer.Land => ["featurecla", "min_zoom"],
            MapLayer.Ocean => ["featurecla", "min_zoom"],
            _ => [],
        };

    /// <summary>Filters and writes the region's FlatGeobuf layers (plus meta.json) into a staging folder.</summary>
    public static string BuildRegion(Region region, Sources sources, string stagingRoot)
    {
        var directory = Path.Combine(stagingRoot, region.Key);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);

        var countries = sources.Countries.Where(region.Selects).ToList();
        var isoSet = countries
            .Select(_ => _.Iso)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var borderFeatures = countries.Select(_ => _.Feature).ToList();
        var bounds = new FeatureCollection(borderFeatures).GetBounds();

        var counts = new Dictionary<MapLayer, int>();
        Write(directory, MapLayer.Borders, Trim(borderFeatures, KeysFor(MapLayer.Borders)), counts);

        foreach (var (layer, features) in sources.Layers)
        {
            var filtered = Filter(layer, features, region, isoSet, bounds);
            Write(directory, layer, Trim(filtered, KeysFor(layer)), counts);
        }

        File.WriteAllText(Path.Combine(directory, "meta.json"), Meta(region, sources.Scale, counts));
        return directory;
    }

    // Cities and states/provinces carry a country code, so they're filtered by country membership;
    // everything else by bounding-box intersection with the region (whole features, no clipping).
    static IEnumerable<Feature> Filter(MapLayer layer, FeatureCollection features, Region region, HashSet<string> isoSet, Envelope bounds)
    {
        if (layer is MapLayer.Cities or MapLayer.StatesProvinces)
        {
            return region.All ? features : features.Where(_ => isoSet.Contains(Props.Text(_, "ADM0_A3")));
        }

        return Within(features, bounds, region.All);
    }

    /// <summary>Packs a staged region folder into a <c>.nupkg</c> and returns its path.</summary>
    public static string Pack(Region region, string stagingDirectory)
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
            dependencyId: "MapBundle",
            dependencyVersion: Version,
            files);
        return packagePath;
    }

    static void Write(string directory, MapLayer layer, FeatureCollection features, Dictionary<MapLayer, int> counts)
    {
        if (features.Count == 0)
        {
            return;
        }

        GeoConverter.Write(features, Path.Combine(directory, Map.FileName(layer)), GeoFormat.FlatGeobuf);
        counts[layer] = features.Count;
    }

    static FeatureCollection Within(FeatureCollection source, Envelope bounds, bool all)
    {
        if (all)
        {
            return source;
        }

        // A region with no geometry gets no waterways (rather than all of them).
        if (bounds.IsEmpty)
        {
            return new();
        }

        return new(source.Where(_ => _.Geometry is { } geometry && Intersects(bounds, geometry.GetBounds())));
    }

    static FeatureCollection Trim(IEnumerable<Feature> features, string[] keep)
    {
        var wanted = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        var result = new FeatureCollection();
        foreach (var feature in features)
        {
            var trimmed = new Feature(feature.Geometry) { Id = feature.Id };
            foreach (var pair in feature.Properties)
            {
                if (wanted.Contains(pair.Key))
                {
                    trimmed.Properties[pair.Key] = pair.Value;
                }
            }

            result.Add(trimmed);
        }

        return result;
    }

    static bool Intersects(Envelope a, Envelope b) =>
        !a.IsEmpty &&
        !b.IsEmpty &&
        b.MinX <= a.MaxX &&
        b.MaxX >= a.MinX &&
        b.MinY <= a.MaxY &&
        b.MaxY >= a.MinY;

    static string Description(Region region) =>
        $"{region.Name} map data — borders, cities and waterways — as FlatGeobuf, derived from Natural Earth (public domain). Read it with the MapBundle package.";

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

    static string Meta(Region region, Scale scale, Dictionary<MapLayer, int> counts)
    {
        var layers = string.Join(",\n", counts
            .OrderBy(_ => _.Key)
            .Select(_ => $"    \"{_.Key}\": {_.Value}"));
        return $$"""
        {
          "region": "{{region.Key}}",
          "name": "{{region.Name}}",
          "scale": "{{NaturalEarth.ScaleToken(scale)}}",
          "attribution": "{{NaturalEarth.Attribution}}",
          "layers": {
        {{layers}}
          }
        }
        """;
    }
}
