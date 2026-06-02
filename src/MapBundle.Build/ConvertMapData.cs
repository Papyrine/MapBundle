using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MapBundle.Build;

/// <summary>
/// The MSBuild task that <c>buildTransitive/MapBundle.targets</c> invokes at a consumer's build time.
/// It is a thin shell over <see cref="MapConverter"/>: it maps the <c>MapBundle*</c> MSBuild
/// properties onto a <see cref="ConvertRequest"/>, runs it, and returns the produced files (each
/// carrying its <c>Region</c> metadata) so the targets can stage them into <c>maps/&lt;Region&gt;</c>.
/// </summary>
public sealed class ConvertMapData : Microsoft.Build.Utilities.Task
{
    /// <summary>The data-package layer files, each with <c>Region</c> metadata.</summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>Where produced files are written (one subfolder per region).</summary>
    [Required]
    public string OutputDirectory { get; set; } = "";

    /// <summary>The vector format to emit (a <c>GeoFormat</c> name). <c>FlatGeobuf</c> copies verbatim.</summary>
    public string Format { get; set; } = "FlatGeobuf";

    /// <summary>When false, no vector data is emitted (images only, if enabled).</summary>
    public bool CopyData { get; set; } = true;

    /// <summary>Vertex-reduction tolerance (a <c>double</c>); empty or <c>0</c> leaves geometry untouched.</summary>
    public string SimplifyTolerance { get; set; } = "";

    /// <summary>The simplify algorithm (a <c>SimplifyMethod</c> name): DouglasPeucker or Visvalingam.</summary>
    public string SimplifyMethod { get; set; } = "";

    /// <summary>When true, a stacked preview PNG is rendered per region.</summary>
    public bool RenderImages { get; set; }

    public string ImageWidth { get; set; } = "";
    public string ImageHeight { get; set; } = "";
    public string ImagePadding { get; set; } = "";
    public string ImageProjection { get; set; } = "";
    public string ImageBackground { get; set; } = "";
    public string ImageOcean { get; set; } = "";
    public string ImageStroke { get; set; } = "";
    public string ImageFill { get; set; } = "";
    public string ImageStrokeWidth { get; set; } = "";
    public string ImagePointRadius { get; set; } = "";
    public string ImageStrokeAutoScale { get; set; } = "";
    public string ImageLabels { get; set; } = "";
    public string ImageLabelSize { get; set; } = "";
    public string ImageLabelColor { get; set; } = "";
    public string ImageCompression { get; set; } = "";

    /// <summary>Every file produced, each with its <c>Region</c> metadata, for the targets to stage.</summary>
    [Output]
    public ITaskItem[] ConvertedFiles { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            var simplifyTolerance = 0d;
            Apply(SimplifyTolerance, _ => simplifyTolerance = double.Parse(_, System.Globalization.CultureInfo.InvariantCulture));
            var simplifyMethod = GeoConvert.SimplifyMethod.DouglasPeucker;
            Apply(SimplifyMethod, _ => simplifyMethod = MapConverter.ParseSimplifyMethod(_));

            var request = new ConvertRequest
            {
                Sources = [.. SourceFiles.Select(_ => new MapConverter.Source(_.ItemSpec, _.GetMetadata("Region")))],
                OutputDirectory = OutputDirectory,
                Format = MapConverter.ParseFormat(Format),
                CopyData = CopyData,
                SimplifyTolerance = simplifyTolerance,
                SimplifyMethod = simplifyMethod,
                RenderImages = RenderImages,
                Image = BuildImageOptions(),
            };

            var outputs = MapConverter.Convert(request);
            ConvertedFiles = [.. outputs.Select(_ =>
            {
                var item = new TaskItem(_.Path);
                item.SetMetadata("Region", _.Region);
                return (ITaskItem) item;
            })];

            Log.LogMessage(MessageImportance.Normal, $"MapBundle produced {ConvertedFiles.Length} file(s) in '{OutputDirectory}'.");
            return true;
        }
        catch (Exception exception)
        {
            // Surface as a build error rather than a stack trace dump — the messages from MapConverter
            // are written for a consumer reading their own build log.
            Log.LogError($"MapBundle conversion failed: {exception.Message}");
            return false;
        }
    }

    ImageOptions BuildImageOptions()
    {
        var options = new ImageOptions();
        Apply(ImageWidth, _ => options.Width = int.Parse(_));
        Apply(ImageHeight, _ => options.Height = int.Parse(_));
        Apply(ImagePadding, _ => options.Padding = int.Parse(_));
        Apply(ImageProjection, _ => options.Projection = MapConverter.ParseProjection(_));
        Apply(ImageBackground, _ => options.Background = MapConverter.ParseColor(_));
        Apply(ImageOcean, _ => options.Ocean = MapConverter.ParseColor(_));
        Apply(ImageStroke, _ => options.Stroke = MapConverter.ParseColor(_));
        Apply(ImageFill, _ => options.Fill = MapConverter.ParseColor(_));
        Apply(ImageStrokeWidth, _ => options.StrokeWidth = int.Parse(_));
        Apply(ImagePointRadius, _ => options.PointRadius = int.Parse(_));
        Apply(ImageStrokeAutoScale, _ => options.StrokeAutoScale = bool.Parse(_));
        Apply(ImageLabels, _ => options.Labels = bool.Parse(_));
        Apply(ImageLabelSize, _ => options.LabelSize = double.Parse(_, System.Globalization.CultureInfo.InvariantCulture));
        Apply(ImageLabelColor, _ => options.LabelColor = MapConverter.ParseColor(_));
        Apply(ImageCompression, _ => options.Compression = MapConverter.ParseCompression(_));
        return options;
    }

    static void Apply(string value, Action<string> set)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            set(value.Trim());
        }
    }
}
