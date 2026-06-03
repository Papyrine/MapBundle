// This project exists only as a build-failure fixture — see InvalidConsumer.csproj. The validation
// inside the MapBundle.targets _MapBundleFilterLayers target fails the build before this entry point
// is ever reached, so nothing needs to live here.
System.Console.WriteLine("unreachable");
