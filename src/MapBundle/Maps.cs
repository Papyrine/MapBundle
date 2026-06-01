namespace MapBundle;

/// <summary>
/// Entry point for loading bundled map data. By default the data packages (<c>MapBundle.World</c>,
/// <c>MapBundle.[Region]</c>) copy their FlatGeobuf files into a <c>maps</c> folder next to the
/// running application; <see cref="Open()"/> reads from there. (Consumers can instead opt into
/// build-time conversion to another format or image rendering via the <c>MapBundle*</c> MSBuild
/// properties — see the readme — in which case those files are read with GeoConvert directly.)
/// </summary>
public static class Maps
{
    /// <summary>The default location data packages copy into: a <c>maps</c> folder beside the app.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "maps");

    /// <summary>Opens the catalog of map data found in <see cref="DefaultDirectory"/>.</summary>
    public static MapCatalog Open() =>
        Open(DefaultDirectory);

    /// <summary>Opens the catalog of map data found in <paramref name="directory"/>.</summary>
    public static MapCatalog Open(string directory) =>
        new(directory);
}
