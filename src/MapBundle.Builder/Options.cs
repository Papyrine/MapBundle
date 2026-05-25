namespace MapBundle.Builder;

/// <summary>Parsed command-line options for the builder.</summary>
public sealed class Options
{
    public Scale Scale { get; init; } = Scale.M10;
    public string OutputDirectory { get; init; } = "nugets";
    public string CacheDirectory { get; init; } = Path.Combine(".cache", "naturalearth");
    public string Version { get; init; } = "0.1.0";
    public string IconPath { get; init; } = Path.Combine("src", "icon.png");
    public string ReadmePath { get; init; } = "readme.md";
    public string ProjectUrl { get; init; } = "https://github.com/SimonCropp/MapBundle";
    public IReadOnlyList<string>? OnlyRegions { get; init; }

    public bool IncludesRegion(Region region) =>
        OnlyRegions is null || OnlyRegions.Contains(region.Key, StringComparer.OrdinalIgnoreCase);

    public static Options Parse(string[] args)
    {
        var scale = Scale.M10;
        var output = "nugets";
        var cache = Path.Combine(".cache", "naturalearth");
        var version = "0.1.0";
        var icon = Path.Combine("src", "icon.png");
        var readme = "readme.md";
        List<string>? only = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            string Value()
            {
                if (++i >= args.Length)
                {
                    throw new MapBundleException($"Missing value for '{arg}'.");
                }

                return args[i];
            }

            switch (arg)
            {
                case "--scale":
                    if (!NaturalEarth.TryParseScale(Value(), out scale))
                    {
                        throw new MapBundleException("Scale must be one of 110m, 50m, 10m.");
                    }

                    break;
                case "--output":
                    output = Value();
                    break;
                case "--cache":
                    cache = Value();
                    break;
                case "--version":
                    version = Value();
                    break;
                case "--icon":
                    icon = Value();
                    break;
                case "--readme":
                    readme = Value();
                    break;
                case "--regions":
                    only = Value()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();
                    break;
                default:
                    throw new MapBundleException($"Unknown argument '{arg}'.");
            }
        }

        return new()
        {
            Scale = scale,
            OutputDirectory = output,
            CacheDirectory = cache,
            Version = version,
            IconPath = icon,
            ReadmePath = readme,
            OnlyRegions = only,
        };
    }
}
