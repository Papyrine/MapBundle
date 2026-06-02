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

    /// <summary>
    /// Builds only the slow regions — World plus every continent — so per-layer timing changes can
    /// be iterated on without running the ~200 small country packs that come along in
    /// <see cref="Generate"/>. World dominates the wall-clock either way (its global Land/Ocean/
    /// Borders renders are the headline cost); the continents are kept so each continent's per-layer
    /// timing prints alongside World's for comparison.
    /// </summary>
    [Test]
    [Explicit]
    public Task Heavy() =>
        BuildAsync(_ => _.IsWorld || _.IsContinent, writeIndex: false);

    // The regions the Slice test builds: MAPBUNDLE_SLICE (comma-separated ids), or Monaco by default.
    static string[] SliceIds()
    {
        var value = Environment.GetEnvironmentVariable("MAPBUNDLE_SLICE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return ["monaco"];
        }

        return [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    const string version = "0.1.0";
    const string projectUrl = "https://github.com/Papyrine/MapBundle";
    const string tags = "map maps geo geospatial openstreetmap osm flatgeobuf borders cities rivers offline";

    const string attribution =
        "© OpenStreetMap contributors, ODbL (boundaries via country-levels, land/ocean via osmdata.openstreetmap.de); cities, rivers and lakes made with Natural Earth (public domain).";

    static readonly string root = Path.GetFullPath(Path.Combine(ProjectFiles.SolutionDirectory, "../"));
    // The data packages get their own directory, kept separate from nugets/ (which holds the core
    // MapBundle package and the integration fixture feed). A full build wipes its whole output
    // directory; pointing it here means that wipe can't take out the core package alongside it.
    static string OutputDirectory => Path.Combine(root, "data-nugets");
    static string MapsDirectory => Path.Combine(root, "maps");
    static string CacheDirectory => Path.Combine(root, ".cache");
    static string IconPath => Path.Combine(root, "src", "icon.png");
    // The succinct NuGet readme (not the full GitHub readme.md, which carries the entire ~200-row
    // per-region bundle table). Packed at the package root as readme.md (see NuPkgWriter).
    static string ReadmePath => Path.Combine(root, "nuget-readme.md");

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
        // Full builds (writeIndex==true) replace every region in one pass, so wipe data-nugets/ first
        // to avoid stale packages from removed/renamed regions or earlier skipped-on-failure regions
        // lingering. Slice builds touch only a subset, so leave the rest in place.
        if (writeIndex)
        {
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, recursive: true);
            }
            if (Directory.Exists(MapsDirectory))
            {
                Directory.Delete(MapsDirectory, recursive: true);
            }
        }

        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(MapsDirectory);
        var httpDirectory = Path.Combine(CacheDirectory, "http");
        Directory.CreateDirectory(httpDirectory);
        // The OSM servers throttle, and large country extracts over a shared connection can take a long
        // time, so give each request a generous timeout (well past HttpClient's 100s default).
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        await using var httpCache = new HttpCache(httpDirectory, httpClient, maxRetries: 3);

        var regions = await Regions.Load(httpCache, Path.Combine(CacheDirectory, "geofabrik"));
        var countryLevels = await CountryLevels.Download(httpCache, Path.Combine(CacheDirectory, "country-levels"));
        var osmData = await OsmData.Download(httpCache, Path.Combine(CacheDirectory, "osmdata"));
        var naturalEarth = await NaturalEarth.Download(httpCache, Path.Combine(CacheDirectory, "natural-earth"));

        var context = new Context(regions, countryLevels, osmData, naturalEarth);
        var staging = Path.Combine(OutputDirectory, ".staging");

        var chosen = regions.Where(selected).ToList();
        var bundles = new List<Bundle>();
        var failures = new List<string>();
        foreach (var region in chosen)
        {
            var watch = Stopwatch.StartNew();
            Console.WriteLine($"  start  {region.Id}");
            try
            {
                var (directory, counts) = BuildRegion(region, context, staging);
                // Don't ship empty stubs: a region with no layer data (typically a Geofabrik continent
                // child that carries no ISO codes — Alps, Russian federal districts, US states…) has
                // nothing useful to package. Skip it; it won't appear in data-nugets/ or the bundles index.
                if (counts.Count == 0)
                {
                    Console.WriteLine($"  skip   {region.Id} (no layer data, {watch.Elapsed.TotalSeconds:F1}s)");
                    continue;
                }

                var package = Pack(region, directory);
                bundles.Add(new(region, package, directory, counts));
                Console.WriteLine($"  done   {Path.GetFileName(package)} ({watch.Elapsed.TotalSeconds:F1}s)");
            }
            catch (Exception exception)
            {
                // Log and keep going — one bad region must not sink the whole run. Failures are
                // listed at the end so they're easy to spot in a long log.
                failures.Add(region.Id);
                Console.WriteLine($"  FAIL   {region.Id} ({watch.Elapsed.TotalSeconds:F1}s): {exception.GetType().Name}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
        {
            Console.WriteLine($"Skipped {failures.Count} region(s) after failures: {string.Join(", ", failures.OrderBy(_ => _))}");
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

        // Enumerate ISO codes in a stable order when building the per-country feature lists below.
        // HashSet<string> iteration order is randomised per process (.NET randomised string hashing),
        // so iterating `iso` directly would shuffle borders/states feature order between runs —
        // changing the .fgb byte output and, because the preview fills are semi-transparent, the
        // blended preview PNG. Cities is unaffected (NaturalEarth.Cities walks its source in file
        // order and only uses iso.Contains), so the set is still passed there as-is.
        var orderedIso = iso.OrderBy(_ => _, StringComparer.OrdinalIgnoreCase).ToList();

        // country-levels features are already Geo.MakeValid'd at load time (see CountryLevels.
        // ReadFeature), so per-region Repair here would be wasted work. country-levels'
        // Douglas-Peucker simplification leaves self-intersecting rings on heavily-indented
        // coastlines that NTS Buffer(0) repairs — required for triangulating GPU renderers like
        // Mapbox-GL / MapLibre-GL (via earcut, behind tools like geojson.io) which can't fill
        // invalid rings without fan-shaped artifacts across each country.
        var borders = orderedIso
            .Select(context.CountryLevels.Border)
            .OfType<Feature>()
            .ToList();
        var states = orderedIso
            .SelectMany(context.CountryLevels.Subdivisions)
            .ToList();
        var bounds = new FeatureCollection(borders).GetBounds();

        var counts = new Dictionary<MapLayer, int>();
        Write(directory, region, MapLayer.Borders, borders, bounds, counts);
        Write(directory, region, MapLayer.StatesProvinces, states, bounds, counts);

        Write(directory, region, MapLayer.Cities, context.NaturalEarth.Cities(iso), bounds, counts);
        Write(directory, region, MapLayer.Rivers, context.NaturalEarth.Rivers(bounds), bounds, counts);
        Write(directory, region, MapLayer.Lakes, context.NaturalEarth.Lakes(bounds).Select(Repair).ToList(), bounds, counts);

        // Skip Land for landlocked regions: with no ocean intersecting the bbox there's no
        // coastline either, so the Land layer collapses to a single rectangle covering the whole
        // bounds — redundant noise that just bloats the package. "No ocean in bounds" is the
        // robust test (works for Switzerland and Liechtenstein, and also for inland-sea-only
        // countries like Kazakhstan, since osmdata's water polygons are ocean-only).
        // Repair is no longer applied here — OsmData reprojects + validates eagerly at load time,
        // so the per-region path is bbox-cull + optional clip only.
        var ocean = context.OsmData.Ocean(bounds);
        var land = ocean.Count > 0
            ? context.OsmData.Land(bounds)
            : (IReadOnlyList<Feature>) [];

        Write(directory, region, MapLayer.Land, land, bounds, counts);
        Write(directory, region, MapLayer.Ocean, ocean, bounds, counts);
        Write(directory, region, MapLayer.Coastline, context.OsmData.Coastline(bounds), bounds, counts);

        File.WriteAllText(Path.Combine(directory, "meta.json"), Meta(region, counts));
        return (directory, counts);
    }

    // Returns a copy of the feature with its geometry repaired (self-intersections fixed via buffer-zero,
    // rings reoriented to GeoJSON RFC 7946's right-hand rule). Returns a new instance rather than mutating
    // the input, because <see cref="CountryLevels.Border"/> hands the same cached Feature back to every
    // region that needs it and BuildAsync runs regions in parallel — a mutating Repair would let one
    // thread's repaired geometry leak into another thread's already-materialised borders list, producing
    // non-deterministic output between runs even though Buffer(0) is benign per-call.
    static Feature Repair(Feature feature)
    {
        if (feature.Geometry is not { } geometry)
        {
            return feature;
        }

        return new(
            Geo.MakeValid(geometry),
            feature.Properties)
        {
            Id = feature.Id
        };
    }

    static void Write(string directory, Region region, MapLayer layer, IReadOnlyList<Feature> features, Envelope bounds, Dictionary<MapLayer, int> counts)
    {
        if (features.Count == 0)
        {
            return;
        }

        var collection = new FeatureCollection(features);
        GeoConverter.Write(collection, Path.Combine(directory, Map.FileName(layer)), GeoFormat.FlatGeobuf);
        counts[layer] = features.Count;

        // A preview needs a bounding box. `bounds` is computed from country-levels borders, so when
        // country-levels has no entry for a region's ISO code (Antarctica is the canonical case —
        // AQ has no iso1 border, but Natural Earth still tags its research bases with ISO_A2='AQ')
        // bounds is empty and MapRenderer.Validate rejects the call. Skip the preview; the FGB
        // file already shipped above, so the layer's data is still in the package.
        if (bounds.IsEmpty)
        {
            return;
        }

        // Per-layer preview PNG dropped next to the .nupkg under data-nugets/ — same region bounds across
        // every layer so the images overlay cleanly when viewed side-by-side. For StatesProvinces we
        // render only the topmost admin level per country: country-levels' iso2 set mixes levels for
        // some countries (Bangladesh ships admin_level 4 divisions and admin_level 6 districts that
        // sit inside them; India, Russia, France, the UK are similar), and stacking the overlapping
        // semi-transparent fills produced darker blotches in the preview. The .fgb keeps every level
        // — admin_level is preserved as a property so consumers can filter as they need.
        var previewFeatures = layer == MapLayer.StatesProvinces ? TopLevelSubdivisions(features) : features;
        if (previewFeatures.Count == 0)
        {
            return;
        }

        var preview = ReferenceEquals(previewFeatures, features) ? collection : new(previewFeatures);
        var pngPath = Path.Combine(MapsDirectory, $"{region.Key}.{layer}.png");
        MapRenderer.RenderPng(
            preview,
            pngPath,
            new()
            {
                Bounds = bounds,
                Width = 1024,
                Compression = CompressionLevel.Fastest,
                Label = HasNames(layer) ? NameLabel : null,
                LabelPriority = layer == MapLayer.Cities ? CityPriority : null,
            });
    }

    // Keeps only the lowest (broadest) admin_level per country — OSM numbers smaller = higher up the
    // hierarchy, so a country shipping levels 4 and 6 keeps the level-4 features. Features missing
    // an iso1 or admin_level are kept as-is so nothing silently disappears from the preview.
    static IReadOnlyList<Feature> TopLevelSubdivisions(IReadOnlyList<Feature> features)
    {
        var topLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in features)
        {
            var country = CountryOf(feature);
            if (country is null ||
                AdminLevel(feature) is not { } level)
            {
                continue;
            }

            if (!topLevel.TryGetValue(country, out var current) ||
                level < current)
            {
                topLevel[country] = level;
            }
        }

        return [.. features.Where(_ =>
        {
            var country = CountryOf(_);
            if (country is null ||
                AdminLevel(_) is not { } level)
            {
                return true;
            }

            return !topLevel.TryGetValue(country, out var top) ||
                   level == top;
        })];
    }

    // country-levels' iso2 files carry only the iso2 code (e.g. "BD-A"), not a separate iso1. Derive
    // the country from the leading segment, matching CountryLevels.Country's own split-on-hyphen.
    static string? CountryOf(Feature feature)
    {
        var iso2 = Props.Text(feature, "iso2");
        if (iso2.Length == 0)
        {
            return null;
        }

        var dash = iso2.IndexOf('-');
        if (dash < 0)
        {
            return iso2;
        }

        return iso2[..dash];
    }

    static int? AdminLevel(Feature feature)
    {
        var text = Props.Text(feature, "admin_level");
        if (int.TryParse(text, out var value))
        {
            return value;
        }

        return null;
    }

    // Layers whose features carry a "name" property worth rendering as a label: country-levels supplies
    // names for Borders and StatesProvinces; Natural Earth supplies names for Cities, Rivers and Lakes
    // (and rivers/lakes are already filtered to named features only). Coastline/Land/Ocean come from
    // osmdata polygons with no name attribute, so labels would be empty noise.
    static bool HasNames(MapLayer layer) =>
        layer is MapLayer.Borders
            or MapLayer.StatesProvinces
            or MapLayer.Cities
            or MapLayer.Rivers
            or MapLayer.Lakes;

    static string? NameLabel(Feature feature) =>
        feature.Properties.TryGetValue("name", out var value) ? value as string : null;

    // Label priority for the Cities layer: the bigger place wins the renderer's greedy label-collision
    // pass, so a metropolis claims its slot before a nearby small town. Without this every point ties at
    // the renderer's default priority (0) and source-file order decides — which is how Ulladulla (pop
    // 9,250, line 69k in Natural Earth) beat Sydney (pop ~5M, line 99k) on the world preview: their
    // labels overlap at world scale, and the earlier one was placed first. rank (Natural Earth's
    // gap-free 0–14 label rank) is the dominant key so it always orders tiers correctly; population
    // breaks ties within a rank and is defined more finely. Multiplying rank by 1e8 keeps it above any
    // real population (Tokyo's ~37M), so a higher rank never loses to a denser lower-rank place.
    static double CityPriority(Feature feature) =>
        Props.Number(feature, "rank") * 100_000_000 + Props.Number(feature, "population");

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

        var packagePath = Path.Combine(OutputDirectory, $"{region.Key}.{version}.nupkg");
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

    // Register this region's layer files as @(MapBundleData) (tagged with the region). The MapBundle
    // core package's buildTransitive/MapBundle.targets consumes them — copying the raw FlatGeobuf by
    // default, or converting / rendering when the consumer opts in via the MapBundle* properties.
    internal static string Targets(Region region) =>
        $"""
        <Project>
          <ItemGroup>
            <MapBundleData Include="$(MSBuildThisFileDirectory)..\data\*">
              <Region>{region.Key}</Region>
            </MapBundleData>
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
          "attribution": "{{attribution}}",
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
            var rows = bundles.Where(match)
                .OrderBy(_ => _.Region.Name, StringComparer.Ordinal)
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            builder.Append(
                $"""
                ### {heading}

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
