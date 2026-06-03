using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MapBundle.Build;

/// <summary>
/// The MSBuild task that <c>buildTransitive/MapBundle.targets</c> invokes at a consumer's build time.
/// Two responsibilities in one pass over <see cref="SourceFiles"/>:
/// <list type="number">
///   <item><description><b>Filter</b> the data-package layer files against <see cref="Layers"/>
///   (whitelist) and <see cref="ExcludeLayers"/> (blacklist), failing the build with a clear error on
///   any unknown name. Always populates <see cref="FilteredSourceFiles"/>; the targets file replaces
///   <c>@(MapBundleData)</c> with it so downstream targets see the post-filter set.</description></item>
///   <item><description><b>Convert</b> the surviving files via <see cref="MapConverter"/> when any of
///   <c>MapBundleFormat</c> / <c>MapBundleSimplifyTolerance</c> / <c>MapBundleRenderImages</c> /
///   <c>MapBundleCopyData=false</c> demand it. Populates <see cref="ConvertedFiles"/> with what was
///   produced. Skipped entirely on the default raw-copy path so the targets file's task-free
///   verbatim-copy fast path is unaffected.</description></item>
/// </list>
/// The convert step never sees layers the consumer is about to drop, and a filter-only consumer
/// (default format, no simplify, no images) pays no per-file conversion cost. Both the filter and the
/// convert logic are pure C# (<see cref="LayerFilter"/>, <see cref="MapConverter"/>) — unit-testable
/// without spinning up MSBuild.
/// </summary>
public sealed class ConvertMapData : Microsoft.Build.Utilities.Task
{
    /// <summary>The data-package layer files (<c>@(MapBundleData)</c>), each with <c>Region</c> metadata.</summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>The raw <c>MapBundleLayers</c> property value (whitelist), or empty for no whitelist.</summary>
    public string Layers { get; set; } = "";

    /// <summary>The raw <c>MapBundleExcludeLayers</c> property value (blacklist), or empty for none.</summary>
    public string ExcludeLayers { get; set; } = "";

    /// <summary>Where produced files are written (one subfolder per region).</summary>
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
    public string ImageMinFeaturePixels { get; set; } = "";
    public string ImageLabels { get; set; } = "";
    public string ImageLabelSize { get; set; } = "";
    public string ImageLabelColor { get; set; } = "";
    public string ImageCompression { get; set; } = "";

    /// <summary>
    /// The subset of <see cref="SourceFiles"/> that survives the layer filter, each with its original
    /// <c>Region</c> metadata. The targets file replaces <c>@(MapBundleData)</c> with this so the
    /// raw-copy path (and any downstream target) only sees the surviving layers. When no filter is
    /// set this is just <see cref="SourceFiles"/> verbatim.
    /// </summary>
    [Output]
    public ITaskItem[] FilteredSourceFiles { get; set; } = [];

    /// <summary>
    /// The files <see cref="MapConverter"/> produced (each with its <c>Region</c> metadata), for the
    /// targets file to stage into <c>maps/&lt;Region&gt;</c>. Empty when the default raw-copy path is
    /// in effect (no conversion was performed).
    /// </summary>
    [Output]
    public ITaskItem[] ConvertedFiles { get; set; } = [];

    public override bool Execute()
    {
        // Phase 1: resolve both filter lists independently and only fail after both have been tried,
        // so a consumer with a typo in EACH property sees both errors in one build. Doing this serially
        // with a single try/catch would short-circuit on the whitelist and hide the blacklist typo,
        // surfacing it only on the next build — the same one-fix-per-rebuild churn the per-list
        // "all unknowns at once" report in LayerFilter.Resolve already avoids.
        var whitelist = TryResolve(Layers, "MapBundleLayers");
        var blacklist = TryResolve(ExcludeLayers, "MapBundleExcludeLayers");
        if (whitelist is null || blacklist is null)
        {
            return false;
        }

        // Phase 2: filter SourceFiles to the survivors. Even when no filter is set this returns
        // SourceFiles verbatim, so the targets file can unconditionally replace @(MapBundleData) with
        // FilteredSourceFiles without having to gate on the filter properties.
        FilteredSourceFiles = SourceFiles
            .Where(item => LayerFilter.ShouldKeep(
                Path.GetFileNameWithoutExtension(item.ItemSpec),
                whitelist,
                blacklist))
            .ToArray();

        // Phase 3: convert if any conversion-related setting is non-default. Filter-only consumers
        // (the default Format / no simplify / no images / CopyData=true) skip this entirely so the
        // raw-copy path's task-free verbatim copy stays the fast path it's always been.
        if (!NeedsConvert())
        {
            return true;
        }

        try
        {
            var simplifyTolerance = 0d;
            Apply(SimplifyTolerance, _ => simplifyTolerance = double.Parse(_, System.Globalization.CultureInfo.InvariantCulture));
            var simplifyMethod = GeoConvert.SimplifyMethod.DouglasPeucker;
            Apply(SimplifyMethod, _ => simplifyMethod = MapConverter.ParseSimplifyMethod(_));

            var request = new ConvertRequest
            {
                Sources = [.. FilteredSourceFiles.Select(_ => new MapConverter.Source(_.ItemSpec, _.GetMetadata("Region")))],
                OutputDirectory = OutputDirectory,
                Format = MapConverter.ParseFormat(Format),
                CopyData = CopyData,
                SimplifyTolerance = simplifyTolerance,
                SimplifyMethod = simplifyMethod,
                RenderImages = RenderImages,
                Image = BuildImageOptions(),
                SettingsKey = BuildSettingsKey(),
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

    IReadOnlyCollection<string>? TryResolve(string raw, string propertyName)
    {
        try
        {
            return LayerFilter.Resolve(raw, propertyName);
        }
        catch (ArgumentException exception)
        {
            // Surface as a build error so the user sees it the same way they'd see a normal MSBuild
            // error, not a stack-trace dump. Returning null lets Execute report every list's failure
            // before bailing.
            Log.LogError(exception.Message);
            return null;
        }
    }

    // The C# mirror of the targets file's _MapBundleNeedsTask gate. Kept here so the task self-decides
    // whether to invoke MapConverter — which means a filter-only consumer pays no conversion cost even
    // though it still has to load this task to do the filter.
    bool NeedsConvert() =>
        !string.Equals(Format, "FlatGeobuf", StringComparison.OrdinalIgnoreCase)
        || RenderImages
        || HasPositiveSimplifyTolerance()
        || !CopyData;

    bool HasPositiveSimplifyTolerance() =>
        !string.IsNullOrWhiteSpace(SimplifyTolerance)
        && double.TryParse(SimplifyTolerance, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tolerance)
        && tolerance > 0;

    // A signature of every property that affects what Convert emits, built from the raw MSBuild strings
    // so nothing is missed. MapConverter persists it beside the outputs and regenerates them whenever it
    // changes — even when the source .fgb (and hence its timestamp) is untouched. Layers / ExcludeLayers
    // are included so that flipping which layers ship invalidates the cache too: today only the convert
    // outputs are cached (filter-only mode skips this), but if a consumer flips from Format=GeoJson with
    // Layers=Borders to Layers=Borders;Cities, the new Cities layer must be emitted on the next build.
    string BuildSettingsKey() =>
        string.Join(
            "\n",
            Format,
            CopyData ? "data" : "no-data",
            SimplifyTolerance,
            SimplifyMethod,
            RenderImages ? "images" : "no-images",
            Layers, ExcludeLayers,
            ImageWidth, ImageHeight, ImagePadding, ImageProjection, ImageBackground, ImageOcean,
            ImageStroke, ImageFill, ImageStrokeWidth, ImagePointRadius, ImageStrokeAutoScale,
            ImageMinFeaturePixels,
            ImageLabels, ImageLabelSize, ImageLabelColor, ImageCompression);

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
        Apply(ImageMinFeaturePixels, _ => options.MinFeaturePixels = double.Parse(_, System.Globalization.CultureInfo.InvariantCulture));
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
