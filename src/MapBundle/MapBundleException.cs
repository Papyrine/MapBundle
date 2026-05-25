namespace MapBundle;

/// <summary>Thrown when requested map data is missing or cannot be loaded.</summary>
public class MapBundleException(string message) :
    Exception(message);
