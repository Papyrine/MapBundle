# MapBundle

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/MapBundle)](https://ci.appveyor.com/project/SimonCropp/MapBundle)
[![NuGet Status](https://img.shields.io/nuget/v/MapBundle.svg?label=MapBundle)](https://www.nuget.org/packages/MapBundle/)

Bundled, offline map data for .NET apps — borders, cities, waterways and base layers — shipped as [FlatGeobuf](https://flatgeobuf.org/) inside NuGet packages. Most data is derived from [OpenStreetMap](https://www.openstreetmap.org/) under the [Open Database License (ODbL)](https://opendatacommons.org/licenses/odbl/); the cities, rivers and lakes layers come from [Natural Earth](https://www.naturalearthdata.com/) (public domain).


## Packages

| Package | Contents |
| --- | --- |
| `MapBundle` | Core runtime. Loads the bundled `.fgb` layers. No data of its own. |
| `MapBundle.World` | The whole world (every continent merged). |
| `MapBundle.[Region]` | A single continent or country (for example `MapBundle.Europe` or `MapBundle.Monaco`). |

Install the core package plus the area required:

```
dotnet add package MapBundle.Monaco
```

By default a data package copies its FlatGeobuf files into a `maps/<Region>` folder beside the application at build time; the `MapBundle` core reads them from there. When FlatGeobuf is not the desired format, the data can instead be [converted to another format and/or rendered to an image at build time](#build-time-format-conversion-and-images).


## Usage

<!-- snippet: usage -->
<a id='snippet-usage'></a>
```cs
var map = Maps.Open().Load("Monaco");

var borders = map.Borders;        // country polygons
var cities = map.Cities;          // populated places
var states = map.StatesProvinces; // admin-1 polygons
var rivers = map.Rivers;          // rivers
// also: map.Lakes, map.Coastline, map.Land, map.Ocean
```
<sup><a href='/src/Tests/Snippets.cs#L7-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-usage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Layers are read on demand and returned as GeoConvert `FeatureCollection`s (coordinates are WGS84 longitude/latitude).


## Build-time format conversion and images

FlatGeobuf is the default on-disk format, but it is not always the right fit for a consumer. Setting a few MSBuild properties (in a `.csproj`, `Directory.Build.props`, or on the command line) converts a data package's layers — and/or renders a preview image — with [GeoConvert](https://github.com/SimonCropp/GeoConvert) at build time, instead of copying the raw `.fgb`. By default the output lands at `maps/<Region>` next to the built app; set [`MapBundleOutputDirectory`](#staging-output-elsewhere) to redirect it anywhere on disk (e.g. straight into a Blazor app's `wwwroot`).

```xml
<PropertyGroup>
  <!-- Convert every layer to GeoJSON instead of copying the FlatGeobuf. -->
  <MapBundleFormat>GeoJson</MapBundleFormat>
  <!-- Also render a styled preview PNG per region (maps/<Region>/<Region>.png). -->
  <MapBundleRenderImages>true</MapBundleRenderImages>
</PropertyGroup>
```

> **Note:** conversion and image rendering run as an MSBuild task targeting `net10.0`, so they require building with a .NET 10+ SDK's MSBuild (`dotnet build` / `dotnet publish`, or Visual Studio using a .NET 10 SDK). The default raw-copy path needs no task and works under any host.

### Data options

| Property | Default | Description |
| --- | --- | --- |
| `MapBundleFormat` | `FlatGeobuf` | The vector format to emit. Any GeoConvert format: `GeoJson`, `TopoJson`, `Kml`, `Kmz`, `Gpx`, `Wkt`, `Wkb`, `Csv`, `GeoParquet`, `Shapefile`, or `FlatGeobuf` (copies verbatim). Choosing anything other than `FlatGeobuf` opts out of the `.fgb` copy. |
| `MapBundleRenderImages` | `false` | When `true`, render a stacked preview PNG per region (layers painted ocean → land → lakes → rivers → coastline → states → borders → cities). |
| `MapBundleCopyData` | `true` | When `false`, no vector data is emitted at all — useful with `MapBundleRenderImages` for an images-only output. |
| `MapBundleLayers` | (all layers) | Semicolon- or comma-separated whitelist of layer names to keep. Accepts both the `MapLayer` enum names (`Borders`, `StatesProvinces`, `Cities`, `Rivers`, `Lakes`, `Coastline`, `Land`, `Ocean`) and the on-disk filenames (`borders`, `states`, `cities`, …); case-insensitive. Non-listed layers are dropped before either the raw-copy or convert path runs, so the convert path doesn't waste work on layers about to be thrown away. The `meta.json` sidecar is always kept. An unrecognised name fails the build with a list of the valid names — a typo doesn't silently empty the output. |
| `MapBundleExcludeLayers` | (none) | Semicolon- or comma-separated blacklist, applied after `MapBundleLayers`. Same name forms accepted; same validation. Useful to keep most layers but drop a few (e.g. `<MapBundleExcludeLayers>Land;Ocean</MapBundleExcludeLayers>`). |
| `MapBundleSimplifyTolerance` | `0` (off) | Vertex-reduction tolerance applied (to both data and any preview) before writing, using GeoConvert's topology-preserving simplifier — shared edges between adjacent admin polygons (countries within `Borders`, states within `StatesProvinces`) get reduced once to bit-identical vertices on both sides, so the borders stay seamlessly joined after thinning (no hairline gaps or alpha-stacking overlaps along internal borders). A positive value turns on simplification — a perpendicular distance in degrees for `DouglasPeucker`, an effective triangle area in degrees² for `Visvalingam`. Setting it forces a read/rewrite even when `MapBundleFormat` is `FlatGeobuf`. |
| `MapBundleSimplifyMethod` | `DouglasPeucker` | The simplify algorithm: `DouglasPeucker` or `Visvalingam`. Only used when `MapBundleSimplifyTolerance` is positive. |
| `MapBundleOutputDirectory` | (unset) | Where the produced files land on disk. Unset is the historical behaviour: MapBundle writes to the project's intermediate output and auto-stages a copy via `<None Link>` at `maps/<Region>/<filename>.<ext>` in the build output. Set to redirect — typically into the consumer's source tree, e.g. `$(MSBuildProjectDirectory)\wwwroot\sample` for a Blazor WASM app — and MapBundle writes the files there directly under a `<Region>/<filename>.<ext>` subfolder, skipping the default auto-stage. See [Staging output elsewhere](#staging-output-elsewhere). |

### Image options

Only used when `MapBundleRenderImages` is `true`; each is left at GeoConvert's own default when unset.

| Property | Description |
| --- | --- |
| `MapBundleImageWidth` | Image width in pixels (default `2048`). |
| `MapBundleImageHeight` | Image height in pixels; `0` derives it from the width and aspect ratio. |
| `MapBundleImagePadding` | Empty margin around the content, in pixels. |
| `MapBundleImageProjection` | `Auto`, `PlateCarree`, `WebMercator`, `Lambert`, or `Goode`. |
| `MapBundleImageBackground` | Background color (`#RGB`, `#RRGGBB`, or `#RRGGBBAA`). |
| `MapBundleImageOcean` | Ocean (world-envelope) fill color; unset skips the ocean pass. |
| `MapBundleImageStroke` | Outline color for lines, polygon edges, and point markers. |
| `MapBundleImageFill` | Polygon fill color (typically semi-transparent). |
| `MapBundleImageStrokeWidth` | Stroke width in pixels. |
| `MapBundleImagePointRadius` | Point marker radius in pixels. |
| `MapBundleImageStrokeAutoScale` | `true` scales stroke/point size by an implicit-zoom factor. |
| `MapBundleImageMinFeaturePixels` | Render-time minimum feature size, in pixels. Polygons / lines whose projected bounding box is below this in both axes are skipped — useful for dense archipelagoes (Indonesia, Norway, Arctic Canada) that would otherwise paint thousands of sub-pixel islands as 1-px specks at world scale. `0` (default) renders everything; `1` is the "if it can't be painted cleanly, drop it" floor; `4` prunes more for thumbnails. Per-feature, so a country's mainland still renders while its small islands don't. |
| `MapBundleImageLabels` | `true` labels features that carry a `name` (borders, states, cities, rivers, lakes). |
| `MapBundleImageLabelSize` | Cap height of label text in pixels. |
| `MapBundleImageLabelColor` | Label text color. |
| `MapBundleImageCompression` | PNG deflate level: `Optimal`, `Fastest`, `SmallestSize`, or `NoCompression`. |

### Staging output elsewhere

The default staging path (`maps/<Region>/<layer>.<ext>` next to the built app) is fine for a console or desktop consumer that loads via `Maps.Open()` at runtime, but it isn't always where the consumer wants the file to land. `MapBundleOutputDirectory` lets the consumer point MapBundle at a different directory; MapBundle writes the produced files straight there, with the same `<Region>/<filename>.<ext>` layout, and skips the default `<None Link>` auto-stage (the file is already at its final destination).

The motivating use case is a Blazor WebAssembly app that wants the simplified data served as a static asset. The whole pipeline collapses to three properties — no custom MSBuild target, no `<Copy>`, no `<Exec>`:

```xml
<PropertyGroup>
  <!-- Keep only the country borders out of MapBundle.World's eight layers. -->
  <MapBundleLayers>Borders</MapBundleLayers>
  <!-- Simplify with the topology-preserving variant (the default since 0.3.0). -->
  <MapBundleSimplifyTolerance>0.1</MapBundleSimplifyTolerance>
  <!-- Write straight into wwwroot, where Blazor's static-web-assets pipeline picks it up. -->
  <MapBundleOutputDirectory>$(MSBuildProjectDirectory)\wwwroot\sample</MapBundleOutputDirectory>
</PropertyGroup>
```

The simplified borders land at `wwwroot/sample/World/borders.fgb`, served at the URL `/sample/World/borders.fgb` once Blazor's manifest catches up. For Blazor consumers MapBundle also auto-registers the produced files as `<Content>` items (with the project-relative ItemSpec Blazor's `DefineStaticWebAssets` task expects); for non-Blazor SDKs those Content items are inert. A `<Content Remove>` runs first so a rebuild — where the SDK's eval-time wwwroot glob already caught the file the previous run left behind — doesn't trip the `DiscoverPrecompressedAssets` "duplicate FullPath" throw.

Trade-offs to be aware of when redirecting the output:

- The path is exactly `<MapBundleOutputDirectory>/<Region>/<filename>.<ext>`. A single-region single-layer consumer pays for the `<Region>/` subfolder in the URL; that's the multi-region-friendly default. Flatten with the regular MSBuild file-staging tools if it matters.
- Pick an absolute path (or one rooted via `$(MSBuildProjectDirectory)`). A bare relative path will be interpreted relative to the working directory MSBuild was invoked from, which on a Visual Studio build isn't necessarily the project directory.
- Files land in the consumer's source tree if `MapBundleOutputDirectory` points there. They're regenerated on every build that finds them stale, so add the target subfolder (e.g. `wwwroot/sample/`) to `.gitignore`.


## Layers

The `MapLayer` enum (a layer is omitted from a package when the source has nothing for that region):

- **Borders** — country polygons (OSM admin level 2)
- **StatesProvinces** — state/province polygons (OSM admin level 4 / ISO 3166-2)
- **Cities** — populated places (`place=city`/`town`)
- **Rivers** — major waterways (`waterway=river`)
- **Lakes** — lake and reservoir polygons (`natural=water`, `reservoir`)
- **Coastline** — coastlines (derived from the land outlines); omitted for landlocked countries
- **Land** / **Ocean** — global base polygons. **Ocean** is omitted for landlocked countries.

Roads, railways, buildings, land use and terrain are intentionally excluded.


## Packages

Layer icons: 🗺️ Borders · 🏛️ StatesProvinces · 🏙️ Cities · 〰️ Rivers · 💧 Lakes · 🏖️ Coastline · 🟩 Land · 🌊 Ocean<!-- include: bundles.include.md -->

### World

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.World](https://www.nuget.org/packages/MapBundle.World) | 93.5 MB | 167.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 157,728 |

### Continents

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Africa](https://www.nuget.org/packages/MapBundle.Africa) | 5.4 MB | 12.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 6,864 |
| [MapBundle.Antarctica](https://www.nuget.org/packages/MapBundle.Antarctica) | 18 KB | 5 KB | 🏙️ | 40 |
| [MapBundle.Asia](https://www.nuget.org/packages/MapBundle.Asia) | 15.1 MB | 29.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 23,219 |
| [MapBundle.AustraliaOceania](https://www.nuget.org/packages/MapBundle.AustraliaOceania) | 16.8 MB | 25.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 32,773 |
| [MapBundle.CentralAmerica](https://www.nuget.org/packages/MapBundle.CentralAmerica) | 2.6 MB | 4.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,762 |
| [MapBundle.Europe](https://www.nuget.org/packages/MapBundle.Europe) | 71.3 MB | 101.9 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 141,400 |
| [MapBundle.NorthAmerica](https://www.nuget.org/packages/MapBundle.NorthAmerica) | 69.0 MB | 101.4 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 131,304 |
| [MapBundle.Russia](https://www.nuget.org/packages/MapBundle.Russia) | 45.7 MB | 62.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 94,267 |
| [MapBundle.SouthAmerica](https://www.nuget.org/packages/MapBundle.SouthAmerica) | 8.2 MB | 15.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 12,256 |

### Countries

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Afghanistan](https://www.nuget.org/packages/MapBundle.Afghanistan) | 114 KB | 282 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 81 |
| [MapBundle.Albania](https://www.nuget.org/packages/MapBundle.Albania) | 64 KB | 100 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 69 |
| [MapBundle.Algeria](https://www.nuget.org/packages/MapBundle.Algeria) | 259 KB | 426 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 203 |
| [MapBundle.AmericanOceania](https://www.nuget.org/packages/MapBundle.AmericanOceania) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Andorra](https://www.nuget.org/packages/MapBundle.Andorra) | 20 KB | 7 KB | 🗺️ 🏙️ 🏛️ | 9 |
| [MapBundle.Angola](https://www.nuget.org/packages/MapBundle.Angola) | 151 KB | 337 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 101 |
| [MapBundle.Argentina](https://www.nuget.org/packages/MapBundle.Argentina) | 2.2 MB | 3.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 3,851 |
| [MapBundle.Armenia](https://www.nuget.org/packages/MapBundle.Armenia) | 40 KB | 64 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 27 |
| [MapBundle.Australia](https://www.nuget.org/packages/MapBundle.Australia) | 3.3 MB | 5.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,831 |
| [MapBundle.Austria](https://www.nuget.org/packages/MapBundle.Austria) | 58 KB | 123 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 29 |
| [MapBundle.Azerbaijan](https://www.nuget.org/packages/MapBundle.Azerbaijan) | 102 KB | 200 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 137 |
| [MapBundle.Bahamas](https://www.nuget.org/packages/MapBundle.Bahamas) | 744 KB | 1.2 MB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 1,825 |
| [MapBundle.Bangladesh](https://www.nuget.org/packages/MapBundle.Bangladesh) | 346 KB | 660 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 635 |
| [MapBundle.Belarus](https://www.nuget.org/packages/MapBundle.Belarus) | 68 KB | 150 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 33 |
| [MapBundle.Belgium](https://www.nuget.org/packages/MapBundle.Belgium) | 83 KB | 127 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 43 |
| [MapBundle.Belize](https://www.nuget.org/packages/MapBundle.Belize) | 101 KB | 154 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 161 |
| [MapBundle.Benin](https://www.nuget.org/packages/MapBundle.Benin) | 44 KB | 76 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 31 |
| [MapBundle.Bhutan](https://www.nuget.org/packages/MapBundle.Bhutan) | 34 KB | 51 KB | 🗺️ 🏙️ 〰️ 🏛️ | 26 |
| [MapBundle.Bolivia](https://www.nuget.org/packages/MapBundle.Bolivia) | 114 KB | 283 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 103 |
| [MapBundle.BosniaHerzegovina](https://www.nuget.org/packages/MapBundle.BosniaHerzegovina) | 134 KB | 196 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 183 |
| [MapBundle.Botswana](https://www.nuget.org/packages/MapBundle.Botswana) | 65 KB | 136 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 53 |
| [MapBundle.Brazil](https://www.nuget.org/packages/MapBundle.Brazil) | 1.4 MB | 2.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,602 |
| [MapBundle.Bulgaria](https://www.nuget.org/packages/MapBundle.Bulgaria) | 77 KB | 145 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.BurkinaFaso](https://www.nuget.org/packages/MapBundle.BurkinaFaso) | 81 KB | 215 KB | 🗺️ 🏙️ 〰️ 🏛️ | 99 |
| [MapBundle.Burundi](https://www.nuget.org/packages/MapBundle.Burundi) | 42 KB | 80 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 39 |
| [MapBundle.Cambodia](https://www.nuget.org/packages/MapBundle.Cambodia) | 122 KB | 228 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 179 |
| [MapBundle.Cameroon](https://www.nuget.org/packages/MapBundle.Cameroon) | 119 KB | 267 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 87 |
| [MapBundle.Canada](https://www.nuget.org/packages/MapBundle.Canada) | 23.9 MB | 37.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 43,818 |
| [MapBundle.CapeVerde](https://www.nuget.org/packages/MapBundle.CapeVerde) | 63 KB | 84 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.CentralAfricanRepublic](https://www.nuget.org/packages/MapBundle.CentralAfricanRepublic) | 97 KB | 239 KB | 🗺️ 🏙️ 〰️ 🏛️ | 59 |
| [MapBundle.Chad](https://www.nuget.org/packages/MapBundle.Chad) | 69 KB | 149 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 55 |
| [MapBundle.Chile](https://www.nuget.org/packages/MapBundle.Chile) | 4.4 MB | 7.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 7,276 |
| [MapBundle.China](https://www.nuget.org/packages/MapBundle.China) | 4.9 MB | 7.8 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 8,841 |
| [MapBundle.Colombia](https://www.nuget.org/packages/MapBundle.Colombia) | 695 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 760 |
| [MapBundle.CongoDemocraticRepublic](https://www.nuget.org/packages/MapBundle.CongoDemocraticRepublic) | 282 KB | 711 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 201 |
| [MapBundle.CongoBrazzaville](https://www.nuget.org/packages/MapBundle.CongoBrazzaville) | 85 KB | 188 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.CookIslands](https://www.nuget.org/packages/MapBundle.CookIslands) | 33 KB | 34 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 92 |
| [MapBundle.CostaRica](https://www.nuget.org/packages/MapBundle.CostaRica) | 102 KB | 155 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 80 |
| [MapBundle.Croatia](https://www.nuget.org/packages/MapBundle.Croatia) | 357 KB | 618 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 448 |
| [MapBundle.Cuba](https://www.nuget.org/packages/MapBundle.Cuba) | 540 KB | 971 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 1,132 |
| [MapBundle.Cyprus](https://www.nuget.org/packages/MapBundle.Cyprus) | 48 KB | 57 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 15 |
| [MapBundle.CzechRepublic](https://www.nuget.org/packages/MapBundle.CzechRepublic) | 67 KB | 177 KB | 🗺️ 🏙️ 〰️ 🏛️ | 111 |
| [MapBundle.Denmark](https://www.nuget.org/packages/MapBundle.Denmark) | 490 KB | 745 KB | 🗺️ 🏙️ 💧 🏛️ 🏖️ 🟩 🌊 | 619 |
| [MapBundle.Djibouti](https://www.nuget.org/packages/MapBundle.Djibouti) | 50 KB | 81 KB | 🗺️ 🏙️ 💧 🏛️ 🏖️ 🟩 🌊 | 31 |
| [MapBundle.EastTimor](https://www.nuget.org/packages/MapBundle.EastTimor) | 56 KB | 70 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 48 |
| [MapBundle.Ecuador](https://www.nuget.org/packages/MapBundle.Ecuador) | 323 KB | 564 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 390 |
| [MapBundle.Egypt](https://www.nuget.org/packages/MapBundle.Egypt) | 278 KB | 556 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 271 |
| [MapBundle.ElSalvador](https://www.nuget.org/packages/MapBundle.ElSalvador) | 87 KB | 120 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 96 |
| [MapBundle.EquatorialGuinea](https://www.nuget.org/packages/MapBundle.EquatorialGuinea) | 67 KB | 91 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Eritrea](https://www.nuget.org/packages/MapBundle.Eritrea) | 220 KB | 371 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 534 |
| [MapBundle.Estonia](https://www.nuget.org/packages/MapBundle.Estonia) | 234 KB | 343 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 247 |
| [MapBundle.Ethiopia](https://www.nuget.org/packages/MapBundle.Ethiopia) | 166 KB | 303 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 218 |
| [MapBundle.FaroeIslands](https://www.nuget.org/packages/MapBundle.FaroeIslands) | 77 KB | 88 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 47 |
| [MapBundle.Fiji](https://www.nuget.org/packages/MapBundle.Fiji) | 1.9 MB | 2.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 3,963 |
| [MapBundle.Finland](https://www.nuget.org/packages/MapBundle.Finland) | 2.6 MB | 4.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 9,587 |
| [MapBundle.France](https://www.nuget.org/packages/MapBundle.France) | 27.2 MB | 36.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 48,480 |
| [MapBundle.GccStates](https://www.nuget.org/packages/MapBundle.GccStates) | 600 KB | 937 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 788 |
| [MapBundle.Gabon](https://www.nuget.org/packages/MapBundle.Gabon) | 82 KB | 159 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Georgia](https://www.nuget.org/packages/MapBundle.Georgia) | 56 KB | 100 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 29 |
| [MapBundle.Germany](https://www.nuget.org/packages/MapBundle.Germany) | 387 KB | 655 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 445 |
| [MapBundle.Ghana](https://www.nuget.org/packages/MapBundle.Ghana) | 85 KB | 155 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 45 |
| [MapBundle.Greece](https://www.nuget.org/packages/MapBundle.Greece) | 918 KB | 1.5 MB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 961 |
| [MapBundle.Greenland](https://www.nuget.org/packages/MapBundle.Greenland) | 9.1 MB | 13.4 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 17,456 |
| [MapBundle.Guatemala](https://www.nuget.org/packages/MapBundle.Guatemala) | 68 KB | 104 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 85 |
| [MapBundle.Guinea](https://www.nuget.org/packages/MapBundle.Guinea) | 196 KB | 385 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 242 |
| [MapBundle.GuineaBissau](https://www.nuget.org/packages/MapBundle.GuineaBissau) | 175 KB | 273 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 199 |
| [MapBundle.Guyana](https://www.nuget.org/packages/MapBundle.Guyana) | 69 KB | 139 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 35 |
| [MapBundle.HaitiAndDomrep](https://www.nuget.org/packages/MapBundle.HaitiAndDomrep) | 190 KB | 402 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 145 |
| [MapBundle.Honduras](https://www.nuget.org/packages/MapBundle.Honduras) | 222 KB | 338 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 267 |
| [MapBundle.Hungary](https://www.nuget.org/packages/MapBundle.Hungary) | 56 KB | 114 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 58 |
| [MapBundle.Iceland](https://www.nuget.org/packages/MapBundle.Iceland) | 461 KB | 693 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 516 |
| [MapBundle.India](https://www.nuget.org/packages/MapBundle.India) | 1.2 MB | 2.4 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,103 |
| [MapBundle.Indonesia](https://www.nuget.org/packages/MapBundle.Indonesia) | 3.6 MB | 6.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 6,052 |
| [MapBundle.Iran](https://www.nuget.org/packages/MapBundle.Iran) | 544 KB | 892 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 575 |
| [MapBundle.Iraq](https://www.nuget.org/packages/MapBundle.Iraq) | 95 KB | 179 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 91 |
| [MapBundle.IrelandAndNorthernIreland](https://www.nuget.org/packages/MapBundle.IrelandAndNorthernIreland) | 490 KB | 786 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 531 |
| [MapBundle.IsraelAndPalestine](https://www.nuget.org/packages/MapBundle.IsraelAndPalestine) | 47 KB | 76 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 43 |
| [MapBundle.Italy](https://www.nuget.org/packages/MapBundle.Italy) | 767 KB | 1.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 818 |
| [MapBundle.IvoryCoast](https://www.nuget.org/packages/MapBundle.IvoryCoast) | 93 KB | 215 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.Jamaica](https://www.nuget.org/packages/MapBundle.Jamaica) | 53 KB | 68 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 34 |
| [MapBundle.Japan](https://www.nuget.org/packages/MapBundle.Japan) | 2.1 MB | 3.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,956 |
| [MapBundle.Jordan](https://www.nuget.org/packages/MapBundle.Jordan) | 38 KB | 47 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 32 |
| [MapBundle.Kazakhstan](https://www.nuget.org/packages/MapBundle.Kazakhstan) | 378 KB | 703 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 489 |
| [MapBundle.Kenya](https://www.nuget.org/packages/MapBundle.Kenya) | 143 KB | 286 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 171 |
| [MapBundle.Kiribati](https://www.nuget.org/packages/MapBundle.Kiribati) | 4.7 MB | 6.2 MB | 🗺️ 🏙️ 〰️ 💧 🏖️ 🟩 🌊 | 10,349 |
| [MapBundle.Kyrgyzstan](https://www.nuget.org/packages/MapBundle.Kyrgyzstan) | 76 KB | 167 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 35 |
| [MapBundle.Laos](https://www.nuget.org/packages/MapBundle.Laos) | 195 KB | 378 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 288 |
| [MapBundle.Latvia](https://www.nuget.org/packages/MapBundle.Latvia) | 77 KB | 146 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 141 |
| [MapBundle.Lebanon](https://www.nuget.org/packages/MapBundle.Lebanon) | 39 KB | 46 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 21 |
| [MapBundle.Lesotho](https://www.nuget.org/packages/MapBundle.Lesotho) | 37 KB | 57 KB | 🗺️ 🏙️ 〰️ 🏛️ | 22 |
| [MapBundle.Liberia](https://www.nuget.org/packages/MapBundle.Liberia) | 61 KB | 119 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 39 |
| [MapBundle.Libya](https://www.nuget.org/packages/MapBundle.Libya) | 96 KB | 164 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 73 |
| [MapBundle.Liechtenstein](https://www.nuget.org/packages/MapBundle.Liechtenstein) | 20 KB | 8 KB | 🗺️ 🏙️ 〰️ 🏛️ | 14 |
| [MapBundle.Lithuania](https://www.nuget.org/packages/MapBundle.Lithuania) | 65 KB | 140 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 88 |
| [MapBundle.Luxembourg](https://www.nuget.org/packages/MapBundle.Luxembourg) | 25 KB | 20 KB | 🗺️ 🏙️ 〰️ 🏛️ | 17 |
| [MapBundle.Macedonia](https://www.nuget.org/packages/MapBundle.Macedonia) | 38 KB | 70 KB | 🗺️ 🏙️ 〰️ 🏛️ | 78 |
| [MapBundle.Madagascar](https://www.nuget.org/packages/MapBundle.Madagascar) | 311 KB | 592 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 325 |
| [MapBundle.Malawi](https://www.nuget.org/packages/MapBundle.Malawi) | 83 KB | 209 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 61 |
| [MapBundle.MalaysiaSingaporeBrunei](https://www.nuget.org/packages/MapBundle.MalaysiaSingaporeBrunei) | 517 KB | 892 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 1,185 |
| [MapBundle.Maldives](https://www.nuget.org/packages/MapBundle.Maldives) | 100 KB | 220 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 465 |
| [MapBundle.Mali](https://www.nuget.org/packages/MapBundle.Mali) | 94 KB | 224 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 57 |
| [MapBundle.Malta](https://www.nuget.org/packages/MapBundle.Malta) | 33 KB | 34 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 79 |
| [MapBundle.MarshallIslands](https://www.nuget.org/packages/MapBundle.MarshallIslands) | 76 KB | 140 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 319 |
| [MapBundle.Mauritania](https://www.nuget.org/packages/MapBundle.Mauritania) | 85 KB | 140 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 76 |
| [MapBundle.Mauritius](https://www.nuget.org/packages/MapBundle.Mauritius) | 44 KB | 52 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 72 |
| [MapBundle.Mexico](https://www.nuget.org/packages/MapBundle.Mexico) | 1.5 MB | 2.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,388 |
| [MapBundle.Micronesia](https://www.nuget.org/packages/MapBundle.Micronesia) | 67 KB | 104 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 229 |
| [MapBundle.Moldova](https://www.nuget.org/packages/MapBundle.Moldova) | 49 KB | 93 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 49 |
| [MapBundle.Monaco](https://www.nuget.org/packages/MapBundle.Monaco) | 18 KB | 2 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 5 |
| [MapBundle.Mongolia](https://www.nuget.org/packages/MapBundle.Mongolia) | 121 KB | 264 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 93 |
| [MapBundle.Montenegro](https://www.nuget.org/packages/MapBundle.Montenegro) | 48 KB | 71 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 36 |
| [MapBundle.Morocco](https://www.nuget.org/packages/MapBundle.Morocco) | 190 KB | 322 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 137 |
| [MapBundle.Mozambique](https://www.nuget.org/packages/MapBundle.Mozambique) | 219 KB | 420 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 194 |
| [MapBundle.Myanmar](https://www.nuget.org/packages/MapBundle.Myanmar) | 837 KB | 1.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,621 |
| [MapBundle.Namibia](https://www.nuget.org/packages/MapBundle.Namibia) | 100 KB | 198 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 73 |
| [MapBundle.Nauru](https://www.nuget.org/packages/MapBundle.Nauru) | 19 KB | 5 KB | 🗺️ 🏛️ 🏖️ 🟩 🌊 | 18 |
| [MapBundle.Nepal](https://www.nuget.org/packages/MapBundle.Nepal) | 53 KB | 107 KB | 🗺️ 🏙️ 〰️ | 24 |
| [MapBundle.Netherlands](https://www.nuget.org/packages/MapBundle.Netherlands) | 3.8 MB | 4.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 5,090 |
| [MapBundle.NewCaledonia](https://www.nuget.org/packages/MapBundle.NewCaledonia) | 157 KB | 250 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 200 |
| [MapBundle.NewZealand](https://www.nuget.org/packages/MapBundle.NewZealand) | 2.2 MB | 3.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,046 |
| [MapBundle.Nicaragua](https://www.nuget.org/packages/MapBundle.Nicaragua) | 182 KB | 266 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 189 |
| [MapBundle.Niger](https://www.nuget.org/packages/MapBundle.Niger) | 50 KB | 90 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 34 |
| [MapBundle.Nigeria](https://www.nuget.org/packages/MapBundle.Nigeria) | 147 KB | 367 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 154 |
| [MapBundle.Niue](https://www.nuget.org/packages/MapBundle.Niue) | 19 KB | 4 KB | 🗺️ 🏖️ 🟩 🌊 | 4 |
| [MapBundle.NorthKorea](https://www.nuget.org/packages/MapBundle.NorthKorea) | 220 KB | 372 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 210 |
| [MapBundle.Norway](https://www.nuget.org/packages/MapBundle.Norway) | 13.7 MB | 19.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 30,844 |
| [MapBundle.Pakistan](https://www.nuget.org/packages/MapBundle.Pakistan) | 244 KB | 509 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 160 |
| [MapBundle.Palau](https://www.nuget.org/packages/MapBundle.Palau) | 50 KB | 69 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 94 |
| [MapBundle.Panama](https://www.nuget.org/packages/MapBundle.Panama) | 269 KB | 455 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 261 |
| [MapBundle.PapuaNewGuinea](https://www.nuget.org/packages/MapBundle.PapuaNewGuinea) | 1.0 MB | 1.8 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,090 |
| [MapBundle.Paraguay](https://www.nuget.org/packages/MapBundle.Paraguay) | 78 KB | 174 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 68 |
| [MapBundle.Peru](https://www.nuget.org/packages/MapBundle.Peru) | 395 KB | 765 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 406 |
| [MapBundle.Philippines](https://www.nuget.org/packages/MapBundle.Philippines) | 1.4 MB | 2.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,186 |
| [MapBundle.PitcairnIslands](https://www.nuget.org/packages/MapBundle.PitcairnIslands) | 76 KB | 140 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 319 |
| [MapBundle.Poland](https://www.nuget.org/packages/MapBundle.Poland) | 140 KB | 263 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 155 |
| [MapBundle.PolynesieFrancaise](https://www.nuget.org/packages/MapBundle.PolynesieFrancaise) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Portugal](https://www.nuget.org/packages/MapBundle.Portugal) | 198 KB | 357 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 177 |
| [MapBundle.Romania](https://www.nuget.org/packages/MapBundle.Romania) | 101 KB | 212 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 108 |
| [MapBundle.Rwanda](https://www.nuget.org/packages/MapBundle.Rwanda) | 39 KB | 60 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 22 |
| [MapBundle.SaintHelenaAscensionAndTristanDaCunha](https://www.nuget.org/packages/MapBundle.SaintHelenaAscensionAndTristanDaCunha) | 29 KB | 25 KB | 🗺️ 🏛️ 🏖️ 🟩 🌊 | 72 |
| [MapBundle.Samoa](https://www.nuget.org/packages/MapBundle.Samoa) | 35 KB | 32 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 18 |
| [MapBundle.SaoTomeAndPrincipe](https://www.nuget.org/packages/MapBundle.SaoTomeAndPrincipe) | 31 KB | 23 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 15 |
| [MapBundle.SenegalAndGambia](https://www.nuget.org/packages/MapBundle.SenegalAndGambia) | 79 KB | 156 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 77 |
| [MapBundle.Serbia](https://www.nuget.org/packages/MapBundle.Serbia) | 58 KB | 123 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 50 |
| [MapBundle.Seychelles](https://www.nuget.org/packages/MapBundle.Seychelles) | 49 KB | 63 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 125 |
| [MapBundle.SierraLeone](https://www.nuget.org/packages/MapBundle.SierraLeone) | 69 KB | 114 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 94 |
| [MapBundle.Slovakia](https://www.nuget.org/packages/MapBundle.Slovakia) | 41 KB | 72 KB | 🗺️ 🏙️ 〰️ 🏛️ | 20 |
| [MapBundle.Slovenia](https://www.nuget.org/packages/MapBundle.Slovenia) | 60 KB | 126 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 223 |
| [MapBundle.SolomonIslands](https://www.nuget.org/packages/MapBundle.SolomonIslands) | 390 KB | 705 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 727 |
| [MapBundle.Somalia](https://www.nuget.org/packages/MapBundle.Somalia) | 111 KB | 180 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 109 |
| [MapBundle.SouthAfrica](https://www.nuget.org/packages/MapBundle.SouthAfrica) | 238 KB | 476 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 218 |
| [MapBundle.SouthKorea](https://www.nuget.org/packages/MapBundle.SouthKorea) | 753 KB | 1.2 MB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 1,292 |
| [MapBundle.SouthSudan](https://www.nuget.org/packages/MapBundle.SouthSudan) | 78 KB | 181 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 46 |
| [MapBundle.Spain](https://www.nuget.org/packages/MapBundle.Spain) | 650 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 354 |
| [MapBundle.SriLanka](https://www.nuget.org/packages/MapBundle.SriLanka) | 95 KB | 170 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 126 |
| [MapBundle.Sudan](https://www.nuget.org/packages/MapBundle.Sudan) | 126 KB | 252 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 121 |
| [MapBundle.Suriname](https://www.nuget.org/packages/MapBundle.Suriname) | 52 KB | 83 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 33 |
| [MapBundle.Swaziland](https://www.nuget.org/packages/MapBundle.Swaziland) | 21 KB | 10 KB | 🗺️ 🏙️ 〰️ 🏛️ | 13 |
| [MapBundle.Sweden](https://www.nuget.org/packages/MapBundle.Sweden) | 4.7 MB | 7.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 17,038 |
| [MapBundle.Switzerland](https://www.nuget.org/packages/MapBundle.Switzerland) | 53 KB | 109 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 67 |
| [MapBundle.Syria](https://www.nuget.org/packages/MapBundle.Syria) | 64 KB | 101 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 51 |
| [MapBundle.Taiwan](https://www.nuget.org/packages/MapBundle.Taiwan) | 221 KB | 310 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 335 |
| [MapBundle.Tajikistan](https://www.nuget.org/packages/MapBundle.Tajikistan) | 67 KB | 144 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 25 |
| [MapBundle.Tanzania](https://www.nuget.org/packages/MapBundle.Tanzania) | 267 KB | 596 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 249 |
| [MapBundle.Thailand](https://www.nuget.org/packages/MapBundle.Thailand) | 601 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,347 |
| [MapBundle.Togo](https://www.nuget.org/packages/MapBundle.Togo) | 50 KB | 80 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 21 |
| [MapBundle.Tokelau](https://www.nuget.org/packages/MapBundle.Tokelau) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Tonga](https://www.nuget.org/packages/MapBundle.Tonga) | 53 KB | 73 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 146 |
| [MapBundle.Tunisia](https://www.nuget.org/packages/MapBundle.Tunisia) | 124 KB | 220 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 108 |
| [MapBundle.Turkey](https://www.nuget.org/packages/MapBundle.Turkey) | 583 KB | 1.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 634 |
| [MapBundle.Turkmenistan](https://www.nuget.org/packages/MapBundle.Turkmenistan) | 104 KB | 171 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Tuvalu](https://www.nuget.org/packages/MapBundle.Tuvalu) | 28 KB | 24 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 48 |
| [MapBundle.Uganda](https://www.nuget.org/packages/MapBundle.Uganda) | 130 KB | 337 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 175 |
| [MapBundle.Ukraine](https://www.nuget.org/packages/MapBundle.Ukraine) | 291 KB | 545 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 229 |
| [MapBundle.UnitedKingdom](https://www.nuget.org/packages/MapBundle.UnitedKingdom) | 2.0 MB | 4.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,677 |
| [MapBundle.Us](https://www.nuget.org/packages/MapBundle.Us) | 54.5 MB | 74.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 113,970 |
| [MapBundle.Uruguay](https://www.nuget.org/packages/MapBundle.Uruguay) | 84 KB | 154 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 78 |
| [MapBundle.Uzbekistan](https://www.nuget.org/packages/MapBundle.Uzbekistan) | 89 KB | 194 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 61 |
| [MapBundle.Vanuatu](https://www.nuget.org/packages/MapBundle.Vanuatu) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Venezuela](https://www.nuget.org/packages/MapBundle.Venezuela) | 357 KB | 709 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 374 |
| [MapBundle.Vietnam](https://www.nuget.org/packages/MapBundle.Vietnam) | 654 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,075 |
| [MapBundle.WallisEtFutuna](https://www.nuget.org/packages/MapBundle.WallisEtFutuna) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Yemen](https://www.nuget.org/packages/MapBundle.Yemen) | 181 KB | 297 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 307 |
| [MapBundle.Zambia](https://www.nuget.org/packages/MapBundle.Zambia) | 121 KB | 281 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 75 |
| [MapBundle.Zimbabwe](https://www.nuget.org/packages/MapBundle.Zimbabwe) | 79 KB | 175 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ | 44 |
| [MapBundle.UsPuertoRico](https://www.nuget.org/packages/MapBundle.UsPuertoRico) | 59 KB | 72 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 37 |
| [MapBundle.UsUsVirginIslands](https://www.nuget.org/packages/MapBundle.UsUsVirginIslands) | 38 KB | 36 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 44 |
| [MapBundle.IleDeClipperton](https://www.nuget.org/packages/MapBundle.IleDeClipperton) | 129 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
<!-- endInclude -->


## Data sources

- **Borders** and **StatesProvinces** come from [country-levels](https://github.com/hyperknot/country-levels) — OSM-derived, pre-simplified WGS84 boundaries keyed by ISO code.
- **Cities**, **Rivers** and **Lakes** come from [Natural Earth](https://www.naturalearthdata.com/) (public domain, 1:10m) via the [nvkelso/natural-earth-vector](https://github.com/nvkelso/natural-earth-vector) mirror. Cities are selected per region by ISO code; rivers and lakes are clipped to the region's bounding box.
- **Land** and **Ocean** come from [osmdata.openstreetmap.de](https://osmdata.openstreetmap.de/); **Coastline** is derived from the land polygons.


## Regions

The region tree follows [Geofabrik's download index](https://download.geofabrik.de/index-v1.json): the continents and their countries. `MapBundle.World` merges every continent. Sub-country levels (US states, German Bundesländer) are not published. See `src/Tests/Builder/Regions.cs`.


## Building the data packages

The builder lives in the test project (`src/Tests/Builder/`) and runs as an explicit test. It downloads the source data (cached locally by [Replicant](https://github.com/SimonCropp/Replicant)), filters and simplifies each region, exports FlatGeobuf and writes the `.nupkg` files into `nugets/`:

```
src/Tests/bin/Release/net10.0/Tests --treenode-filter "/*/*/PackageBuilder/Generate"
```

To validate the pipeline on a single region (default `monaco`) without building the whole tree:

```
MAPBUNDLE_SLICE=monaco src/Tests/bin/Debug/net10.0/Tests --treenode-filter "/*/*/PackageBuilder/Slice"
```

Geometry simplification and EPSG:3857→4326 reprojection use [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite), a **build-only** dependency; the shipped `MapBundle` core depends only on GeoConvert.


## License

The `MapBundle` core library is MIT.

The data packages are licensed under the [ODbL](https://opendatacommons.org/licenses/odbl/), reflecting their OpenStreetMap-derived content:

- **Borders** and **StatesProvinces** (via [country-levels](https://github.com/hyperknot/country-levels)) and **Land**, **Ocean** and **Coastline** (via [osmdata](https://osmdata.openstreetmap.de/)) are © OpenStreetMap contributors, ODbL.
- **Cities**, **Rivers** and **Lakes** are made with [Natural Earth](https://www.naturalearthdata.com/) (public domain).
