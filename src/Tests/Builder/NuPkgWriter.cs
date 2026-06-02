/// <summary>
/// Writes a NuGet package (an OPC zip) by hand — no SDK pack step. Adds the required
/// <c>[Content_Types].xml</c>, <c>_rels/.rels</c>, <c>.nuspec</c> and core-properties entries around
/// the supplied payload files (data, buildTransitive targets, icon, readme).
/// </summary>
public static class NuPkgWriter
{
    public static void Write(
        string packagePath,
        string id,
        string version,
        string description,
        string projectUrl,
        string tags,
        string license,
        string dependencyId,
        string dependencyVersion,
        IReadOnlyDictionary<string, byte[]> files)
    {
        var hasIcon = files.ContainsKey("icon.png");
        var hasReadme = files.ContainsKey("readme.md");
        var psmdcp = $"package/services/metadata/core-properties/{Guid.NewGuid():N}.psmdcp";

        using var stream = File.Create(packagePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteText(zip, "[Content_Types].xml", ContentTypes());
        WriteText(zip, "_rels/.rels", Rels(id, psmdcp));
        WriteText(zip, $"{id}.nuspec", Nuspec(id, version, description, projectUrl, tags, license, dependencyId, dependencyVersion, hasIcon, hasReadme));
        WriteText(zip, psmdcp, Psmdcp(id, version, description, tags));

        foreach (var pair in files)
        {
            WriteBytes(zip, pair.Key, pair.Value);
        }
    }

    static void WriteText(ZipArchive zip, string name, string text) =>
        WriteBytes(zip, name, Encoding.UTF8.GetBytes(text));

    static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    static string ContentTypes() =>
        """
        <?xml version="1.0" encoding="utf-8"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="psmdcp" ContentType="application/vnd.openxmlformats-package.core-properties+xml" />
          <Default Extension="nuspec" ContentType="application/octet" />
          <Default Extension="fgb" ContentType="application/octet" />
          <Default Extension="json" ContentType="application/octet" />
          <Default Extension="targets" ContentType="application/octet" />
          <Default Extension="png" ContentType="image/png" />
          <Default Extension="md" ContentType="text/markdown" />
        </Types>
        """;

    static string Rels(string id, string psmdcp) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/{id}.nuspec" Id="Rel1" />
          <Relationship Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="/{psmdcp}" Id="Rel2" />
        </Relationships>
        """;

    static string Nuspec(
        string id,
        string version,
        string description,
        string projectUrl,
        string tags,
        string license,
        string dependencyId,
        string dependencyVersion,
        bool hasIcon,
        bool hasReadme)
    {
        var icon = hasIcon ? "\n    <icon>icon.png</icon>" : "";
        var readme = hasReadme ? "\n    <readme>readme.md</readme>" : "";
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>{id}</id>
            <version>{version}</version>
            <authors>Simon Cropp</authors>
            <description>{Escape(description)}</description>
            <projectUrl>{projectUrl}</projectUrl>
            <license type="expression">{license}</license>{icon}{readme}
            <tags>{Escape(tags)}</tags>
            <dependencies>
              <group targetFramework="net8.0">
                <dependency id="{dependencyId}" version="{dependencyVersion}" />
              </group>
            </dependencies>
          </metadata>
        </package>
        """;
    }

    static string Psmdcp(string id, string version, string description, string tags) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <coreProperties xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties">
          <dc:creator>Simon Cropp</dc:creator>
          <dc:description>{Escape(description)}</dc:description>
          <dc:identifier>{id}</dc:identifier>
          <version>{version}</version>
          <keywords>{Escape(tags)}</keywords>
          <lastModifiedBy>MapBundle.Builder</lastModifiedBy>
        </coreProperties>
        """;

    static string Escape(string text) =>
        text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
