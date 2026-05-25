public class PackageGeneration
{
    [Test]
    public async Task Generate()
    {
        // Opt-in: downloads Natural Earth and writes every data .nupkg into nugets/. The CI data-build
        // step sets MAPBUNDLE_BUILD=1 and runs just this test; otherwise it's a no-op so normal test
        // runs stay fast.
        if (Environment.GetEnvironmentVariable("MAPBUNDLE_BUILD") is null)
        {
            return;
        }

        await PackageBuilder.RunAsync();
    }
}
