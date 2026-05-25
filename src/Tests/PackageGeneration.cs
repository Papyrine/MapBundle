public class PackageGeneration
{
    [Test]
    public async Task Generate()
    {
        // Opt-in: downloads the OSM sources and writes every data .nupkg into nugets/. The CI data-build
        // step sets MAPBUNDLE_BUILD=1 and runs just this test; otherwise it's a no-op so normal test
        // runs stay fast.
        if (Environment.GetEnvironmentVariable("MAPBUNDLE_BUILD") is null)
        {
            return;
        }

        await PackageBuilder.RunAsync();
    }

    [Test]
    public async Task Slice()
    {
        // Opt-in vertical slice: build just the regions named in MAPBUNDLE_SLICE (comma separated, default
        // "monaco") to validate the whole pipeline end-to-end without building the full tree.
        var slice = Environment.GetEnvironmentVariable("MAPBUNDLE_SLICE");
        if (slice is null)
        {
            return;
        }

        var ids = slice.Length == 0 ? ["monaco"] : slice.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await PackageBuilder.BuildAsync(ids);
    }
}
