using System.Globalization;
using System.IO.Compression;
using GeoConvert;

namespace MapBundle.Build;

/// <summary>
/// The build-time conversion engine behind the <c>ConvertMapData</c> MSBuild task, kept free of any
/// MSBuild types so it can be unit-tested directly. Given the FlatGeobuf layer files a data package
/// ships, it can copy them verbatim, convert them to another <see cref="GeoFormat"/>, and/or render a
/// stacked preview image per region — all via GeoConvert.
/// </summary>
public static class MapConverter
{
    /// <summary>One input layer file together with the region it belongs to.</summary>
    public readonly record struct Source(string Path, string Region);

    /// <summary>One produced file together with the region it belongs to.</summary>
    public readonly record struct Output(string Path, string Region);

    /// <summary>
    /// Copies / converts the supplied data files and (optionally) renders a per-region preview image
    /// into <paramref name="request"/>'s output directory, returning every file produced.
    /// </summary>
    public static IReadOnlyList<Output> Convert(ConvertRequest request)
    {
        if (request.Format == GeoFormat.Png)
        {
            throw new ArgumentException(
                "Png is not a data format; render images with RenderImages instead of setting it as the Format.");
        }

        // A change to the consumer's MapBundle* settings (e.g. a new SimplifyTolerance or image colour)
        // does not touch the source .fgb, so the per-file timestamp check below would wrongly treat the
        // existing (stale) outputs as up to date. SettingsChanged compares the request's settings
        // signature against the one persisted last run and, when it differs, forces every file to be
        // rewritten so the change actually takes effect.
        var rewriteAll = SettingsChanged(request);

        var outputs = new List<Output>();
        foreach (var group in request.Sources.GroupBy(_ => _.Region, StringComparer.Ordinal))
        {
            var region = group.Key;
            var directory = Path.Combine(request.OutputDirectory, region);
            Directory.CreateDirectory(directory);

            var layers = group.Select(_ => _.Path).ToList();

            if (request.CopyData)
            {
                foreach (var source in layers)
                {
                    outputs.Add(EmitData(source, directory, region, request, rewriteAll));
                }
            }

            if (request.RenderImages)
            {
                var image = RenderImage(layers, directory, region, request, rewriteAll);
                if (image is { } produced)
                {
                    outputs.Add(produced);
                }
            }
        }

        WriteSettingsStamp(request);
        return outputs;
    }

    // Either copies the file verbatim (meta.json, or .fgb when the target format is FlatGeobuf and no
    // simplify is requested) or reads a .fgb, optionally simplifies it, and writes it in the requested
    // format. Up-to-date outputs are left untouched so incremental builds stay cheap, but are still
    // reported so the copy-to-output step always sees them.
    static Output EmitData(string source, string directory, string region, ConvertRequest request, bool rewriteAll)
    {
        var format = request.Format;
        var isFgb = string.Equals(Path.GetExtension(source), ".fgb", StringComparison.OrdinalIgnoreCase);
        var convert = isFgb && format != GeoFormat.FlatGeobuf;
        var simplify = isFgb && request.SimplifyTolerance > 0;

        var name = Path.GetFileNameWithoutExtension(source);
        var extension = convert ? Extension(format) : Path.GetExtension(source);
        var target = Path.Combine(directory, name + extension);

        if (rewriteAll || !UpToDate(target, source))
        {
            if (convert || simplify)
            {
                var collection = GeoConverter.Read(source, GeoFormat.FlatGeobuf);
                if (simplify)
                {
                    collection = Simplifier.Simplify(collection, request.SimplifyTolerance, request.SimplifyMethod);
                }

                // `format` is FlatGeobuf in the simplify-only case (convert is false), so this writes the
                // simplified .fgb back; otherwise it writes the converted format.
                GeoConverter.Write(collection, target, format);
            }
            else
            {
                File.Copy(source, target, overwrite: true);
            }
        }

        return new(target, region);
    }

    // Stacks the region's FlatGeobuf layers bottom-up (ocean first, cities last) and renders one PNG.
    // Returns null when the region carries no renderable layer (e.g. a meta-only group).
    static Output? RenderImage(IReadOnlyList<string> layers, string directory, string region, ConvertRequest request, bool rewriteAll)
    {
        var fgb = layers
            .Where(_ => string.Equals(Path.GetExtension(_), ".fgb", StringComparison.OrdinalIgnoreCase))
            .OrderBy(LayerRank)
            .ToList();
        if (fgb.Count == 0)
        {
            return null;
        }

        var target = Path.Combine(directory, region + ".png");
        if (rewriteAll || !UpToDate(target, fgb))
        {
            var collections = fgb
                .Select(_ =>
                {
                    var collection = GeoConverter.Read(_, GeoFormat.FlatGeobuf);
                    // Simplify before render so the preview reflects the same generalisation the data
                    // emission applies (Simplifier preserves the collection name either way).
                    if (request.SimplifyTolerance > 0)
                    {
                        collection = Simplifier.Simplify(collection, request.SimplifyTolerance, request.SimplifyMethod);
                    }

                    collection.Name = Path.GetFileNameWithoutExtension(_);
                    return collection;
                })
                .ToList();
            MapRenderer.RenderPng(collections, target, request.Image.ToRenderOptions());
        }

        return new(target, region);
    }

    // Lower rank paints first (underneath). Layers not in the table sort to the middle so an unknown
    // file never ends up hiding borders or cities.
    static int LayerRank(string path) =>
        Path.GetFileNameWithoutExtension(path).ToLowerInvariant() switch
        {
            "ocean" => 0,
            "land" => 1,
            "lakes" => 2,
            "rivers" => 3,
            "coastline" => 4,
            "states" => 5,
            "borders" => 6,
            "cities" => 8,
            _ => 7,
        };

    static bool UpToDate(string target, string source) =>
        UpToDate(target, [source]);

    static bool UpToDate(string target, IReadOnlyList<string> sources) =>
        File.Exists(target) &&
        sources.All(_ => File.GetLastWriteTimeUtc(_) <= File.GetLastWriteTimeUtc(target));

    // The settings signature from the last run lives in this file at the root of the output directory;
    // regions live in subfolders, so it never collides with one.
    const string settingsStampName = ".mapbundle-settings";

    // True when a settings signature is supplied (the MSBuild task always supplies one) and it differs
    // from the one persisted by the previous run — i.e. the consumer changed a MapBundle* property, so
    // every output must be regenerated. A null signature (direct engine callers) keeps the pure
    // timestamp behaviour, as does a first run whose stamp does not exist yet (nothing is built either).
    static bool SettingsChanged(ConvertRequest request)
    {
        if (request.SettingsKey is not { } key)
        {
            return false;
        }

        var stamp = Path.Combine(request.OutputDirectory, settingsStampName);
        return !File.Exists(stamp) || File.ReadAllText(stamp) != key;
    }

    static void WriteSettingsStamp(ConvertRequest request)
    {
        if (request.SettingsKey is not { } key)
        {
            return;
        }

        Directory.CreateDirectory(request.OutputDirectory);
        File.WriteAllText(Path.Combine(request.OutputDirectory, settingsStampName), key);
    }

    /// <summary>Parses a <see cref="GeoFormat"/> name (case-insensitive), as set via <c>MapBundleFormat</c>.</summary>
    public static GeoFormat ParseFormat(string value)
    {
        if (Enum.TryParse<GeoFormat>(value, ignoreCase: true, out var format))
        {
            return format;
        }

        throw new ArgumentException(
            $"Unknown map format '{value}'. Valid values: {string.Join(", ", Enum.GetNames<GeoFormat>())}.");
    }

    /// <summary>The canonical file extension (including the dot) for a vector <see cref="GeoFormat"/>.</summary>
    public static string Extension(GeoFormat format) =>
        format switch
        {
            GeoFormat.GeoJson => ".geojson",
            GeoFormat.TopoJson => ".topojson",
            GeoFormat.Shapefile => ".shp",
            GeoFormat.FlatGeobuf => ".fgb",
            GeoFormat.Kml => ".kml",
            GeoFormat.Kmz => ".kmz",
            GeoFormat.Gpx => ".gpx",
            GeoFormat.Wkt => ".wkt",
            GeoFormat.Wkb => ".wkb",
            GeoFormat.Csv => ".csv",
            GeoFormat.GeoParquet => ".parquet",
            _ => throw new ArgumentException($"No file extension for format '{format}'."),
        };

    /// <summary>Parses a <see cref="SimplifyMethod"/> name (case-insensitive), as set via <c>MapBundleSimplifyMethod</c>.</summary>
    public static SimplifyMethod ParseSimplifyMethod(string value)
    {
        if (Enum.TryParse<SimplifyMethod>(value, ignoreCase: true, out var method))
        {
            return method;
        }

        throw new ArgumentException(
            $"Unknown simplify method '{value}'. Valid values: {string.Join(", ", Enum.GetNames<SimplifyMethod>())}.");
    }

    /// <summary>Parses a <see cref="MapProjection"/> name (case-insensitive), as set via <c>MapBundleImageProjection</c>.</summary>
    public static MapProjection ParseProjection(string value)
    {
        if (Enum.TryParse<MapProjection>(value, ignoreCase: true, out var projection))
        {
            return projection;
        }

        throw new ArgumentException(
            $"Unknown projection '{value}'. Valid values: {string.Join(", ", Enum.GetNames<MapProjection>())}.");
    }

    /// <summary>Parses a PNG compression level (case-insensitive), as set via <c>MapBundleImageCompression</c>.</summary>
    public static CompressionLevel ParseCompression(string value)
    {
        if (Enum.TryParse<CompressionLevel>(value, ignoreCase: true, out var level))
        {
            return level;
        }

        throw new ArgumentException(
            $"Unknown compression '{value}'. Valid values: {string.Join(", ", Enum.GetNames<CompressionLevel>())}.");
    }

    /// <summary>
    /// Parses an <c>#RGB</c> / <c>#RRGGBB</c> / <c>#RRGGBBAA</c> hex color (the leading <c>#</c> is
    /// optional). Used for every image color toggle.
    /// </summary>
    public static Rgba ParseColor(string value)
    {
        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        static byte Hex(string hex) => byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        try
        {
            switch (text.Length)
            {
                case 3:
                    return new(Hex($"{text[0]}{text[0]}"), Hex($"{text[1]}{text[1]}"), Hex($"{text[2]}{text[2]}"));
                case 6:
                    return new(Hex(text[..2]), Hex(text[2..4]), Hex(text[4..6]));
                case 8:
                    return new(Hex(text[..2]), Hex(text[2..4]), Hex(text[4..6]), Hex(text[6..8]));
            }
        }
        catch (FormatException)
        {
            // Fall through to the shared error below.
        }

        throw new ArgumentException($"'{value}' is not a valid hex color (#RGB, #RRGGBB or #RRGGBBAA).");
    }
}
