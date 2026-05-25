namespace MapBundle;

/// <summary>The feature layers a <see cref="Map"/> can contain.</summary>
public enum MapLayer
{
    /// <summary>Country boundary polygons (admin level 0).</summary>
    Borders,

    /// <summary>Populated places (cities and towns) as points.</summary>
    Cities,

    /// <summary>River and lake centerlines.</summary>
    Rivers,

    /// <summary>Lake and reservoir polygons.</summary>
    Lakes,

    /// <summary>State / province boundary polygons (admin level 1).</summary>
    StatesProvinces,

    /// <summary>Coastlines as lines.</summary>
    Coastline,

    /// <summary>Global land polygons.</summary>
    Land,

    /// <summary>Global ocean polygons.</summary>
    Ocean,
}
