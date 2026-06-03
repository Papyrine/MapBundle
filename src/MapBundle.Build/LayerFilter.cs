namespace MapBundle.Build;

/// <summary>
/// The pure logic behind the <c>FilterMapBundleLayers</c> MSBuild task, kept free of any MSBuild types
/// so it can be unit-tested directly. Decides which layer files a data package ships survive the
/// consumer's <c>MapBundleLayers</c> (whitelist) and <c>MapBundleExcludeLayers</c> (blacklist) lists.
///
/// The consumer's input is the human-facing name (the <c>MapLayer</c> enum names like
/// <c>StatesProvinces</c>, or the on-disk filenames like <c>states</c>, in any case, separated by
/// <c>;</c> or <c>,</c>). The output everywhere else in the codebase is the on-disk filename — so
/// <see cref="Resolve"/> normalises both name forms down to filenames in one place and throws if it
/// sees an unrecognised token, so a typo can't silently empty the consumer's <c>maps/&lt;Region&gt;</c>.
/// </summary>
public static class LayerFilter
{
    // The eight on-disk layer filenames (Map.FileName(MapLayer) sans the .fgb extension), lowercase.
    // Keep this in sync with MapBundle.Map.FileName — they share the same names by design (the data
    // package's targets file registers files by these names; the core reads by these names).
    static readonly string[] knownLayerFiles =
    [
        "borders",
        "cities",
        "rivers",
        "lakes",
        "states",
        "coastline",
        "land",
        "ocean",
    ];

    // Aliases the consumer can use → the canonical on-disk filename. Includes every enum-name form
    // (Borders, StatesProvinces, …) and every on-disk-filename form (borders, states, …). The only
    // enum↔filename mismatch is StatesProvinces ↔ states; the rest are identical strings, so the
    // alias map handles both lookup directions uniformly without a separate translation step.
    static readonly Dictionary<string, string> aliases = BuildAliases();

    static Dictionary<string, string> BuildAliases()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in knownLayerFiles)
        {
            map[file] = file;
        }
        // The one enum-name → filename translation. Every other MapLayer enum value is byte-equal to
        // its filename when lowercased ("Borders" → "borders"), so the dictionary already handles them.
        map["StatesProvinces"] = "states";
        return map;
    }

    /// <summary>The eight on-disk layer filenames the data package can carry.</summary>
    public static IReadOnlyCollection<string> KnownLayerFiles => knownLayerFiles;

    /// <summary>The human-readable list of valid names, for error messages.</summary>
    public static string KnownNamesDescription =>
        "Borders, StatesProvinces, Cities, Rivers, Lakes, Coastline, Land, Ocean " +
        "(the on-disk filenames borders/states/cities/rivers/lakes/coastline/land/ocean are also accepted).";

    /// <summary>
    /// Normalises a raw user-supplied list (e.g. <c>"Borders, StatesProvinces; cities"</c>) to the set
    /// of canonical on-disk layer filenames (<c>{"borders", "states", "cities"}</c>). Empty / null /
    /// whitespace returns an empty set. Accepts <c>;</c> and <c>,</c> as separators, strips spaces,
    /// case-insensitive. Throws <see cref="ArgumentException"/> when any token isn't a known layer.
    /// </summary>
    /// <param name="raw">The raw property value (e.g. the consumer's <c>MapBundleLayers</c>).</param>
    /// <param name="propertyName">The MSBuild property name to mention in the error message.</param>
    public static IReadOnlyCollection<string> Resolve(string? raw, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var tokens = raw!
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToArray();

        var unknown = tokens
            .Where(token => !aliases.ContainsKey(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"{propertyName} contains unknown layer name(s): {string.Join(", ", unknown)}. " +
                $"Known layers: {KnownNamesDescription}");
        }

        // HashSet so callers can ContainsCheck cheaply. Ordinal-ignore-case so a later membership test
        // on already-lowercased on-disk filenames isn't surprised by an upper-cased input.
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            resolved.Add(aliases[token]);
        }
        return resolved;
    }

    /// <summary>
    /// True if the file with the given <paramref name="filenameWithoutExtension"/> (e.g.
    /// <c>"borders"</c>, <c>"meta"</c>) survives the supplied whitelist + blacklist.
    /// </summary>
    /// <remarks>
    /// <para>Non-layer files (anything not in <see cref="KnownLayerFiles"/>) are always kept — the
    /// whitelist's "drop everything not listed" rule doesn't apply to e.g. <c>meta.json</c>, which
    /// the core needs to enumerate available layers.</para>
    /// <para>An empty <paramref name="whitelist"/> means "no whitelist" (every layer is allowed),
    /// matching the MSBuild semantics where unset <c>MapBundleLayers</c> means keep all.</para>
    /// </remarks>
    public static bool ShouldKeep(
        string filenameWithoutExtension,
        IReadOnlyCollection<string> whitelist,
        IReadOnlyCollection<string> blacklist)
    {
        // Non-layer files (meta.json, future sidecars) survive untouched.
        if (!IsKnownLayer(filenameWithoutExtension))
        {
            return true;
        }
        // Whitelist applies only when non-empty (unset = all layers allowed).
        if (whitelist.Count > 0 && !ContainsIgnoreCase(whitelist, filenameWithoutExtension))
        {
            return false;
        }
        if (ContainsIgnoreCase(blacklist, filenameWithoutExtension))
        {
            return false;
        }
        return true;
    }

    static bool IsKnownLayer(string filenameWithoutExtension) =>
        knownLayerFiles.Any(known => string.Equals(known, filenameWithoutExtension, StringComparison.OrdinalIgnoreCase));

    static bool ContainsIgnoreCase(IReadOnlyCollection<string> set, string value)
    {
        // HashSet<string> with the case-insensitive comparer (which Resolve returns) handles this in
        // O(1); the fallback path is for callers that pass a plain list, which the unit tests do.
        if (set is HashSet<string> hash && hash.Comparer.Equals(StringComparer.OrdinalIgnoreCase))
        {
            return hash.Contains(value);
        }
        foreach (var item in set)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
