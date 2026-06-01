<#
.SYNOPSIS
  Fails the build unless the shipped MapBundle source has the required line coverage.

  Microsoft code coverage instruments every loaded module, so this scopes to files under src/
  (excluding the test project) and de-duplicates lines that appear in multiple class entries.
#>
param(
    [string] $Report = "TestResults/unit.cobertura.xml",
    [double] $Threshold = 100
)

[xml] $coverage = Get-Content $Report

# Map "file|line" -> max hits across all <class> entries for that file.
$lineHits = @{}
foreach ($package in $coverage.coverage.packages.package)
{
    foreach ($class in $package.classes.class)
    {
        $file = $class.filename
        # Scope to the shipped library only: files under src/MapBundle/. This naturally excludes the
        # test project, the build-only Builder (src/MapBundle.Builder/), and any dependency whose
        # symbols happen to map to a 'src' path.
        if (-not $file -or $file -notmatch '[\\/]src[\\/]MapBundle[\\/]')
        {
            continue
        }

        foreach ($line in $class.lines.line)
        {
            $key = "$file|$($line.number)"
            $hits = [int] $line.hits
            if (-not $lineHits.ContainsKey($key) -or $lineHits[$key] -lt $hits)
            {
                $lineHits[$key] = $hits
            }
        }
    }
}

$covered = 0
$uncovered = @{}
foreach ($entry in $lineHits.GetEnumerator())
{
    if ($entry.Value -gt 0)
    {
        $covered++
    }
    else
    {
        $parts = $entry.Key -split '\|'
        $name = $parts[0] -replace '.*[\\/]src[\\/]', ''
        if (-not $uncovered.ContainsKey($name)) { $uncovered[$name] = @() }
        $uncovered[$name] += [int] $parts[1]
    }
}

$total = $lineHits.Count

# Zero shipped-source lines means the report is empty — not 100% coverage. This happens when the
# build that produced it used deterministic source paths (ContinuousIntegrationBuild), whose rewritten
# "/_/..." paths the coverage collector cannot map back to files. Fail loudly rather than silently
# passing a meaningless 0/0.
if ($total -eq 0)
{
    Write-Error "No shipped-source coverage found: 0 lines matched 'src/MapBundle/' in '$Report'. The report is empty — ensure the build that produced it ran with -p:DeterministicSourcePaths=false."
    exit 1
}

$percent = 100.0 * $covered / $total
"Line coverage (shipped source): {0}/{1} = {2:N2}%" -f $covered, $total, $percent

if ($percent -lt $Threshold)
{
    "Uncovered lines:"
    foreach ($name in ($uncovered.Keys | Sort-Object))
    {
        "  {0}: {1}" -f $name, (($uncovered[$name] | Sort-Object) -join ',')
    }

    Write-Error ("Coverage {0:N2}% is below the required {1}%." -f $percent, $Threshold)
    exit 1
}
