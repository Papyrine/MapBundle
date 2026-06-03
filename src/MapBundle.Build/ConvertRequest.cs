using System.IO.Compression;
using GeoConvert;

namespace MapBundle.Build;

/// <summary>A single conversion run: what to convert, where to, and how to render any preview images.</summary>
public sealed class ConvertRequest
{
    /// <summary>The layer files to process, each tagged with its region.</summary>
    public required IReadOnlyList<MapConverter.Source> Sources { get; init; }

    /// <summary>The directory produced files are written under (one subfolder per region).</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>The vector format data is emitted as. <see cref="GeoFormat.FlatGeobuf"/> copies verbatim.</summary>
    public GeoFormat Format { get; init; } = GeoFormat.FlatGeobuf;

    /// <summary>When false, no vector data is emitted at all (images only, if enabled).</summary>
    public bool CopyData { get; init; } = true;

    /// <summary>
    /// Vertex-reduction tolerance applied to every layer (data and preview) before it is written, via
    /// GeoConvert's <see cref="Simplifier.SimplifyTopology(FeatureCollection, double, SimplifyMethod)"/> —
    /// the topology-preserving variant, so adjacent admin polygons (countries, states) that share an
    /// edge get that edge reduced once to bit-identical vertices on both sides, instead of twice with
    /// different chord choices (which is what produces the hairline gaps the plain overload leaves on
    /// admin layers). <c>0</c> (the default) disables simplification and keeps the verbatim FlatGeobuf
    /// copy fast-path; any positive value forces a read/simplify/write even when <see cref="Format"/>
    /// is FlatGeobuf. Its unit depends on <see cref="SimplifyMethod"/>: a distance in degrees for
    /// Douglas–Peucker, an area in degrees² for Visvalingam.
    /// </summary>
    public double SimplifyTolerance { get; init; }

    /// <summary>The line-simplification algorithm used when <see cref="SimplifyTolerance"/> is positive.</summary>
    public SimplifyMethod SimplifyMethod { get; init; } = SimplifyMethod.DouglasPeucker;

    /// <summary>When true, a stacked preview PNG is rendered per region.</summary>
    public bool RenderImages { get; init; }

    /// <summary>The image render toggles (only consulted when <see cref="RenderImages"/> is true).</summary>
    public ImageOptions Image { get; init; } = new();

    /// <summary>
    /// A signature of the consumer's <c>MapBundle*</c> settings, persisted beside the outputs so that
    /// changing a setting (e.g. <see cref="SimplifyTolerance"/>, <see cref="SimplifyMethod"/> or an
    /// image colour) regenerates them even though the source <c>.fgb</c> is unchanged and would
    /// otherwise look up to date. <c>null</c> (the default, for direct engine callers) keeps the pure
    /// source-vs-output timestamp behaviour; the MSBuild <c>ConvertMapData</c> task always supplies one.
    /// </summary>
    public string? SettingsKey { get; init; }
}

/// <summary>
/// The image-render toggles, surfaced one-to-one as <c>MapBundleImage*</c> MSBuild properties.
/// Defaults mirror GeoConvert's <see cref="RenderOptions"/> so leaving a knob unset matches the
/// renderer's own default.
/// </summary>
public sealed class ImageOptions
{
    public int Width { get; set; } = 2048;
    public int Height { get; set; }
    public int Padding { get; set; } = 8;
    public MapProjection Projection { get; set; } = MapProjection.Auto;
    public Rgba Background { get; set; } = Rgba.White;
    public Rgba? Ocean { get; set; }
    public Rgba Stroke { get; set; } = new(30, 30, 30);
    public Rgba Fill { get; set; } = new(70, 130, 180, 120);
    public int StrokeWidth { get; set; } = 2;
    public int PointRadius { get; set; } = 4;
    public bool StrokeAutoScale { get; set; }

    /// <summary>
    /// Forwarded to <see cref="RenderOptions.MinFeaturePixels"/> — the render-time cartographic
    /// "selection" threshold. A positive value drops polygons / lines whose projected pixel bbox is
    /// below the threshold in both axes, so dense archipelagoes (Indonesia, Norway, Arctic Canada)
    /// don't paint thousands of sub-pixel islands as 1-px specks at world scale while keeping the
    /// mainland that does deserve to render. <c>0</c> (the default) renders everything.
    /// </summary>
    public double MinFeaturePixels { get; set; }

    /// <summary>When true, features carrying a <c>name</c> property are labelled.</summary>
    public bool Labels { get; set; }
    public double LabelSize { get; set; } = 14;
    public Rgba LabelColor { get; set; } = new(20, 20, 20);
    public CompressionLevel Compression { get; set; } = CompressionLevel.Optimal;

    /// <summary>Builds the GeoConvert <see cref="RenderOptions"/> these toggles describe.</summary>
    public RenderOptions ToRenderOptions() =>
        new()
        {
            Width = Width,
            Height = Height,
            Padding = Padding,
            Projection = Projection,
            Background = Background,
            Ocean = Ocean,
            Stroke = Stroke,
            Fill = Fill,
            StrokeWidth = StrokeWidth,
            PointRadius = PointRadius,
            StrokeAutoScale = StrokeAutoScale,
            MinFeaturePixels = MinFeaturePixels,
            LabelSize = LabelSize,
            LabelColor = LabelColor,
            Label = Labels ? NameLabel : null,
            Compression = Compression,
        };

    static string? NameLabel(Feature feature) =>
        feature.Properties.TryGetValue("name", out var value) ? value as string : null;
}
