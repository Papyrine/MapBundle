using MapBundle.Builder;

var options = Options.Parse(args);

using var http = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(10),
};
http.DefaultRequestHeaders.UserAgent.ParseAdd("MapBundle.Builder");

Console.WriteLine($"Downloading Natural Earth {NaturalEarth.ScaleToken(options.Scale)} into '{options.CacheDirectory}' ...");
var cache = new DataCache(options.CacheDirectory, http);
var sources = await Sources.Download(cache, options.Scale);
Console.WriteLine($"Loaded {sources.Countries.Count} countries and {sources.Places.Count} populated places.");

Directory.CreateDirectory(options.OutputDirectory);
var staging = Path.Combine(options.OutputDirectory, ".staging");

var packed = 0;
foreach (var region in Regions.All.Where(options.IncludesRegion))
{
    var directory = PackageBuilder.BuildRegion(region, sources, staging);
    var package = PackageBuilder.Pack(region, directory, options);
    Console.WriteLine($"  {Path.GetFileName(package)}");
    packed++;
}

Console.WriteLine($"Done. Packed {packed} package(s) into '{options.OutputDirectory}'.");
