namespace MapBundle;

/// <summary>The set of region maps available in a directory.</summary>
public sealed class MapCatalog
{
    string root;

    /// <summary>Creates a catalog over <paramref name="root"/> (one subdirectory per installed region).</summary>
    public MapCatalog(string root) =>
        this.root = root;

    /// <summary>The directory this catalog reads from.</summary>
    public string Directory => root;

    /// <summary>The regions with data present, ordered by name.</summary>
    public IReadOnlyList<string> Regions =>
        System.IO.Directory.Exists(root)
            ? System.IO.Directory.GetDirectories(root)
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(_ => _, StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>Whether data for <paramref name="region"/> is present.</summary>
    public bool Contains(string region) =>
        System.IO.Directory.Exists(Path.Combine(root, region));

    /// <summary>Loads the map for <paramref name="region"/> (for example <c>"World"</c> or <c>"EuropeWestern"</c>).</summary>
    public Map Load(string region)
    {
        var directory = Path.Combine(root, region);
        if (System.IO.Directory.Exists(directory))
        {
            return new(region, directory);
        }

        throw new MapBundleException($"No map data for region '{region}' in '{root}'. Add the MapBundle.{region} (or MapBundle.World) package.");
    }
}
