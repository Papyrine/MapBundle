using System.Xml.Linq;

/// <summary>
/// The single source of truth for the package version: <c>&lt;Version&gt;</c> in
/// <c>src/Directory.Build.props</c>, which the core <c>MapBundle</c> package is built with. The data
/// packages carry the same version and depend on <c>MapBundle (&gt;= that version)</c>, so the
/// dependency always resolves to an existing core package (avoiding NU1603).
/// </summary>
static class CoreVersion
{
    public static readonly string Value = Read();

    static string Read()
    {
        var path = Path.Combine(ProjectFiles.SolutionDirectory, "Directory.Build.props");
        var version = XDocument.Load(path).Descendants("Version").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new($"Could not read <Version> from {path}.");
        }

        return version.Trim();
    }
}
