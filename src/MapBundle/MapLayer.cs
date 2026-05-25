namespace MapBundle;

/// <summary>The feature layers a <see cref="Map"/> can contain.</summary>
public enum MapLayer
{
    /// <summary>Country boundary polygons.</summary>
    Borders,

    /// <summary>Populated places (cities and towns) as points.</summary>
    Cities,

    /// <summary>River and lake centerlines.</summary>
    Rivers,

    /// <summary>Lake and reservoir polygons.</summary>
    Lakes,
}
