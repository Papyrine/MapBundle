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

    /// <summary>When true, a stacked preview PNG is rendered per region.</summary>
    public bool RenderImages { get; init; }

    /// <summary>The image render toggles (only consulted when <see cref="RenderImages"/> is true).</summary>
    public ImageOptions Image { get; init; } = new();
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
            LabelSize = LabelSize,
            LabelColor = LabelColor,
            Label = Labels ? NameLabel : null,
            Compression = Compression,
        };

    static string? NameLabel(Feature feature) =>
        feature.Properties.TryGetValue("name", out var value) ? value as string : null;
}
