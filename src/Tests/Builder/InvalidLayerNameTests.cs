/// <summary>
/// Pins the negative behaviour of <c>_MapBundleFilterLayers</c> in <c>buildTransitive/MapBundle.targets</c>:
/// when <c>MapBundleLayers</c> or <c>MapBundleExcludeLayers</c> names a layer that doesn't exist, the
/// consumer's build must FAIL — not silently empty <c>maps/&lt;Region&gt;</c> because the typo's lowercase
/// form isn't on the known-layer list. The companion <c>FilteredConsumer</c> integration project locks
/// in the positive path; this test locks in the negative.
///
/// Driven by invoking <c>dotnet build</c> on <c>IntegrationTests/InvalidConsumer/</c> as a subprocess
/// (that project is deliberately NOT in <c>IntegrationTests.slnx</c> so a plain <c>dotnet build
/// IntegrationTests</c> doesn't try to build it), then inverting the assertion: non-zero exit, and the
/// error text matches the message <c>_MapBundleFilterLayers</c> emits.
///
/// Marked <c>[Explicit]</c> because it depends on the local NuGet feed having both <c>MapBundle</c>
/// (from <c>dotnet build src --configuration Release</c>) and <c>MapBundle.Monaco</c> (from the
/// <c>IntegrationFixture.BuildMonacoPackage</c> test) already populated. Run explicitly, and is
/// invoked from CI in the same block as the positive consumers:
///   Tests --treenode-filter "/*/*/InvalidLayerNameTests/*"
/// </summary>
public class InvalidLayerNameTests
{
    [Test]
    [Explicit]
    public async Task Unknown_layer_name_fails_the_build_with_a_clear_error()
    {
        var root = Path.GetFullPath(Path.Combine(ProjectFiles.SolutionDirectory, "../"));
        var project = Path.Combine(root, "IntegrationTests", "InvalidConsumer", "InvalidConsumer.csproj");
        await Assert.That(File.Exists(project))
            .IsTrue()
            .Because($"InvalidConsumer.csproj should exist at {project}");

        // Drive the failing build. --configuration Release matches what CI builds the positive
        // consumers with, so the same restored package set is reused. Clean obj/bin first so the build
        // actually runs the targets (an incremental no-op would skip _MapBundleFilterLayers entirely
        // and the test would pass vacuously).
        var consumerDirectory = Path.GetDirectoryName(project)!;
        DeleteIfPresent(Path.Combine(consumerDirectory, "bin"));
        DeleteIfPresent(Path.Combine(consumerDirectory, "obj"));

        var info = new ProcessStartInfo("dotnet", $"build \"{project}\" --configuration Release")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        using var process = Process.Start(info)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await stdoutTask) + (await stderrTask);

        // Build must have failed. If it succeeded, the validation isn't firing and consumers with
        // typos will ship empty maps/ folders.
        await Assert.That(process.ExitCode)
            .IsNotEqualTo(0)
            .Because($"Build of InvalidConsumer should have failed but exited 0. Output:\n{output}");

        // Error must mention the validation phrase the target emits — proves the failure is OUR error
        // (not, say, a NuGet restore failure or some other build break that would let a real typo slip
        // through unnoticed).
        await Assert.That(output)
            .Contains("unknown layer name(s)")
            .Because($"Build failed but not with the MapBundle layer-name validation error. Output:\n{output}");

        // Both typos must be reported (not just the first), so a user fixing one and re-running isn't
        // surprised by the second.
        await Assert.That(output).Contains("boders").Because(output);
        await Assert.That(output).Contains("citties").Because(output);
    }

    static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
