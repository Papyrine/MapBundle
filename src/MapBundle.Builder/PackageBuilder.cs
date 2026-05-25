namespace MapBundle.Builder;

/// <summary>Filters the source layers for a region, writes the FlatGeobuf files, and packs the nupkg.</summary>
public static class PackageBuilder
{
    const string Tags = "map maps geo geospatial naturalearth flatgeobuf borders cities rivers offline";

    // Natural Earth ships dozens of attribute columns per feature; keep only a small, useful subset.
    // This shrinks the data and avoids overflowing the FlatGeobuf header with very wide attribute tables.
    static readonly string[] BorderKeys = ["NAME", "NAME_LONG", "ISO_A2", "ADM0_A3", "CONTINENT", "SUBREGION"];
    static readonly string[] CityKeys = ["NAME", "ADM0_A3", "SCALERANK", "POP_MAX"];
    static readonly string[] WaterKeys = ["name", "name_en", "featurecla", "scalerank"];

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

        var places = sources.Places
            .Where(_ => region.All || isoSet.Contains(Props.Text(_, "ADM0_A3")));

        var counts = new Dictionary<MapLayer, int>();
        Write(directory, MapLayer.Borders, Trim(borderFeatures, BorderKeys), counts);
        Write(directory, MapLayer.Cities, Trim(places, CityKeys), counts);

        if (sources.Rivers is { } rivers)
        {
            Write(directory, MapLayer.Rivers, Trim(Within(rivers, bounds, region.All), WaterKeys), counts);
        }

        if (sources.Lakes is { } lakes)
        {
            Write(directory, MapLayer.Lakes, Trim(Within(lakes, bounds, region.All), WaterKeys), counts);
        }

        File.WriteAllText(Path.Combine(directory, "meta.json"), Meta(region, sources.Scale, counts));
        return directory;
    }

    /// <summary>Packs a staged region folder into a <c>.nupkg</c> and returns its path.</summary>
    public static string Pack(Region region, string stagingDirectory, Options options)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(stagingDirectory))
        {
            files[$"data/{Path.GetFileName(file)}"] = File.ReadAllBytes(file);
        }

        files[$"buildTransitive/{region.PackageId}.targets"] = Encoding.UTF8.GetBytes(Targets(region));

        if (File.Exists(options.IconPath))
        {
            files["icon.png"] = File.ReadAllBytes(options.IconPath);
        }

        if (File.Exists(options.ReadmePath))
        {
            files["readme.md"] = File.ReadAllBytes(options.ReadmePath);
        }

        var packagePath = Path.Combine(options.OutputDirectory, $"{region.PackageId}.{options.Version}.nupkg");
        NuPkgWriter.Write(
            packagePath,
            region.PackageId,
            options.Version,
            Description(region),
            options.ProjectUrl,
            Tags,
            dependencyId: "MapBundle",
            dependencyVersion: options.Version,
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
