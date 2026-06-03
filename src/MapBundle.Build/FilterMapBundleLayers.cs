using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MapBundle.Build;

/// <summary>
/// The MSBuild task that <c>buildTransitive/MapBundle.targets</c> invokes when the consumer has set
/// <c>MapBundleLayers</c> and/or <c>MapBundleExcludeLayers</c>. A thin shell over <see cref="LayerFilter"/>:
/// resolves the two property strings to canonical layer-filename sets (failing the build with a clear
/// error on any unknown name), then returns the subset of input items that survive the filter — each
/// carrying its original <c>Region</c> metadata so the targets can stage them into <c>maps/&lt;Region&gt;</c>.
/// </summary>
public sealed class FilterMapBundleLayers : Microsoft.Build.Utilities.Task
{
    /// <summary>The data-package layer files (<c>@(MapBundleData)</c>), each with <c>Region</c> metadata.</summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>The raw <c>MapBundleLayers</c> property value (whitelist), or empty for no whitelist.</summary>
    public string Layers { get; set; } = "";

    /// <summary>The raw <c>MapBundleExcludeLayers</c> property value (blacklist), or empty for none.</summary>
    public string ExcludeLayers { get; set; } = "";

    /// <summary>The subset of <see cref="SourceFiles"/> that survives the filter.</summary>
    [Output]
    public ITaskItem[] FilteredFiles { get; set; } = [];

    public override bool Execute()
    {
        // Resolve both lists independently and only fail after both have been tried, so a consumer
        // with a typo in EACH property sees both errors in one build. Doing this serially with one
        // try/catch would short-circuit on the whitelist and hide the blacklist typo, surfacing it
        // only on the next build — the same one-fix-per-rebuild churn the per-list "all unknowns at
        // once" report in Resolve already avoids.
        var whitelist = TryResolve(Layers, "MapBundleLayers");
        var blacklist = TryResolve(ExcludeLayers, "MapBundleExcludeLayers");
        if (whitelist is null || blacklist is null)
        {
            return false;
        }

        FilteredFiles = SourceFiles
            .Where(item => LayerFilter.ShouldKeep(
                Path.GetFileNameWithoutExtension(item.ItemSpec),
                whitelist,
                blacklist))
            .ToArray();
        return true;
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
}
