namespace MapBundle.Builder;

/// <summary>
/// A published region package. Country membership is declarative: a country is selected if it is not
/// excluded and it is either explicitly included, in one of the listed continents, or in one of the
/// listed Natural Earth sub-regions. Overlap between regions is allowed.
/// </summary>
public sealed record Region(
    string Key,
    string Name,
    string[] Subregions,
    string[] Continents,
    string[] IncludeIso,
    string[] ExcludeIso,
    bool All = false)
{
    public string PackageId => $"MapBundle.{Key}";

    public bool Selects(Country country)
    {
        if (ExcludeIso.Contains(country.Iso, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (All)
        {
            return true;
        }

        if (IncludeIso.Contains(country.Iso, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Continents.Contains(country.Continent, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return Subregions.Contains(country.Subregion, StringComparer.OrdinalIgnoreCase);
    }
}
