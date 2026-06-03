using MapBundle.Build;

// Unit tests for the pure logic behind the FilterMapBundleLayers MSBuild task (LayerFilter). The task
// glue itself is exercised end-to-end by the FilteredConsumer integration project and the
// InvalidLayerNameTests negative integration test. Splitting it this way means parser/edge-case
// coverage doesn't have to pay the cost of spinning up dotnet build.
public class LayerFilterTests
{
    // -------------------------- Resolve --------------------------

    [Test]
    public async Task Resolve_null_or_whitespace_returns_empty_set()
    {
        await Assert.That(LayerFilter.Resolve(null, "P")).IsEmpty();
        await Assert.That(LayerFilter.Resolve("", "P")).IsEmpty();
        await Assert.That(LayerFilter.Resolve("   ", "P")).IsEmpty();
    }

    [Test]
    public async Task Resolve_accepts_enum_names()
    {
        var resolved = LayerFilter.Resolve("Borders;StatesProvinces;Cities", "MapBundleLayers");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders", "states", "cities" });
    }

    [Test]
    public async Task Resolve_accepts_on_disk_filenames()
    {
        var resolved = LayerFilter.Resolve("borders;states;cities", "MapBundleLayers");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders", "states", "cities" });
    }

    [Test]
    public async Task Resolve_is_case_insensitive()
    {
        // The on-disk filenames are lowercase; resolve has to accept any case mix so a consumer who
        // copy-pastes from the C# MapLayer enum (PascalCase) isn't surprised.
        var resolved = LayerFilter.Resolve("BORDERS;statesprovinces;CiTiEs", "MapBundleLayers");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders", "states", "cities" });
    }

    [Test]
    public async Task Resolve_translates_StatesProvinces_to_states()
    {
        // This is the ONE enum-name → on-disk-filename mismatch. Both forms have to resolve to the same
        // canonical filename so the keep/drop decision in ShouldKeep uses a single name space.
        await Assert.That(LayerFilter.Resolve("StatesProvinces", "P")).IsEquivalentTo(new[] { "states" });
        await Assert.That(LayerFilter.Resolve("states", "P")).IsEquivalentTo(new[] { "states" });
    }

    [Test]
    public async Task Resolve_accepts_both_semicolon_and_comma_separators()
    {
        var semi = LayerFilter.Resolve("Borders;Cities", "P");
        var comma = LayerFilter.Resolve("Borders,Cities", "P");
        var mixed = LayerFilter.Resolve("Borders,Cities;Rivers", "P");
        await Assert.That(semi).IsEquivalentTo(new[] { "borders", "cities" });
        await Assert.That(comma).IsEquivalentTo(new[] { "borders", "cities" });
        await Assert.That(mixed).IsEquivalentTo(new[] { "borders", "cities", "rivers" });
    }

    [Test]
    public async Task Resolve_strips_whitespace_around_tokens()
    {
        // 'Borders, Cities' (with a space after the comma) is the natural way to write a list — the
        // parser has to handle it without forcing the consumer to omit whitespace.
        var resolved = LayerFilter.Resolve(" Borders , Cities ", "P");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders", "cities" });
    }

    [Test]
    public async Task Resolve_drops_empty_tokens()
    {
        // ;;Borders;; happens when a consumer concatenates a list defensively. Empty tokens are not an
        // error (no information was lost) and should be silently dropped.
        var resolved = LayerFilter.Resolve(";;Borders;;Cities;;", "P");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders", "cities" });
    }

    [Test]
    public async Task Resolve_deduplicates()
    {
        // Borders;borders;BORDERS is the same name three times. The resulting set has one entry.
        var resolved = LayerFilter.Resolve("Borders;borders;BORDERS", "P");
        await Assert.That(resolved).IsEquivalentTo(new[] { "borders" });
    }

    [Test]
    public async Task Resolve_throws_on_unknown_layer_name()
    {
        // The critical safety check: a typo must throw, not silently succeed with an empty set (which
        // would then quietly empty the consumer's maps/<Region> folder).
        var message = CaptureMessage(() => LayerFilter.Resolve("Boders", "MapBundleLayers"));
        await Assert.That(message).Contains("MapBundleLayers");
        await Assert.That(message).Contains("Boders");
        await Assert.That(message).Contains("unknown layer name(s)");
        // The error message should enumerate the valid names so the user can self-correct without
        // chasing down docs.
        await Assert.That(message).Contains("Borders");
        await Assert.That(message).Contains("StatesProvinces");
    }

    [Test]
    public async Task Resolve_reports_every_unknown_token_at_once()
    {
        // A consumer correcting their list shouldn't have to fix one typo, rebuild, fix the next.
        var message = CaptureMessage(() => LayerFilter.Resolve("Boders;Citties;Lkes", "P"));
        await Assert.That(message).Contains("Boders");
        await Assert.That(message).Contains("Citties");
        await Assert.That(message).Contains("Lkes");
    }

    [Test]
    public async Task Resolve_uses_the_supplied_property_name_in_the_error()
    {
        // The same parser handles both MapBundleLayers and MapBundleExcludeLayers; the error needs to
        // tell the user which one is wrong.
        var include = CaptureMessage(() => LayerFilter.Resolve("foo", "MapBundleLayers"));
        var exclude = CaptureMessage(() => LayerFilter.Resolve("foo", "MapBundleExcludeLayers"));
        await Assert.That(include).Contains("MapBundleLayers ");
        await Assert.That(exclude).Contains("MapBundleExcludeLayers");
    }

    // The codebase's existing exception-assertion pattern (`Assert.That(action).Throws<T>()`) doesn't
    // surface the message for further assertions. Captures it locally so the message-content tests
    // above read naturally.
    static string CaptureMessage(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
        throw new("Expected an ArgumentException; none was thrown.");
    }

    [Test]
    public async Task Resolve_accepts_every_known_layer_name()
    {
        // Belt-and-braces: every entry in KnownLayerFiles must round-trip through Resolve. If a new
        // layer is added (e.g. Roads) and the alias map isn't updated, this test catches it.
        var all = string.Join(";", LayerFilter.KnownLayerFiles);
        var resolved = LayerFilter.Resolve(all, "P");
        await Assert.That(resolved).IsEquivalentTo(LayerFilter.KnownLayerFiles);
    }

    // -------------------------- ShouldKeep --------------------------

    [Test]
    public async Task ShouldKeep_non_layer_files_always_survive()
    {
        // meta.json (and any future sidecar) isn't a layer file — the whitelist's "drop everything
        // not listed" rule must not catch it, because the core needs it to enumerate available layers.
        var whitelist = new HashSet<string> { "borders" };
        var blacklist = new HashSet<string>();
        await Assert.That(LayerFilter.ShouldKeep("meta", whitelist, blacklist)).IsTrue();
        // Even with both lists empty, non-layer files are kept (the trivial case).
        await Assert.That(LayerFilter.ShouldKeep("meta", [], [])).IsTrue();
    }

    [Test]
    public async Task ShouldKeep_empty_whitelist_keeps_every_layer()
    {
        // Unset MapBundleLayers (which resolves to an empty set) means "no whitelist" — all layers
        // are allowed unless the blacklist drops them.
        await Assert.That(LayerFilter.ShouldKeep("borders", [], [])).IsTrue();
        await Assert.That(LayerFilter.ShouldKeep("cities", [], [])).IsTrue();
    }

    [Test]
    public async Task ShouldKeep_whitelist_drops_unlisted_layers()
    {
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "borders" };
        await Assert.That(LayerFilter.ShouldKeep("borders", whitelist, [])).IsTrue();
        await Assert.That(LayerFilter.ShouldKeep("cities", whitelist, [])).IsFalse();
    }

    [Test]
    public async Task ShouldKeep_blacklist_wins_over_whitelist()
    {
        // The order of application is whitelist then blacklist: a layer in both lists is dropped (the
        // blacklist always wins). This is the integration consumer's pinned scenario, restated as a
        // unit test so a future refactor can't flip the order.
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "borders", "cities" };
        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cities" };
        await Assert.That(LayerFilter.ShouldKeep("borders", whitelist, blacklist)).IsTrue();
        await Assert.That(LayerFilter.ShouldKeep("cities", whitelist, blacklist)).IsFalse();
    }

    [Test]
    public async Task ShouldKeep_is_case_insensitive_on_input_filename()
    {
        // Defensive: the on-disk filenames are lowercase, but if a caller (today the MSBuild task,
        // tomorrow whatever) passes "Borders" we still keep/drop correctly.
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "borders" };
        await Assert.That(LayerFilter.ShouldKeep("BORDERS", whitelist, [])).IsTrue();
    }

    [Test]
    public async Task ShouldKeep_tokens_are_exact_match_not_prefix_or_substring()
    {
        // A naive Contains-style check could let "land" match "lakes" (or vice versa). Confirm token
        // equality is exact: a "land" whitelist shouldn't keep "lakes", and a "lakes" blacklist
        // shouldn't drop "land".
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "land" };
        await Assert.That(LayerFilter.ShouldKeep("land", whitelist, [])).IsTrue();
        await Assert.That(LayerFilter.ShouldKeep("lakes", whitelist, [])).IsFalse();

        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lakes" };
        await Assert.That(LayerFilter.ShouldKeep("land", [], blacklist)).IsTrue();
        await Assert.That(LayerFilter.ShouldKeep("lakes", [], blacklist)).IsFalse();
    }
}
