/// <summary>
/// A published region package, derived from a Geofabrik index entry. Continents have no
/// <see cref="Parent"/>; countries name their continent. <see cref="World"/> is synthetic and merges
/// every continent.
/// </summary>
public sealed record Region(
    string Id,
    string? Parent,
    string Name,
    string[] Iso,
    string? ShpUrl,
    bool IsWorld = false)
{
    /// <summary>The folder/key the data ships under (PascalCase), for example <c>"Monaco"</c> or <c>"NorthAmerica"</c>.</summary>
    public string Key => IsWorld ? "World" : Pascal(Id);

    public string PackageId => $"MapBundle.{Key}";

    /// <summary>A continent (or continent-sized leaf such as Russia): a top-level Geofabrik region.</summary>
    public bool IsContinent => !IsWorld && Parent is null;

    static string Pascal(string id) =>
        string.Concat(id
            .Split('-', '/')
            .Where(_ => _.Length > 0)
            .Select(_ => char.ToUpperInvariant(_[0]) + _[1..]));
}
