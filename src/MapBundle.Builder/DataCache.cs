namespace MapBundle.Builder;

/// <summary>Downloads files into a local cache directory, skipping anything already present.</summary>
public sealed class DataCache(string directory, HttpClient client)
{
    public string Directory => directory;

    /// <summary>
    /// Returns the local path for <paramref name="url"/>, downloading it if absent. A non-success
    /// response throws when <paramref name="required"/>, otherwise returns null (the source layer is
    /// treated as unavailable at this scale).
    /// </summary>
    public async Task<string?> Download(string url, string fileName, bool required, CancellationToken cancellation = default)
    {
        var target = Path.Combine(directory, fileName);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            return target;
        }

        System.IO.Directory.CreateDirectory(directory);

        using var response = await client.GetAsync(url, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            if (required)
            {
                throw new MapBundleException($"Failed to download '{url}': {(int) response.StatusCode} {response.ReasonPhrase}.");
            }

            return null;
        }

        var temp = $"{target}.tmp";
        await using (var file = File.Create(temp))
        {
            await response.Content.CopyToAsync(file, cancellation);
        }

        File.Move(temp, target, overwrite: true);
        return target;
    }
}
