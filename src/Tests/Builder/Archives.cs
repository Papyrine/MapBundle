using System.Formats.Tar;

namespace MapBundle.Builder;

/// <summary>
/// Downloads (via Replicant's cache) and extracts the zip/tar.gz archives the OSM sources ship as.
/// Extraction is itself cached: a <c>.extracted</c> marker lets repeat runs skip re-unpacking.
/// </summary>
static class Archives
{
    /// <summary>Downloads a <c>.zip</c> and extracts it, returning the extraction directory.</summary>
    public static async Task<string> Zip(HttpCache httpCache, string url, string directory)
    {
        var archive = await Download(httpCache, url, directory);
        return Extract(archive, _ => ZipFile.ExtractToDirectory(archive, _));
    }

    /// <summary>Downloads a <c>.tgz</c>/<c>.tar.gz</c> and extracts it, returning the extraction directory.</summary>
    public static async Task<string> TarGz(HttpCache httpCache, string url, string directory)
    {
        var archive = await Download(httpCache, url, directory);
        return Extract(archive, target =>
        {
            using var file = File.OpenRead(archive);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, target, overwriteFiles: true);
        });
    }

    /// <summary>The first file matching <paramref name="pattern"/> anywhere under <paramref name="directory"/>.</summary>
    public static string? Find(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).FirstOrDefault();

    static async Task<string> Download(HttpCache httpCache, string url, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(new Uri(url).LocalPath));
        await httpCache.ToFileAsync(url, path);
        return path;
    }

    static string Extract(string archive, Action<string> extract)
    {
        var target = Path.ChangeExtension(archive, null);
        if (Path.GetExtension(target) == ".tar")
        {
            target = Path.ChangeExtension(target, null);
        }

        var marker = Path.Combine(target, ".extracted");
        if (File.Exists(marker))
        {
            return target;
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.CreateDirectory(target);
        extract(target);
        File.WriteAllText(marker, archive);
        return target;
    }
}
