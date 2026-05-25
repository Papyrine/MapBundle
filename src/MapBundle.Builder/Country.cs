namespace MapBundle.Builder;

/// <summary>A country from the Natural Earth admin-0 layer, with the attributes used for grouping.</summary>
public sealed record Country(string Iso, string Name, string Continent, string Subregion, Feature Feature);
