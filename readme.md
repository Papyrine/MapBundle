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


## Converting and rendering in code

Because layers come back as GeoConvert `FeatureCollection`s — and GeoConvert is a public dependency of the core package — the same data can be written to another format or rasterised to an image directly in code, with no extra dependency. This is the in-process counterpart to the [build-time MSBuild properties](#build-time-format-conversion-and-images) below.

<!-- snippet: convert -->
<a id='snippet-convert'></a>
```cs
var map = Maps.Open().Load("Monaco");

// Layers come back as GeoConvert FeatureCollections, so GeoConvert can write
// them out in another format or rasterise them directly — no extra dependency.
var borders = map.Load(MapLayer.Borders);

// Convert the layer to GeoJSON (any GeoFormat works: Kml, TopoJson, Shapefile, …).
GeoConverter.Write(borders, "borders.geojson", GeoFormat.GeoJson);

// Render the layer to a PNG (pass several collections to stack them bottom-up).
MapRenderer.RenderPng([borders], "borders.png", new() { Width = 1024 });
```
<sup><a href='/src/Tests/Snippets.cs#L22-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-convert' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`MapRenderer.RenderPng` takes one or more collections and paints them bottom-up in the order given, and `RenderOptions` exposes the same styling knobs as the `MapBundleImage*` properties. See [GeoConvert](https://github.com/SimonCropp/GeoConvert) for the full format and rendering API.


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

The default staging path (`maps/<Region>/<layer>.<ext>` next to the built app) is fine for a console or desktop consumer that loads via `Maps.Open()` at runtime, but it isn't always where the consumer wants the file to land. `MapBundleOutputDirectory` lets the consumer point MapBundle at a different directory; MapBundle writes the produced files straight there, with the same `<Region>/<filename>.<ext>` layout, and skips the default `<None Link>` auto-stage (the file is already at its final destination). This works whether or not the build also converts/simplifies/renders — setting `MapBundleOutputDirectory` on its own redirects the verbatim FlatGeobuf copy too.

The motivating use case is a Blazor WebAssembly app that wants the simplified data served as a static asset. The whole pipeline collapses to three properties — no custom MSBuild target, no `<Copy>`, no `<Exec>`:

```xml
<PropertyGroup>
  <!-- Keep only the country borders out of MapBundle.World's eight layers. -->
  <MapBundleLayers>Borders</MapBundleLayers>
  <!-- Thin the geometry; MapBundle uses GeoConvert's topology-preserving simplifier, so shared admin borders stay joined. -->
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
- Redirected files are not copied into `maps/` next to the app, so the no-arg `Maps.Open()` (which only ever reads `AppContext.BaseDirectory/maps`) won't find them. The consumer reads them back with the directory overload — `Maps.Open("<MapBundleOutputDirectory>").Load("<Region>")` — or, for a Blazor/WASM app, fetches the served `.fgb` over HTTP. Loading via `Maps.Open` needs `MapBundleFormat` left at the default FlatGeobuf, since the reader is FlatGeobuf-only.


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
| [MapBundle.World](https://www.nuget.org/packages/MapBundle.World) | 93.7 MB | 168.1 MB | [🗺️](/maps/World.Borders.png) [🏙️](/maps/World.Cities.png) [〰️](/maps/World.Rivers.png) [💧](/maps/World.Lakes.png) [🏛️](/maps/World.StatesProvinces.png) [🏖️](/maps/World.Coastline.png) [🟩](/maps/World.Land.png) [🌊](/maps/World.Ocean.png) | 157,802 |

### Continents

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Africa](https://www.nuget.org/packages/MapBundle.Africa) | 5.5 MB | 12.1 MB | [🗺️](/maps/Africa.Borders.png) [🏙️](/maps/Africa.Cities.png) [〰️](/maps/Africa.Rivers.png) [💧](/maps/Africa.Lakes.png) [🏛️](/maps/Africa.StatesProvinces.png) [🏖️](/maps/Africa.Coastline.png) [🟩](/maps/Africa.Land.png) [🌊](/maps/Africa.Ocean.png) | 6,873 |
| [MapBundle.Antarctica](https://www.nuget.org/packages/MapBundle.Antarctica) | 18 KB | 5 KB | 🏙️ | 40 |
| [MapBundle.Asia](https://www.nuget.org/packages/MapBundle.Asia) | 15.2 MB | 29.7 MB | [🗺️](/maps/Asia.Borders.png) [🏙️](/maps/Asia.Cities.png) [〰️](/maps/Asia.Rivers.png) [💧](/maps/Asia.Lakes.png) [🏛️](/maps/Asia.StatesProvinces.png) [🏖️](/maps/Asia.Coastline.png) [🟩](/maps/Asia.Land.png) [🌊](/maps/Asia.Ocean.png) | 23,281 |
| [MapBundle.AustraliaOceania](https://www.nuget.org/packages/MapBundle.AustraliaOceania) | 16.8 MB | 25.0 MB | [🗺️](/maps/AustraliaOceania.Borders.png) [🏙️](/maps/AustraliaOceania.Cities.png) [〰️](/maps/AustraliaOceania.Rivers.png) [💧](/maps/AustraliaOceania.Lakes.png) [🏛️](/maps/AustraliaOceania.StatesProvinces.png) [🏖️](/maps/AustraliaOceania.Coastline.png) [🟩](/maps/AustraliaOceania.Land.png) [🌊](/maps/AustraliaOceania.Ocean.png) | 32,772 |
| [MapBundle.CentralAmerica](https://www.nuget.org/packages/MapBundle.CentralAmerica) | 2.6 MB | 4.5 MB | [🗺️](/maps/CentralAmerica.Borders.png) [🏙️](/maps/CentralAmerica.Cities.png) [〰️](/maps/CentralAmerica.Rivers.png) [💧](/maps/CentralAmerica.Lakes.png) [🏛️](/maps/CentralAmerica.StatesProvinces.png) [🏖️](/maps/CentralAmerica.Coastline.png) [🟩](/maps/CentralAmerica.Land.png) [🌊](/maps/CentralAmerica.Ocean.png) | 4,762 |
| [MapBundle.Europe](https://www.nuget.org/packages/MapBundle.Europe) | 71.3 MB | 101.9 MB | [🗺️](/maps/Europe.Borders.png) [🏙️](/maps/Europe.Cities.png) [〰️](/maps/Europe.Rivers.png) [💧](/maps/Europe.Lakes.png) [🏛️](/maps/Europe.StatesProvinces.png) [🏖️](/maps/Europe.Coastline.png) [🟩](/maps/Europe.Land.png) [🌊](/maps/Europe.Ocean.png) | 141,407 |
| [MapBundle.NorthAmerica](https://www.nuget.org/packages/MapBundle.NorthAmerica) | 69.0 MB | 101.4 MB | [🗺️](/maps/NorthAmerica.Borders.png) [🏙️](/maps/NorthAmerica.Cities.png) [〰️](/maps/NorthAmerica.Rivers.png) [💧](/maps/NorthAmerica.Lakes.png) [🏛️](/maps/NorthAmerica.StatesProvinces.png) [🏖️](/maps/NorthAmerica.Coastline.png) [🟩](/maps/NorthAmerica.Land.png) [🌊](/maps/NorthAmerica.Ocean.png) | 131,312 |
| [MapBundle.Russia](https://www.nuget.org/packages/MapBundle.Russia) | 45.7 MB | 62.1 MB | [🗺️](/maps/Russia.Borders.png) [🏙️](/maps/Russia.Cities.png) [〰️](/maps/Russia.Rivers.png) [💧](/maps/Russia.Lakes.png) [🏛️](/maps/Russia.StatesProvinces.png) [🏖️](/maps/Russia.Coastline.png) [🟩](/maps/Russia.Land.png) [🌊](/maps/Russia.Ocean.png) | 94,268 |
| [MapBundle.SouthAmerica](https://www.nuget.org/packages/MapBundle.SouthAmerica) | 8.2 MB | 15.2 MB | [🗺️](/maps/SouthAmerica.Borders.png) [🏙️](/maps/SouthAmerica.Cities.png) [〰️](/maps/SouthAmerica.Rivers.png) [💧](/maps/SouthAmerica.Lakes.png) [🏛️](/maps/SouthAmerica.StatesProvinces.png) [🏖️](/maps/SouthAmerica.Coastline.png) [🟩](/maps/SouthAmerica.Land.png) [🌊](/maps/SouthAmerica.Ocean.png) | 12,255 |

### Countries

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Afghanistan](https://www.nuget.org/packages/MapBundle.Afghanistan) | 114 KB | 282 KB | [🗺️](/maps/Afghanistan.Borders.png) [🏙️](/maps/Afghanistan.Cities.png) [〰️](/maps/Afghanistan.Rivers.png) [💧](/maps/Afghanistan.Lakes.png) [🏛️](/maps/Afghanistan.StatesProvinces.png) | 81 |
| [MapBundle.Albania](https://www.nuget.org/packages/MapBundle.Albania) | 64 KB | 100 KB | [🗺️](/maps/Albania.Borders.png) [🏙️](/maps/Albania.Cities.png) [〰️](/maps/Albania.Rivers.png) [💧](/maps/Albania.Lakes.png) [🏛️](/maps/Albania.StatesProvinces.png) [🏖️](/maps/Albania.Coastline.png) [🟩](/maps/Albania.Land.png) [🌊](/maps/Albania.Ocean.png) | 69 |
| [MapBundle.Algeria](https://www.nuget.org/packages/MapBundle.Algeria) | 341 KB | 521 KB | [🗺️](/maps/Algeria.Borders.png) [🏙️](/maps/Algeria.Cities.png) [〰️](/maps/Algeria.Rivers.png) [🏛️](/maps/Algeria.StatesProvinces.png) [🏖️](/maps/Algeria.Coastline.png) [🟩](/maps/Algeria.Land.png) [🌊](/maps/Algeria.Ocean.png) | 210 |
| [MapBundle.AmericanOceania](https://www.nuget.org/packages/MapBundle.AmericanOceania) | 129 KB | 213 KB | [🗺️](/maps/AmericanOceania.Borders.png) [🏙️](/maps/AmericanOceania.Cities.png) [🏛️](/maps/AmericanOceania.StatesProvinces.png) [🏖️](/maps/AmericanOceania.Coastline.png) [🟩](/maps/AmericanOceania.Land.png) [🌊](/maps/AmericanOceania.Ocean.png) | 193 |
| [MapBundle.Andorra](https://www.nuget.org/packages/MapBundle.Andorra) | 20 KB | 7 KB | [🗺️](/maps/Andorra.Borders.png) [🏙️](/maps/Andorra.Cities.png) [🏛️](/maps/Andorra.StatesProvinces.png) | 9 |
| [MapBundle.Angola](https://www.nuget.org/packages/MapBundle.Angola) | 150 KB | 337 KB | [🗺️](/maps/Angola.Borders.png) [🏙️](/maps/Angola.Cities.png) [〰️](/maps/Angola.Rivers.png) [🏛️](/maps/Angola.StatesProvinces.png) [🏖️](/maps/Angola.Coastline.png) [🟩](/maps/Angola.Land.png) [🌊](/maps/Angola.Ocean.png) | 101 |
| [MapBundle.Argentina](https://www.nuget.org/packages/MapBundle.Argentina) | 2.2 MB | 3.2 MB | [🗺️](/maps/Argentina.Borders.png) [🏙️](/maps/Argentina.Cities.png) [〰️](/maps/Argentina.Rivers.png) [💧](/maps/Argentina.Lakes.png) [🏛️](/maps/Argentina.StatesProvinces.png) [🏖️](/maps/Argentina.Coastline.png) [🟩](/maps/Argentina.Land.png) [🌊](/maps/Argentina.Ocean.png) | 3,851 |
| [MapBundle.Armenia](https://www.nuget.org/packages/MapBundle.Armenia) | 40 KB | 64 KB | [🗺️](/maps/Armenia.Borders.png) [🏙️](/maps/Armenia.Cities.png) [〰️](/maps/Armenia.Rivers.png) [💧](/maps/Armenia.Lakes.png) [🏛️](/maps/Armenia.StatesProvinces.png) | 27 |
| [MapBundle.Australia](https://www.nuget.org/packages/MapBundle.Australia) | 3.3 MB | 5.6 MB | [🗺️](/maps/Australia.Borders.png) [🏙️](/maps/Australia.Cities.png) [〰️](/maps/Australia.Rivers.png) [💧](/maps/Australia.Lakes.png) [🏛️](/maps/Australia.StatesProvinces.png) [🏖️](/maps/Australia.Coastline.png) [🟩](/maps/Australia.Land.png) [🌊](/maps/Australia.Ocean.png) | 4,829 |
| [MapBundle.Austria](https://www.nuget.org/packages/MapBundle.Austria) | 58 KB | 123 KB | [🗺️](/maps/Austria.Borders.png) [🏙️](/maps/Austria.Cities.png) [〰️](/maps/Austria.Rivers.png) [💧](/maps/Austria.Lakes.png) [🏛️](/maps/Austria.StatesProvinces.png) | 29 |
| [MapBundle.Azerbaijan](https://www.nuget.org/packages/MapBundle.Azerbaijan) | 101 KB | 200 KB | [🗺️](/maps/Azerbaijan.Borders.png) [🏙️](/maps/Azerbaijan.Cities.png) [〰️](/maps/Azerbaijan.Rivers.png) [💧](/maps/Azerbaijan.Lakes.png) [🏛️](/maps/Azerbaijan.StatesProvinces.png) [🏖️](/maps/Azerbaijan.Coastline.png) [🟩](/maps/Azerbaijan.Land.png) [🌊](/maps/Azerbaijan.Ocean.png) | 137 |
| [MapBundle.Bahamas](https://www.nuget.org/packages/MapBundle.Bahamas) | 744 KB | 1.2 MB | [🗺️](/maps/Bahamas.Borders.png) [🏙️](/maps/Bahamas.Cities.png) [🏛️](/maps/Bahamas.StatesProvinces.png) [🏖️](/maps/Bahamas.Coastline.png) [🟩](/maps/Bahamas.Land.png) [🌊](/maps/Bahamas.Ocean.png) | 1,825 |
| [MapBundle.Bangladesh](https://www.nuget.org/packages/MapBundle.Bangladesh) | 346 KB | 660 KB | [🗺️](/maps/Bangladesh.Borders.png) [🏙️](/maps/Bangladesh.Cities.png) [〰️](/maps/Bangladesh.Rivers.png) [💧](/maps/Bangladesh.Lakes.png) [🏛️](/maps/Bangladesh.StatesProvinces.png) [🏖️](/maps/Bangladesh.Coastline.png) [🟩](/maps/Bangladesh.Land.png) [🌊](/maps/Bangladesh.Ocean.png) | 635 |
| [MapBundle.Belarus](https://www.nuget.org/packages/MapBundle.Belarus) | 68 KB | 150 KB | [🗺️](/maps/Belarus.Borders.png) [🏙️](/maps/Belarus.Cities.png) [〰️](/maps/Belarus.Rivers.png) [💧](/maps/Belarus.Lakes.png) [🏛️](/maps/Belarus.StatesProvinces.png) | 33 |
| [MapBundle.Belgium](https://www.nuget.org/packages/MapBundle.Belgium) | 83 KB | 127 KB | [🗺️](/maps/Belgium.Borders.png) [🏙️](/maps/Belgium.Cities.png) [〰️](/maps/Belgium.Rivers.png) [🏛️](/maps/Belgium.StatesProvinces.png) [🏖️](/maps/Belgium.Coastline.png) [🟩](/maps/Belgium.Land.png) [🌊](/maps/Belgium.Ocean.png) | 43 |
| [MapBundle.Belize](https://www.nuget.org/packages/MapBundle.Belize) | 101 KB | 154 KB | [🗺️](/maps/Belize.Borders.png) [🏙️](/maps/Belize.Cities.png) [〰️](/maps/Belize.Rivers.png) [🏛️](/maps/Belize.StatesProvinces.png) [🏖️](/maps/Belize.Coastline.png) [🟩](/maps/Belize.Land.png) [🌊](/maps/Belize.Ocean.png) | 161 |
| [MapBundle.Benin](https://www.nuget.org/packages/MapBundle.Benin) | 44 KB | 76 KB | [🗺️](/maps/Benin.Borders.png) [🏙️](/maps/Benin.Cities.png) [〰️](/maps/Benin.Rivers.png) [🏛️](/maps/Benin.StatesProvinces.png) [🏖️](/maps/Benin.Coastline.png) [🟩](/maps/Benin.Land.png) [🌊](/maps/Benin.Ocean.png) | 31 |
| [MapBundle.Bhutan](https://www.nuget.org/packages/MapBundle.Bhutan) | 34 KB | 51 KB | [🗺️](/maps/Bhutan.Borders.png) [🏙️](/maps/Bhutan.Cities.png) [〰️](/maps/Bhutan.Rivers.png) [🏛️](/maps/Bhutan.StatesProvinces.png) | 26 |
| [MapBundle.Bolivia](https://www.nuget.org/packages/MapBundle.Bolivia) | 115 KB | 283 KB | [🗺️](/maps/Bolivia.Borders.png) [🏙️](/maps/Bolivia.Cities.png) [〰️](/maps/Bolivia.Rivers.png) [💧](/maps/Bolivia.Lakes.png) [🏛️](/maps/Bolivia.StatesProvinces.png) | 103 |
| [MapBundle.BosniaHerzegovina](https://www.nuget.org/packages/MapBundle.BosniaHerzegovina) | 134 KB | 196 KB | [🗺️](/maps/BosniaHerzegovina.Borders.png) [🏙️](/maps/BosniaHerzegovina.Cities.png) [〰️](/maps/BosniaHerzegovina.Rivers.png) [🏛️](/maps/BosniaHerzegovina.StatesProvinces.png) [🏖️](/maps/BosniaHerzegovina.Coastline.png) [🟩](/maps/BosniaHerzegovina.Land.png) [🌊](/maps/BosniaHerzegovina.Ocean.png) | 183 |
| [MapBundle.Botswana](https://www.nuget.org/packages/MapBundle.Botswana) | 65 KB | 136 KB | [🗺️](/maps/Botswana.Borders.png) [🏙️](/maps/Botswana.Cities.png) [〰️](/maps/Botswana.Rivers.png) [💧](/maps/Botswana.Lakes.png) [🏛️](/maps/Botswana.StatesProvinces.png) | 53 |
| [MapBundle.Brazil](https://www.nuget.org/packages/MapBundle.Brazil) | 1.4 MB | 2.6 MB | [🗺️](/maps/Brazil.Borders.png) [🏙️](/maps/Brazil.Cities.png) [〰️](/maps/Brazil.Rivers.png) [💧](/maps/Brazil.Lakes.png) [🏛️](/maps/Brazil.StatesProvinces.png) [🏖️](/maps/Brazil.Coastline.png) [🟩](/maps/Brazil.Land.png) [🌊](/maps/Brazil.Ocean.png) | 1,601 |
| [MapBundle.Bulgaria](https://www.nuget.org/packages/MapBundle.Bulgaria) | 77 KB | 145 KB | [🗺️](/maps/Bulgaria.Borders.png) [🏙️](/maps/Bulgaria.Cities.png) [〰️](/maps/Bulgaria.Rivers.png) [🏛️](/maps/Bulgaria.StatesProvinces.png) [🏖️](/maps/Bulgaria.Coastline.png) [🟩](/maps/Bulgaria.Land.png) [🌊](/maps/Bulgaria.Ocean.png) | 60 |
| [MapBundle.BurkinaFaso](https://www.nuget.org/packages/MapBundle.BurkinaFaso) | 84 KB | 215 KB | [🗺️](/maps/BurkinaFaso.Borders.png) [🏙️](/maps/BurkinaFaso.Cities.png) [〰️](/maps/BurkinaFaso.Rivers.png) [🏛️](/maps/BurkinaFaso.StatesProvinces.png) | 99 |
| [MapBundle.Burundi](https://www.nuget.org/packages/MapBundle.Burundi) | 42 KB | 80 KB | [🗺️](/maps/Burundi.Borders.png) [🏙️](/maps/Burundi.Cities.png) [〰️](/maps/Burundi.Rivers.png) [💧](/maps/Burundi.Lakes.png) [🏛️](/maps/Burundi.StatesProvinces.png) | 39 |
| [MapBundle.Cambodia](https://www.nuget.org/packages/MapBundle.Cambodia) | 122 KB | 228 KB | [🗺️](/maps/Cambodia.Borders.png) [🏙️](/maps/Cambodia.Cities.png) [〰️](/maps/Cambodia.Rivers.png) [💧](/maps/Cambodia.Lakes.png) [🏛️](/maps/Cambodia.StatesProvinces.png) [🏖️](/maps/Cambodia.Coastline.png) [🟩](/maps/Cambodia.Land.png) [🌊](/maps/Cambodia.Ocean.png) | 179 |
| [MapBundle.Cameroon](https://www.nuget.org/packages/MapBundle.Cameroon) | 120 KB | 267 KB | [🗺️](/maps/Cameroon.Borders.png) [🏙️](/maps/Cameroon.Cities.png) [〰️](/maps/Cameroon.Rivers.png) [💧](/maps/Cameroon.Lakes.png) [🏛️](/maps/Cameroon.StatesProvinces.png) [🏖️](/maps/Cameroon.Coastline.png) [🟩](/maps/Cameroon.Land.png) [🌊](/maps/Cameroon.Ocean.png) | 87 |
| [MapBundle.Canada](https://www.nuget.org/packages/MapBundle.Canada) | 23.9 MB | 37.2 MB | [🗺️](/maps/Canada.Borders.png) [🏙️](/maps/Canada.Cities.png) [〰️](/maps/Canada.Rivers.png) [💧](/maps/Canada.Lakes.png) [🏛️](/maps/Canada.StatesProvinces.png) [🏖️](/maps/Canada.Coastline.png) [🟩](/maps/Canada.Land.png) [🌊](/maps/Canada.Ocean.png) | 43,814 |
| [MapBundle.CapeVerde](https://www.nuget.org/packages/MapBundle.CapeVerde) | 63 KB | 84 KB | [🗺️](/maps/CapeVerde.Borders.png) [🏙️](/maps/CapeVerde.Cities.png) [🏛️](/maps/CapeVerde.StatesProvinces.png) [🏖️](/maps/CapeVerde.Coastline.png) [🟩](/maps/CapeVerde.Land.png) [🌊](/maps/CapeVerde.Ocean.png) | 61 |
| [MapBundle.CentralAfricanRepublic](https://www.nuget.org/packages/MapBundle.CentralAfricanRepublic) | 97 KB | 239 KB | [🗺️](/maps/CentralAfricanRepublic.Borders.png) [🏙️](/maps/CentralAfricanRepublic.Cities.png) [〰️](/maps/CentralAfricanRepublic.Rivers.png) [🏛️](/maps/CentralAfricanRepublic.StatesProvinces.png) | 59 |
| [MapBundle.Chad](https://www.nuget.org/packages/MapBundle.Chad) | 69 KB | 149 KB | [🗺️](/maps/Chad.Borders.png) [🏙️](/maps/Chad.Cities.png) [〰️](/maps/Chad.Rivers.png) [💧](/maps/Chad.Lakes.png) [🏛️](/maps/Chad.StatesProvinces.png) | 55 |
| [MapBundle.Chile](https://www.nuget.org/packages/MapBundle.Chile) | 4.4 MB | 7.7 MB | [🗺️](/maps/Chile.Borders.png) [🏙️](/maps/Chile.Cities.png) [〰️](/maps/Chile.Rivers.png) [💧](/maps/Chile.Lakes.png) [🏛️](/maps/Chile.StatesProvinces.png) [🏖️](/maps/Chile.Coastline.png) [🟩](/maps/Chile.Land.png) [🌊](/maps/Chile.Ocean.png) | 7,276 |
| [MapBundle.China](https://www.nuget.org/packages/MapBundle.China) | 4.9 MB | 7.8 MB | [🗺️](/maps/China.Borders.png) [🏙️](/maps/China.Cities.png) [〰️](/maps/China.Rivers.png) [💧](/maps/China.Lakes.png) [🏛️](/maps/China.StatesProvinces.png) [🏖️](/maps/China.Coastline.png) [🟩](/maps/China.Land.png) [🌊](/maps/China.Ocean.png) | 8,847 |
| [MapBundle.Colombia](https://www.nuget.org/packages/MapBundle.Colombia) | 697 KB | 1.1 MB | [🗺️](/maps/Colombia.Borders.png) [🏙️](/maps/Colombia.Cities.png) [〰️](/maps/Colombia.Rivers.png) [💧](/maps/Colombia.Lakes.png) [🏛️](/maps/Colombia.StatesProvinces.png) [🏖️](/maps/Colombia.Coastline.png) [🟩](/maps/Colombia.Land.png) [🌊](/maps/Colombia.Ocean.png) | 760 |
| [MapBundle.CongoDemocraticRepublic](https://www.nuget.org/packages/MapBundle.CongoDemocraticRepublic) | 280 KB | 711 KB | [🗺️](/maps/CongoDemocraticRepublic.Borders.png) [🏙️](/maps/CongoDemocraticRepublic.Cities.png) [〰️](/maps/CongoDemocraticRepublic.Rivers.png) [💧](/maps/CongoDemocraticRepublic.Lakes.png) [🏛️](/maps/CongoDemocraticRepublic.StatesProvinces.png) [🏖️](/maps/CongoDemocraticRepublic.Coastline.png) [🟩](/maps/CongoDemocraticRepublic.Land.png) [🌊](/maps/CongoDemocraticRepublic.Ocean.png) | 201 |
| [MapBundle.CongoBrazzaville](https://www.nuget.org/packages/MapBundle.CongoBrazzaville) | 85 KB | 188 KB | [🗺️](/maps/CongoBrazzaville.Borders.png) [🏙️](/maps/CongoBrazzaville.Cities.png) [〰️](/maps/CongoBrazzaville.Rivers.png) [💧](/maps/CongoBrazzaville.Lakes.png) [🏛️](/maps/CongoBrazzaville.StatesProvinces.png) [🏖️](/maps/CongoBrazzaville.Coastline.png) [🟩](/maps/CongoBrazzaville.Land.png) [🌊](/maps/CongoBrazzaville.Ocean.png) | 61 |
| [MapBundle.CookIslands](https://www.nuget.org/packages/MapBundle.CookIslands) | 33 KB | 34 KB | [🗺️](/maps/CookIslands.Borders.png) [🏙️](/maps/CookIslands.Cities.png) [🏖️](/maps/CookIslands.Coastline.png) [🟩](/maps/CookIslands.Land.png) [🌊](/maps/CookIslands.Ocean.png) | 92 |
| [MapBundle.CostaRica](https://www.nuget.org/packages/MapBundle.CostaRica) | 102 KB | 155 KB | [🗺️](/maps/CostaRica.Borders.png) [🏙️](/maps/CostaRica.Cities.png) [〰️](/maps/CostaRica.Rivers.png) [💧](/maps/CostaRica.Lakes.png) [🏛️](/maps/CostaRica.StatesProvinces.png) [🏖️](/maps/CostaRica.Coastline.png) [🟩](/maps/CostaRica.Land.png) [🌊](/maps/CostaRica.Ocean.png) | 80 |
| [MapBundle.Croatia](https://www.nuget.org/packages/MapBundle.Croatia) | 357 KB | 618 KB | [🗺️](/maps/Croatia.Borders.png) [🏙️](/maps/Croatia.Cities.png) [〰️](/maps/Croatia.Rivers.png) [🏛️](/maps/Croatia.StatesProvinces.png) [🏖️](/maps/Croatia.Coastline.png) [🟩](/maps/Croatia.Land.png) [🌊](/maps/Croatia.Ocean.png) | 448 |
| [MapBundle.Cuba](https://www.nuget.org/packages/MapBundle.Cuba) | 540 KB | 971 KB | [🗺️](/maps/Cuba.Borders.png) [🏙️](/maps/Cuba.Cities.png) [🏛️](/maps/Cuba.StatesProvinces.png) [🏖️](/maps/Cuba.Coastline.png) [🟩](/maps/Cuba.Land.png) [🌊](/maps/Cuba.Ocean.png) | 1,132 |
| [MapBundle.Cyprus](https://www.nuget.org/packages/MapBundle.Cyprus) | 48 KB | 57 KB | [🗺️](/maps/Cyprus.Borders.png) [🏙️](/maps/Cyprus.Cities.png) [🏛️](/maps/Cyprus.StatesProvinces.png) [🏖️](/maps/Cyprus.Coastline.png) [🟩](/maps/Cyprus.Land.png) [🌊](/maps/Cyprus.Ocean.png) | 15 |
| [MapBundle.CzechRepublic](https://www.nuget.org/packages/MapBundle.CzechRepublic) | 65 KB | 177 KB | [🗺️](/maps/CzechRepublic.Borders.png) [🏙️](/maps/CzechRepublic.Cities.png) [〰️](/maps/CzechRepublic.Rivers.png) [🏛️](/maps/CzechRepublic.StatesProvinces.png) | 111 |
| [MapBundle.Denmark](https://www.nuget.org/packages/MapBundle.Denmark) | 490 KB | 745 KB | [🗺️](/maps/Denmark.Borders.png) [🏙️](/maps/Denmark.Cities.png) [💧](/maps/Denmark.Lakes.png) [🏛️](/maps/Denmark.StatesProvinces.png) [🏖️](/maps/Denmark.Coastline.png) [🟩](/maps/Denmark.Land.png) [🌊](/maps/Denmark.Ocean.png) | 619 |
| [MapBundle.Djibouti](https://www.nuget.org/packages/MapBundle.Djibouti) | 50 KB | 81 KB | [🗺️](/maps/Djibouti.Borders.png) [🏙️](/maps/Djibouti.Cities.png) [💧](/maps/Djibouti.Lakes.png) [🏛️](/maps/Djibouti.StatesProvinces.png) [🏖️](/maps/Djibouti.Coastline.png) [🟩](/maps/Djibouti.Land.png) [🌊](/maps/Djibouti.Ocean.png) | 31 |
| [MapBundle.EastTimor](https://www.nuget.org/packages/MapBundle.EastTimor) | 56 KB | 70 KB | [🗺️](/maps/EastTimor.Borders.png) [🏙️](/maps/EastTimor.Cities.png) [🏛️](/maps/EastTimor.StatesProvinces.png) [🏖️](/maps/EastTimor.Coastline.png) [🟩](/maps/EastTimor.Land.png) [🌊](/maps/EastTimor.Ocean.png) | 48 |
| [MapBundle.Ecuador](https://www.nuget.org/packages/MapBundle.Ecuador) | 323 KB | 564 KB | [🗺️](/maps/Ecuador.Borders.png) [🏙️](/maps/Ecuador.Cities.png) [〰️](/maps/Ecuador.Rivers.png) [🏛️](/maps/Ecuador.StatesProvinces.png) [🏖️](/maps/Ecuador.Coastline.png) [🟩](/maps/Ecuador.Land.png) [🌊](/maps/Ecuador.Ocean.png) | 390 |
| [MapBundle.Egypt](https://www.nuget.org/packages/MapBundle.Egypt) | 277 KB | 556 KB | [🗺️](/maps/Egypt.Borders.png) [🏙️](/maps/Egypt.Cities.png) [〰️](/maps/Egypt.Rivers.png) [💧](/maps/Egypt.Lakes.png) [🏛️](/maps/Egypt.StatesProvinces.png) [🏖️](/maps/Egypt.Coastline.png) [🟩](/maps/Egypt.Land.png) [🌊](/maps/Egypt.Ocean.png) | 271 |
| [MapBundle.ElSalvador](https://www.nuget.org/packages/MapBundle.ElSalvador) | 87 KB | 120 KB | [🗺️](/maps/ElSalvador.Borders.png) [🏙️](/maps/ElSalvador.Cities.png) [〰️](/maps/ElSalvador.Rivers.png) [🏛️](/maps/ElSalvador.StatesProvinces.png) [🏖️](/maps/ElSalvador.Coastline.png) [🟩](/maps/ElSalvador.Land.png) [🌊](/maps/ElSalvador.Ocean.png) | 96 |
| [MapBundle.EquatorialGuinea](https://www.nuget.org/packages/MapBundle.EquatorialGuinea) | 67 KB | 91 KB | [🗺️](/maps/EquatorialGuinea.Borders.png) [🏙️](/maps/EquatorialGuinea.Cities.png) [〰️](/maps/EquatorialGuinea.Rivers.png) [💧](/maps/EquatorialGuinea.Lakes.png) [🏛️](/maps/EquatorialGuinea.StatesProvinces.png) [🏖️](/maps/EquatorialGuinea.Coastline.png) [🟩](/maps/EquatorialGuinea.Land.png) [🌊](/maps/EquatorialGuinea.Ocean.png) | 60 |
| [MapBundle.Eritrea](https://www.nuget.org/packages/MapBundle.Eritrea) | 220 KB | 371 KB | [🗺️](/maps/Eritrea.Borders.png) [🏙️](/maps/Eritrea.Cities.png) [〰️](/maps/Eritrea.Rivers.png) [🏛️](/maps/Eritrea.StatesProvinces.png) [🏖️](/maps/Eritrea.Coastline.png) [🟩](/maps/Eritrea.Land.png) [🌊](/maps/Eritrea.Ocean.png) | 534 |
| [MapBundle.Estonia](https://www.nuget.org/packages/MapBundle.Estonia) | 234 KB | 343 KB | [🗺️](/maps/Estonia.Borders.png) [🏙️](/maps/Estonia.Cities.png) [〰️](/maps/Estonia.Rivers.png) [💧](/maps/Estonia.Lakes.png) [🏛️](/maps/Estonia.StatesProvinces.png) [🏖️](/maps/Estonia.Coastline.png) [🟩](/maps/Estonia.Land.png) [🌊](/maps/Estonia.Ocean.png) | 247 |
| [MapBundle.Ethiopia](https://www.nuget.org/packages/MapBundle.Ethiopia) | 166 KB | 303 KB | [🗺️](/maps/Ethiopia.Borders.png) [🏙️](/maps/Ethiopia.Cities.png) [〰️](/maps/Ethiopia.Rivers.png) [💧](/maps/Ethiopia.Lakes.png) [🏛️](/maps/Ethiopia.StatesProvinces.png) [🏖️](/maps/Ethiopia.Coastline.png) [🟩](/maps/Ethiopia.Land.png) [🌊](/maps/Ethiopia.Ocean.png) | 218 |
| [MapBundle.FaroeIslands](https://www.nuget.org/packages/MapBundle.FaroeIslands) | 77 KB | 88 KB | [🗺️](/maps/FaroeIslands.Borders.png) [🏙️](/maps/FaroeIslands.Cities.png) [🏖️](/maps/FaroeIslands.Coastline.png) [🟩](/maps/FaroeIslands.Land.png) [🌊](/maps/FaroeIslands.Ocean.png) | 47 |
| [MapBundle.Fiji](https://www.nuget.org/packages/MapBundle.Fiji) | 1.9 MB | 2.7 MB | [🗺️](/maps/Fiji.Borders.png) [🏙️](/maps/Fiji.Cities.png) [〰️](/maps/Fiji.Rivers.png) [💧](/maps/Fiji.Lakes.png) [🏛️](/maps/Fiji.StatesProvinces.png) [🏖️](/maps/Fiji.Coastline.png) [🟩](/maps/Fiji.Land.png) [🌊](/maps/Fiji.Ocean.png) | 3,963 |
| [MapBundle.Finland](https://www.nuget.org/packages/MapBundle.Finland) | 2.6 MB | 4.3 MB | [🗺️](/maps/Finland.Borders.png) [🏙️](/maps/Finland.Cities.png) [〰️](/maps/Finland.Rivers.png) [💧](/maps/Finland.Lakes.png) [🏛️](/maps/Finland.StatesProvinces.png) [🏖️](/maps/Finland.Coastline.png) [🟩](/maps/Finland.Land.png) [🌊](/maps/Finland.Ocean.png) | 9,589 |
| [MapBundle.France](https://www.nuget.org/packages/MapBundle.France) | 27.2 MB | 36.1 MB | [🗺️](/maps/France.Borders.png) [🏙️](/maps/France.Cities.png) [〰️](/maps/France.Rivers.png) [💧](/maps/France.Lakes.png) [🏛️](/maps/France.StatesProvinces.png) [🏖️](/maps/France.Coastline.png) [🟩](/maps/France.Land.png) [🌊](/maps/France.Ocean.png) | 48,484 |
| [MapBundle.GccStates](https://www.nuget.org/packages/MapBundle.GccStates) | 950 KB | 1.5 MB | [🗺️](/maps/GccStates.Borders.png) [🏙️](/maps/GccStates.Cities.png) [〰️](/maps/GccStates.Rivers.png) [💧](/maps/GccStates.Lakes.png) [🏛️](/maps/GccStates.StatesProvinces.png) [🏖️](/maps/GccStates.Coastline.png) [🟩](/maps/GccStates.Land.png) [🌊](/maps/GccStates.Ocean.png) | 1,247 |
| [MapBundle.Gabon](https://www.nuget.org/packages/MapBundle.Gabon) | 82 KB | 159 KB | [🗺️](/maps/Gabon.Borders.png) [🏙️](/maps/Gabon.Cities.png) [〰️](/maps/Gabon.Rivers.png) [💧](/maps/Gabon.Lakes.png) [🏛️](/maps/Gabon.StatesProvinces.png) [🏖️](/maps/Gabon.Coastline.png) [🟩](/maps/Gabon.Land.png) [🌊](/maps/Gabon.Ocean.png) | 60 |
| [MapBundle.Georgia](https://www.nuget.org/packages/MapBundle.Georgia) | 56 KB | 100 KB | [🗺️](/maps/Georgia.Borders.png) [🏙️](/maps/Georgia.Cities.png) [〰️](/maps/Georgia.Rivers.png) [💧](/maps/Georgia.Lakes.png) [🏛️](/maps/Georgia.StatesProvinces.png) [🏖️](/maps/Georgia.Coastline.png) [🟩](/maps/Georgia.Land.png) [🌊](/maps/Georgia.Ocean.png) | 29 |
| [MapBundle.Germany](https://www.nuget.org/packages/MapBundle.Germany) | 387 KB | 655 KB | [🗺️](/maps/Germany.Borders.png) [🏙️](/maps/Germany.Cities.png) [〰️](/maps/Germany.Rivers.png) [💧](/maps/Germany.Lakes.png) [🏛️](/maps/Germany.StatesProvinces.png) [🏖️](/maps/Germany.Coastline.png) [🟩](/maps/Germany.Land.png) [🌊](/maps/Germany.Ocean.png) | 445 |
| [MapBundle.Ghana](https://www.nuget.org/packages/MapBundle.Ghana) | 85 KB | 155 KB | [🗺️](/maps/Ghana.Borders.png) [🏙️](/maps/Ghana.Cities.png) [〰️](/maps/Ghana.Rivers.png) [💧](/maps/Ghana.Lakes.png) [🏛️](/maps/Ghana.StatesProvinces.png) [🏖️](/maps/Ghana.Coastline.png) [🟩](/maps/Ghana.Land.png) [🌊](/maps/Ghana.Ocean.png) | 45 |
| [MapBundle.Greece](https://www.nuget.org/packages/MapBundle.Greece) | 918 KB | 1.5 MB | [🗺️](/maps/Greece.Borders.png) [🏙️](/maps/Greece.Cities.png) [〰️](/maps/Greece.Rivers.png) [🏛️](/maps/Greece.StatesProvinces.png) [🏖️](/maps/Greece.Coastline.png) [🟩](/maps/Greece.Land.png) [🌊](/maps/Greece.Ocean.png) | 961 |
| [MapBundle.Greenland](https://www.nuget.org/packages/MapBundle.Greenland) | 9.1 MB | 13.4 MB | [🗺️](/maps/Greenland.Borders.png) [🏙️](/maps/Greenland.Cities.png) [〰️](/maps/Greenland.Rivers.png) [💧](/maps/Greenland.Lakes.png) [🏛️](/maps/Greenland.StatesProvinces.png) [🏖️](/maps/Greenland.Coastline.png) [🟩](/maps/Greenland.Land.png) [🌊](/maps/Greenland.Ocean.png) | 17,456 |
| [MapBundle.Guatemala](https://www.nuget.org/packages/MapBundle.Guatemala) | 68 KB | 104 KB | [🗺️](/maps/Guatemala.Borders.png) [🏙️](/maps/Guatemala.Cities.png) [〰️](/maps/Guatemala.Rivers.png) [💧](/maps/Guatemala.Lakes.png) [🏛️](/maps/Guatemala.StatesProvinces.png) [🏖️](/maps/Guatemala.Coastline.png) [🟩](/maps/Guatemala.Land.png) [🌊](/maps/Guatemala.Ocean.png) | 85 |
| [MapBundle.Guinea](https://www.nuget.org/packages/MapBundle.Guinea) | 194 KB | 385 KB | [🗺️](/maps/Guinea.Borders.png) [🏙️](/maps/Guinea.Cities.png) [〰️](/maps/Guinea.Rivers.png) [🏛️](/maps/Guinea.StatesProvinces.png) [🏖️](/maps/Guinea.Coastline.png) [🟩](/maps/Guinea.Land.png) [🌊](/maps/Guinea.Ocean.png) | 242 |
| [MapBundle.GuineaBissau](https://www.nuget.org/packages/MapBundle.GuineaBissau) | 176 KB | 273 KB | [🗺️](/maps/GuineaBissau.Borders.png) [🏙️](/maps/GuineaBissau.Cities.png) [〰️](/maps/GuineaBissau.Rivers.png) [🏛️](/maps/GuineaBissau.StatesProvinces.png) [🏖️](/maps/GuineaBissau.Coastline.png) [🟩](/maps/GuineaBissau.Land.png) [🌊](/maps/GuineaBissau.Ocean.png) | 199 |
| [MapBundle.Guyana](https://www.nuget.org/packages/MapBundle.Guyana) | 69 KB | 139 KB | [🗺️](/maps/Guyana.Borders.png) [🏙️](/maps/Guyana.Cities.png) [〰️](/maps/Guyana.Rivers.png) [🏛️](/maps/Guyana.StatesProvinces.png) [🏖️](/maps/Guyana.Coastline.png) [🟩](/maps/Guyana.Land.png) [🌊](/maps/Guyana.Ocean.png) | 35 |
| [MapBundle.HaitiAndDomrep](https://www.nuget.org/packages/MapBundle.HaitiAndDomrep) | 190 KB | 402 KB | [🗺️](/maps/HaitiAndDomrep.Borders.png) [🏙️](/maps/HaitiAndDomrep.Cities.png) [〰️](/maps/HaitiAndDomrep.Rivers.png) [💧](/maps/HaitiAndDomrep.Lakes.png) [🏛️](/maps/HaitiAndDomrep.StatesProvinces.png) [🏖️](/maps/HaitiAndDomrep.Coastline.png) [🟩](/maps/HaitiAndDomrep.Land.png) [🌊](/maps/HaitiAndDomrep.Ocean.png) | 145 |
| [MapBundle.Honduras](https://www.nuget.org/packages/MapBundle.Honduras) | 222 KB | 338 KB | [🗺️](/maps/Honduras.Borders.png) [🏙️](/maps/Honduras.Cities.png) [〰️](/maps/Honduras.Rivers.png) [💧](/maps/Honduras.Lakes.png) [🏛️](/maps/Honduras.StatesProvinces.png) [🏖️](/maps/Honduras.Coastline.png) [🟩](/maps/Honduras.Land.png) [🌊](/maps/Honduras.Ocean.png) | 267 |
| [MapBundle.Hungary](https://www.nuget.org/packages/MapBundle.Hungary) | 56 KB | 114 KB | [🗺️](/maps/Hungary.Borders.png) [🏙️](/maps/Hungary.Cities.png) [〰️](/maps/Hungary.Rivers.png) [💧](/maps/Hungary.Lakes.png) [🏛️](/maps/Hungary.StatesProvinces.png) | 58 |
| [MapBundle.Iceland](https://www.nuget.org/packages/MapBundle.Iceland) | 460 KB | 692 KB | [🗺️](/maps/Iceland.Borders.png) [🏙️](/maps/Iceland.Cities.png) [〰️](/maps/Iceland.Rivers.png) [🏛️](/maps/Iceland.StatesProvinces.png) [🏖️](/maps/Iceland.Coastline.png) [🟩](/maps/Iceland.Land.png) [🌊](/maps/Iceland.Ocean.png) | 516 |
| [MapBundle.India](https://www.nuget.org/packages/MapBundle.India) | 1.2 MB | 2.4 MB | [🗺️](/maps/India.Borders.png) [🏙️](/maps/India.Cities.png) [〰️](/maps/India.Rivers.png) [💧](/maps/India.Lakes.png) [🏛️](/maps/India.StatesProvinces.png) [🏖️](/maps/India.Coastline.png) [🟩](/maps/India.Land.png) [🌊](/maps/India.Ocean.png) | 2,103 |
| [MapBundle.Indonesia](https://www.nuget.org/packages/MapBundle.Indonesia) | 3.6 MB | 6.5 MB | [🗺️](/maps/Indonesia.Borders.png) [🏙️](/maps/Indonesia.Cities.png) [〰️](/maps/Indonesia.Rivers.png) [💧](/maps/Indonesia.Lakes.png) [🏛️](/maps/Indonesia.StatesProvinces.png) [🏖️](/maps/Indonesia.Coastline.png) [🟩](/maps/Indonesia.Land.png) [🌊](/maps/Indonesia.Ocean.png) | 6,052 |
| [MapBundle.Iran](https://www.nuget.org/packages/MapBundle.Iran) | 544 KB | 892 KB | [🗺️](/maps/Iran.Borders.png) [🏙️](/maps/Iran.Cities.png) [〰️](/maps/Iran.Rivers.png) [💧](/maps/Iran.Lakes.png) [🏛️](/maps/Iran.StatesProvinces.png) [🏖️](/maps/Iran.Coastline.png) [🟩](/maps/Iran.Land.png) [🌊](/maps/Iran.Ocean.png) | 575 |
| [MapBundle.Iraq](https://www.nuget.org/packages/MapBundle.Iraq) | 94 KB | 179 KB | [🗺️](/maps/Iraq.Borders.png) [🏙️](/maps/Iraq.Cities.png) [〰️](/maps/Iraq.Rivers.png) [💧](/maps/Iraq.Lakes.png) [🏛️](/maps/Iraq.StatesProvinces.png) [🏖️](/maps/Iraq.Coastline.png) [🟩](/maps/Iraq.Land.png) [🌊](/maps/Iraq.Ocean.png) | 91 |
| [MapBundle.IrelandAndNorthernIreland](https://www.nuget.org/packages/MapBundle.IrelandAndNorthernIreland) | 489 KB | 785 KB | [🗺️](/maps/IrelandAndNorthernIreland.Borders.png) [🏙️](/maps/IrelandAndNorthernIreland.Cities.png) [〰️](/maps/IrelandAndNorthernIreland.Rivers.png) [💧](/maps/IrelandAndNorthernIreland.Lakes.png) [🏛️](/maps/IrelandAndNorthernIreland.StatesProvinces.png) [🏖️](/maps/IrelandAndNorthernIreland.Coastline.png) [🟩](/maps/IrelandAndNorthernIreland.Land.png) [🌊](/maps/IrelandAndNorthernIreland.Ocean.png) | 531 |
| [MapBundle.IsraelAndPalestine](https://www.nuget.org/packages/MapBundle.IsraelAndPalestine) | 47 KB | 76 KB | [🗺️](/maps/IsraelAndPalestine.Borders.png) [🏙️](/maps/IsraelAndPalestine.Cities.png) [〰️](/maps/IsraelAndPalestine.Rivers.png) [💧](/maps/IsraelAndPalestine.Lakes.png) [🏛️](/maps/IsraelAndPalestine.StatesProvinces.png) [🏖️](/maps/IsraelAndPalestine.Coastline.png) [🟩](/maps/IsraelAndPalestine.Land.png) [🌊](/maps/IsraelAndPalestine.Ocean.png) | 43 |
| [MapBundle.Italy](https://www.nuget.org/packages/MapBundle.Italy) | 769 KB | 1.2 MB | [🗺️](/maps/Italy.Borders.png) [🏙️](/maps/Italy.Cities.png) [〰️](/maps/Italy.Rivers.png) [💧](/maps/Italy.Lakes.png) [🏛️](/maps/Italy.StatesProvinces.png) [🏖️](/maps/Italy.Coastline.png) [🟩](/maps/Italy.Land.png) [🌊](/maps/Italy.Ocean.png) | 818 |
| [MapBundle.IvoryCoast](https://www.nuget.org/packages/MapBundle.IvoryCoast) | 93 KB | 215 KB | [🗺️](/maps/IvoryCoast.Borders.png) [🏙️](/maps/IvoryCoast.Cities.png) [〰️](/maps/IvoryCoast.Rivers.png) [💧](/maps/IvoryCoast.Lakes.png) [🏛️](/maps/IvoryCoast.StatesProvinces.png) [🏖️](/maps/IvoryCoast.Coastline.png) [🟩](/maps/IvoryCoast.Land.png) [🌊](/maps/IvoryCoast.Ocean.png) | 61 |
| [MapBundle.Jamaica](https://www.nuget.org/packages/MapBundle.Jamaica) | 53 KB | 68 KB | [🗺️](/maps/Jamaica.Borders.png) [🏙️](/maps/Jamaica.Cities.png) [🏛️](/maps/Jamaica.StatesProvinces.png) [🏖️](/maps/Jamaica.Coastline.png) [🟩](/maps/Jamaica.Land.png) [🌊](/maps/Jamaica.Ocean.png) | 34 |
| [MapBundle.Japan](https://www.nuget.org/packages/MapBundle.Japan) | 2.1 MB | 3.3 MB | [🗺️](/maps/Japan.Borders.png) [🏙️](/maps/Japan.Cities.png) [〰️](/maps/Japan.Rivers.png) [💧](/maps/Japan.Lakes.png) [🏛️](/maps/Japan.StatesProvinces.png) [🏖️](/maps/Japan.Coastline.png) [🟩](/maps/Japan.Land.png) [🌊](/maps/Japan.Ocean.png) | 2,956 |
| [MapBundle.Jordan](https://www.nuget.org/packages/MapBundle.Jordan) | 38 KB | 47 KB | [🗺️](/maps/Jordan.Borders.png) [🏙️](/maps/Jordan.Cities.png) [〰️](/maps/Jordan.Rivers.png) [💧](/maps/Jordan.Lakes.png) [🏛️](/maps/Jordan.StatesProvinces.png) [🏖️](/maps/Jordan.Coastline.png) [🟩](/maps/Jordan.Land.png) [🌊](/maps/Jordan.Ocean.png) | 32 |
| [MapBundle.Kazakhstan](https://www.nuget.org/packages/MapBundle.Kazakhstan) | 378 KB | 703 KB | [🗺️](/maps/Kazakhstan.Borders.png) [🏙️](/maps/Kazakhstan.Cities.png) [〰️](/maps/Kazakhstan.Rivers.png) [💧](/maps/Kazakhstan.Lakes.png) [🏛️](/maps/Kazakhstan.StatesProvinces.png) [🏖️](/maps/Kazakhstan.Coastline.png) [🟩](/maps/Kazakhstan.Land.png) [🌊](/maps/Kazakhstan.Ocean.png) | 489 |
| [MapBundle.Kenya](https://www.nuget.org/packages/MapBundle.Kenya) | 143 KB | 286 KB | [🗺️](/maps/Kenya.Borders.png) [🏙️](/maps/Kenya.Cities.png) [〰️](/maps/Kenya.Rivers.png) [💧](/maps/Kenya.Lakes.png) [🏛️](/maps/Kenya.StatesProvinces.png) [🏖️](/maps/Kenya.Coastline.png) [🟩](/maps/Kenya.Land.png) [🌊](/maps/Kenya.Ocean.png) | 171 |
| [MapBundle.Kiribati](https://www.nuget.org/packages/MapBundle.Kiribati) | 4.7 MB | 6.2 MB | [🗺️](/maps/Kiribati.Borders.png) [🏙️](/maps/Kiribati.Cities.png) [〰️](/maps/Kiribati.Rivers.png) [💧](/maps/Kiribati.Lakes.png) [🏖️](/maps/Kiribati.Coastline.png) [🟩](/maps/Kiribati.Land.png) [🌊](/maps/Kiribati.Ocean.png) | 10,347 |
| [MapBundle.Kyrgyzstan](https://www.nuget.org/packages/MapBundle.Kyrgyzstan) | 76 KB | 167 KB | [🗺️](/maps/Kyrgyzstan.Borders.png) [🏙️](/maps/Kyrgyzstan.Cities.png) [〰️](/maps/Kyrgyzstan.Rivers.png) [💧](/maps/Kyrgyzstan.Lakes.png) [🏛️](/maps/Kyrgyzstan.StatesProvinces.png) | 35 |
| [MapBundle.Laos](https://www.nuget.org/packages/MapBundle.Laos) | 195 KB | 378 KB | [🗺️](/maps/Laos.Borders.png) [🏙️](/maps/Laos.Cities.png) [〰️](/maps/Laos.Rivers.png) [💧](/maps/Laos.Lakes.png) [🏛️](/maps/Laos.StatesProvinces.png) [🏖️](/maps/Laos.Coastline.png) [🟩](/maps/Laos.Land.png) [🌊](/maps/Laos.Ocean.png) | 288 |
| [MapBundle.Latvia](https://www.nuget.org/packages/MapBundle.Latvia) | 77 KB | 146 KB | [🗺️](/maps/Latvia.Borders.png) [🏙️](/maps/Latvia.Cities.png) [〰️](/maps/Latvia.Rivers.png) [💧](/maps/Latvia.Lakes.png) [🏛️](/maps/Latvia.StatesProvinces.png) [🏖️](/maps/Latvia.Coastline.png) [🟩](/maps/Latvia.Land.png) [🌊](/maps/Latvia.Ocean.png) | 141 |
| [MapBundle.Lebanon](https://www.nuget.org/packages/MapBundle.Lebanon) | 39 KB | 46 KB | [🗺️](/maps/Lebanon.Borders.png) [🏙️](/maps/Lebanon.Cities.png) [〰️](/maps/Lebanon.Rivers.png) [🏛️](/maps/Lebanon.StatesProvinces.png) [🏖️](/maps/Lebanon.Coastline.png) [🟩](/maps/Lebanon.Land.png) [🌊](/maps/Lebanon.Ocean.png) | 21 |
| [MapBundle.Lesotho](https://www.nuget.org/packages/MapBundle.Lesotho) | 37 KB | 57 KB | [🗺️](/maps/Lesotho.Borders.png) [🏙️](/maps/Lesotho.Cities.png) [〰️](/maps/Lesotho.Rivers.png) [🏛️](/maps/Lesotho.StatesProvinces.png) | 22 |
| [MapBundle.Liberia](https://www.nuget.org/packages/MapBundle.Liberia) | 61 KB | 119 KB | [🗺️](/maps/Liberia.Borders.png) [🏙️](/maps/Liberia.Cities.png) [〰️](/maps/Liberia.Rivers.png) [💧](/maps/Liberia.Lakes.png) [🏛️](/maps/Liberia.StatesProvinces.png) [🏖️](/maps/Liberia.Coastline.png) [🟩](/maps/Liberia.Land.png) [🌊](/maps/Liberia.Ocean.png) | 39 |
| [MapBundle.Libya](https://www.nuget.org/packages/MapBundle.Libya) | 96 KB | 164 KB | [🗺️](/maps/Libya.Borders.png) [🏙️](/maps/Libya.Cities.png) [🏛️](/maps/Libya.StatesProvinces.png) [🏖️](/maps/Libya.Coastline.png) [🟩](/maps/Libya.Land.png) [🌊](/maps/Libya.Ocean.png) | 73 |
| [MapBundle.Liechtenstein](https://www.nuget.org/packages/MapBundle.Liechtenstein) | 20 KB | 8 KB | [🗺️](/maps/Liechtenstein.Borders.png) [🏙️](/maps/Liechtenstein.Cities.png) [〰️](/maps/Liechtenstein.Rivers.png) [🏛️](/maps/Liechtenstein.StatesProvinces.png) | 14 |
| [MapBundle.Lithuania](https://www.nuget.org/packages/MapBundle.Lithuania) | 65 KB | 140 KB | [🗺️](/maps/Lithuania.Borders.png) [🏙️](/maps/Lithuania.Cities.png) [〰️](/maps/Lithuania.Rivers.png) [💧](/maps/Lithuania.Lakes.png) [🏛️](/maps/Lithuania.StatesProvinces.png) [🏖️](/maps/Lithuania.Coastline.png) [🟩](/maps/Lithuania.Land.png) [🌊](/maps/Lithuania.Ocean.png) | 88 |
| [MapBundle.Luxembourg](https://www.nuget.org/packages/MapBundle.Luxembourg) | 25 KB | 20 KB | [🗺️](/maps/Luxembourg.Borders.png) [🏙️](/maps/Luxembourg.Cities.png) [〰️](/maps/Luxembourg.Rivers.png) [🏛️](/maps/Luxembourg.StatesProvinces.png) | 17 |
| [MapBundle.Macedonia](https://www.nuget.org/packages/MapBundle.Macedonia) | 38 KB | 70 KB | [🗺️](/maps/Macedonia.Borders.png) [🏙️](/maps/Macedonia.Cities.png) [〰️](/maps/Macedonia.Rivers.png) [🏛️](/maps/Macedonia.StatesProvinces.png) | 78 |
| [MapBundle.Madagascar](https://www.nuget.org/packages/MapBundle.Madagascar) | 311 KB | 592 KB | [🗺️](/maps/Madagascar.Borders.png) [🏙️](/maps/Madagascar.Cities.png) [〰️](/maps/Madagascar.Rivers.png) [💧](/maps/Madagascar.Lakes.png) [🏛️](/maps/Madagascar.StatesProvinces.png) [🏖️](/maps/Madagascar.Coastline.png) [🟩](/maps/Madagascar.Land.png) [🌊](/maps/Madagascar.Ocean.png) | 325 |
| [MapBundle.Malawi](https://www.nuget.org/packages/MapBundle.Malawi) | 83 KB | 209 KB | [🗺️](/maps/Malawi.Borders.png) [🏙️](/maps/Malawi.Cities.png) [〰️](/maps/Malawi.Rivers.png) [💧](/maps/Malawi.Lakes.png) [🏛️](/maps/Malawi.StatesProvinces.png) | 61 |
| [MapBundle.MalaysiaSingaporeBrunei](https://www.nuget.org/packages/MapBundle.MalaysiaSingaporeBrunei) | 528 KB | 931 KB | [🗺️](/maps/MalaysiaSingaporeBrunei.Borders.png) [🏙️](/maps/MalaysiaSingaporeBrunei.Cities.png) [〰️](/maps/MalaysiaSingaporeBrunei.Rivers.png) [🏛️](/maps/MalaysiaSingaporeBrunei.StatesProvinces.png) [🏖️](/maps/MalaysiaSingaporeBrunei.Coastline.png) [🟩](/maps/MalaysiaSingaporeBrunei.Land.png) [🌊](/maps/MalaysiaSingaporeBrunei.Ocean.png) | 1,198 |
| [MapBundle.Maldives](https://www.nuget.org/packages/MapBundle.Maldives) | 100 KB | 220 KB | [🗺️](/maps/Maldives.Borders.png) [🏙️](/maps/Maldives.Cities.png) [🏛️](/maps/Maldives.StatesProvinces.png) [🏖️](/maps/Maldives.Coastline.png) [🟩](/maps/Maldives.Land.png) [🌊](/maps/Maldives.Ocean.png) | 465 |
| [MapBundle.Mali](https://www.nuget.org/packages/MapBundle.Mali) | 94 KB | 224 KB | [🗺️](/maps/Mali.Borders.png) [🏙️](/maps/Mali.Cities.png) [〰️](/maps/Mali.Rivers.png) [💧](/maps/Mali.Lakes.png) [🏛️](/maps/Mali.StatesProvinces.png) | 57 |
| [MapBundle.Malta](https://www.nuget.org/packages/MapBundle.Malta) | 33 KB | 34 KB | [🗺️](/maps/Malta.Borders.png) [🏙️](/maps/Malta.Cities.png) [🏛️](/maps/Malta.StatesProvinces.png) [🏖️](/maps/Malta.Coastline.png) [🟩](/maps/Malta.Land.png) [🌊](/maps/Malta.Ocean.png) | 79 |
| [MapBundle.MarshallIslands](https://www.nuget.org/packages/MapBundle.MarshallIslands) | 76 KB | 140 KB | [🗺️](/maps/MarshallIslands.Borders.png) [🏙️](/maps/MarshallIslands.Cities.png) [🏖️](/maps/MarshallIslands.Coastline.png) [🟩](/maps/MarshallIslands.Land.png) [🌊](/maps/MarshallIslands.Ocean.png) | 319 |
| [MapBundle.Mauritania](https://www.nuget.org/packages/MapBundle.Mauritania) | 85 KB | 140 KB | [🗺️](/maps/Mauritania.Borders.png) [🏙️](/maps/Mauritania.Cities.png) [〰️](/maps/Mauritania.Rivers.png) [🏛️](/maps/Mauritania.StatesProvinces.png) [🏖️](/maps/Mauritania.Coastline.png) [🟩](/maps/Mauritania.Land.png) [🌊](/maps/Mauritania.Ocean.png) | 76 |
| [MapBundle.Mauritius](https://www.nuget.org/packages/MapBundle.Mauritius) | 44 KB | 52 KB | [🗺️](/maps/Mauritius.Borders.png) [🏙️](/maps/Mauritius.Cities.png) [🏛️](/maps/Mauritius.StatesProvinces.png) [🏖️](/maps/Mauritius.Coastline.png) [🟩](/maps/Mauritius.Land.png) [🌊](/maps/Mauritius.Ocean.png) | 72 |
| [MapBundle.Mexico](https://www.nuget.org/packages/MapBundle.Mexico) | 1.5 MB | 2.5 MB | [🗺️](/maps/Mexico.Borders.png) [🏙️](/maps/Mexico.Cities.png) [〰️](/maps/Mexico.Rivers.png) [💧](/maps/Mexico.Lakes.png) [🏛️](/maps/Mexico.StatesProvinces.png) [🏖️](/maps/Mexico.Coastline.png) [🟩](/maps/Mexico.Land.png) [🌊](/maps/Mexico.Ocean.png) | 2,388 |
| [MapBundle.Micronesia](https://www.nuget.org/packages/MapBundle.Micronesia) | 66 KB | 103 KB | [🗺️](/maps/Micronesia.Borders.png) [🏙️](/maps/Micronesia.Cities.png) [🏛️](/maps/Micronesia.StatesProvinces.png) [🏖️](/maps/Micronesia.Coastline.png) [🟩](/maps/Micronesia.Land.png) [🌊](/maps/Micronesia.Ocean.png) | 229 |
| [MapBundle.Moldova](https://www.nuget.org/packages/MapBundle.Moldova) | 50 KB | 93 KB | [🗺️](/maps/Moldova.Borders.png) [🏙️](/maps/Moldova.Cities.png) [〰️](/maps/Moldova.Rivers.png) [🏛️](/maps/Moldova.StatesProvinces.png) [🏖️](/maps/Moldova.Coastline.png) [🟩](/maps/Moldova.Land.png) [🌊](/maps/Moldova.Ocean.png) | 49 |
| [MapBundle.Monaco](https://www.nuget.org/packages/MapBundle.Monaco) | 18 KB | 2 KB | [🗺️](/maps/Monaco.Borders.png) [🏙️](/maps/Monaco.Cities.png) [🏖️](/maps/Monaco.Coastline.png) [🟩](/maps/Monaco.Land.png) [🌊](/maps/Monaco.Ocean.png) | 5 |
| [MapBundle.Mongolia](https://www.nuget.org/packages/MapBundle.Mongolia) | 121 KB | 264 KB | [🗺️](/maps/Mongolia.Borders.png) [🏙️](/maps/Mongolia.Cities.png) [〰️](/maps/Mongolia.Rivers.png) [💧](/maps/Mongolia.Lakes.png) [🏛️](/maps/Mongolia.StatesProvinces.png) | 93 |
| [MapBundle.Montenegro](https://www.nuget.org/packages/MapBundle.Montenegro) | 48 KB | 71 KB | [🗺️](/maps/Montenegro.Borders.png) [🏙️](/maps/Montenegro.Cities.png) [〰️](/maps/Montenegro.Rivers.png) [💧](/maps/Montenegro.Lakes.png) [🏛️](/maps/Montenegro.StatesProvinces.png) [🏖️](/maps/Montenegro.Coastline.png) [🟩](/maps/Montenegro.Land.png) [🌊](/maps/Montenegro.Ocean.png) | 36 |
| [MapBundle.Morocco](https://www.nuget.org/packages/MapBundle.Morocco) | 190 KB | 322 KB | [🗺️](/maps/Morocco.Borders.png) [🏙️](/maps/Morocco.Cities.png) [〰️](/maps/Morocco.Rivers.png) [🏛️](/maps/Morocco.StatesProvinces.png) [🏖️](/maps/Morocco.Coastline.png) [🟩](/maps/Morocco.Land.png) [🌊](/maps/Morocco.Ocean.png) | 137 |
| [MapBundle.Mozambique](https://www.nuget.org/packages/MapBundle.Mozambique) | 219 KB | 420 KB | [🗺️](/maps/Mozambique.Borders.png) [🏙️](/maps/Mozambique.Cities.png) [〰️](/maps/Mozambique.Rivers.png) [💧](/maps/Mozambique.Lakes.png) [🏛️](/maps/Mozambique.StatesProvinces.png) [🏖️](/maps/Mozambique.Coastline.png) [🟩](/maps/Mozambique.Land.png) [🌊](/maps/Mozambique.Ocean.png) | 194 |
| [MapBundle.Myanmar](https://www.nuget.org/packages/MapBundle.Myanmar) | 837 KB | 1.7 MB | [🗺️](/maps/Myanmar.Borders.png) [🏙️](/maps/Myanmar.Cities.png) [〰️](/maps/Myanmar.Rivers.png) [💧](/maps/Myanmar.Lakes.png) [🏛️](/maps/Myanmar.StatesProvinces.png) [🏖️](/maps/Myanmar.Coastline.png) [🟩](/maps/Myanmar.Land.png) [🌊](/maps/Myanmar.Ocean.png) | 1,621 |
| [MapBundle.Namibia](https://www.nuget.org/packages/MapBundle.Namibia) | 100 KB | 198 KB | [🗺️](/maps/Namibia.Borders.png) [🏙️](/maps/Namibia.Cities.png) [〰️](/maps/Namibia.Rivers.png) [💧](/maps/Namibia.Lakes.png) [🏛️](/maps/Namibia.StatesProvinces.png) [🏖️](/maps/Namibia.Coastline.png) [🟩](/maps/Namibia.Land.png) [🌊](/maps/Namibia.Ocean.png) | 73 |
| [MapBundle.Nauru](https://www.nuget.org/packages/MapBundle.Nauru) | 19 KB | 5 KB | [🗺️](/maps/Nauru.Borders.png) [🏛️](/maps/Nauru.StatesProvinces.png) [🏖️](/maps/Nauru.Coastline.png) [🟩](/maps/Nauru.Land.png) [🌊](/maps/Nauru.Ocean.png) | 18 |
| [MapBundle.Nepal](https://www.nuget.org/packages/MapBundle.Nepal) | 53 KB | 107 KB | [🗺️](/maps/Nepal.Borders.png) [🏙️](/maps/Nepal.Cities.png) [〰️](/maps/Nepal.Rivers.png) | 24 |
| [MapBundle.Netherlands](https://www.nuget.org/packages/MapBundle.Netherlands) | 3.8 MB | 4.7 MB | [🗺️](/maps/Netherlands.Borders.png) [🏙️](/maps/Netherlands.Cities.png) [〰️](/maps/Netherlands.Rivers.png) [💧](/maps/Netherlands.Lakes.png) [🏛️](/maps/Netherlands.StatesProvinces.png) [🏖️](/maps/Netherlands.Coastline.png) [🟩](/maps/Netherlands.Land.png) [🌊](/maps/Netherlands.Ocean.png) | 5,086 |
| [MapBundle.NewCaledonia](https://www.nuget.org/packages/MapBundle.NewCaledonia) | 157 KB | 250 KB | [🗺️](/maps/NewCaledonia.Borders.png) [🏙️](/maps/NewCaledonia.Cities.png) [🏖️](/maps/NewCaledonia.Coastline.png) [🟩](/maps/NewCaledonia.Land.png) [🌊](/maps/NewCaledonia.Ocean.png) | 200 |
| [MapBundle.NewZealand](https://www.nuget.org/packages/MapBundle.NewZealand) | 2.2 MB | 3.2 MB | [🗺️](/maps/NewZealand.Borders.png) [🏙️](/maps/NewZealand.Cities.png) [〰️](/maps/NewZealand.Rivers.png) [💧](/maps/NewZealand.Lakes.png) [🏛️](/maps/NewZealand.StatesProvinces.png) [🏖️](/maps/NewZealand.Coastline.png) [🟩](/maps/NewZealand.Land.png) [🌊](/maps/NewZealand.Ocean.png) | 4,046 |
| [MapBundle.Nicaragua](https://www.nuget.org/packages/MapBundle.Nicaragua) | 182 KB | 266 KB | [🗺️](/maps/Nicaragua.Borders.png) [🏙️](/maps/Nicaragua.Cities.png) [〰️](/maps/Nicaragua.Rivers.png) [💧](/maps/Nicaragua.Lakes.png) [🏛️](/maps/Nicaragua.StatesProvinces.png) [🏖️](/maps/Nicaragua.Coastline.png) [🟩](/maps/Nicaragua.Land.png) [🌊](/maps/Nicaragua.Ocean.png) | 189 |
| [MapBundle.Niger](https://www.nuget.org/packages/MapBundle.Niger) | 50 KB | 90 KB | [🗺️](/maps/Niger.Borders.png) [🏙️](/maps/Niger.Cities.png) [〰️](/maps/Niger.Rivers.png) [💧](/maps/Niger.Lakes.png) [🏛️](/maps/Niger.StatesProvinces.png) | 34 |
| [MapBundle.Nigeria](https://www.nuget.org/packages/MapBundle.Nigeria) | 146 KB | 367 KB | [🗺️](/maps/Nigeria.Borders.png) [🏙️](/maps/Nigeria.Cities.png) [〰️](/maps/Nigeria.Rivers.png) [💧](/maps/Nigeria.Lakes.png) [🏛️](/maps/Nigeria.StatesProvinces.png) [🏖️](/maps/Nigeria.Coastline.png) [🟩](/maps/Nigeria.Land.png) [🌊](/maps/Nigeria.Ocean.png) | 154 |
| [MapBundle.Niue](https://www.nuget.org/packages/MapBundle.Niue) | 19 KB | 4 KB | [🗺️](/maps/Niue.Borders.png) [🏖️](/maps/Niue.Coastline.png) [🟩](/maps/Niue.Land.png) [🌊](/maps/Niue.Ocean.png) | 4 |
| [MapBundle.NorthKorea](https://www.nuget.org/packages/MapBundle.NorthKorea) | 220 KB | 372 KB | [🗺️](/maps/NorthKorea.Borders.png) [🏙️](/maps/NorthKorea.Cities.png) [〰️](/maps/NorthKorea.Rivers.png) [💧](/maps/NorthKorea.Lakes.png) [🏛️](/maps/NorthKorea.StatesProvinces.png) [🏖️](/maps/NorthKorea.Coastline.png) [🟩](/maps/NorthKorea.Land.png) [🌊](/maps/NorthKorea.Ocean.png) | 210 |
| [MapBundle.Norway](https://www.nuget.org/packages/MapBundle.Norway) | 13.7 MB | 19.5 MB | [🗺️](/maps/Norway.Borders.png) [🏙️](/maps/Norway.Cities.png) [〰️](/maps/Norway.Rivers.png) [💧](/maps/Norway.Lakes.png) [🏛️](/maps/Norway.StatesProvinces.png) [🏖️](/maps/Norway.Coastline.png) [🟩](/maps/Norway.Land.png) [🌊](/maps/Norway.Ocean.png) | 30,849 |
| [MapBundle.Pakistan](https://www.nuget.org/packages/MapBundle.Pakistan) | 244 KB | 509 KB | [🗺️](/maps/Pakistan.Borders.png) [🏙️](/maps/Pakistan.Cities.png) [〰️](/maps/Pakistan.Rivers.png) [💧](/maps/Pakistan.Lakes.png) [🏛️](/maps/Pakistan.StatesProvinces.png) [🏖️](/maps/Pakistan.Coastline.png) [🟩](/maps/Pakistan.Land.png) [🌊](/maps/Pakistan.Ocean.png) | 160 |
| [MapBundle.Palau](https://www.nuget.org/packages/MapBundle.Palau) | 50 KB | 69 KB | [🗺️](/maps/Palau.Borders.png) [🏙️](/maps/Palau.Cities.png) [🏛️](/maps/Palau.StatesProvinces.png) [🏖️](/maps/Palau.Coastline.png) [🟩](/maps/Palau.Land.png) [🌊](/maps/Palau.Ocean.png) | 94 |
| [MapBundle.Panama](https://www.nuget.org/packages/MapBundle.Panama) | 269 KB | 455 KB | [🗺️](/maps/Panama.Borders.png) [🏙️](/maps/Panama.Cities.png) [〰️](/maps/Panama.Rivers.png) [💧](/maps/Panama.Lakes.png) [🏛️](/maps/Panama.StatesProvinces.png) [🏖️](/maps/Panama.Coastline.png) [🟩](/maps/Panama.Land.png) [🌊](/maps/Panama.Ocean.png) | 261 |
| [MapBundle.PapuaNewGuinea](https://www.nuget.org/packages/MapBundle.PapuaNewGuinea) | 1.0 MB | 1.8 MB | [🗺️](/maps/PapuaNewGuinea.Borders.png) [🏙️](/maps/PapuaNewGuinea.Cities.png) [〰️](/maps/PapuaNewGuinea.Rivers.png) [💧](/maps/PapuaNewGuinea.Lakes.png) [🏛️](/maps/PapuaNewGuinea.StatesProvinces.png) [🏖️](/maps/PapuaNewGuinea.Coastline.png) [🟩](/maps/PapuaNewGuinea.Land.png) [🌊](/maps/PapuaNewGuinea.Ocean.png) | 2,088 |
| [MapBundle.Paraguay](https://www.nuget.org/packages/MapBundle.Paraguay) | 78 KB | 174 KB | [🗺️](/maps/Paraguay.Borders.png) [🏙️](/maps/Paraguay.Cities.png) [〰️](/maps/Paraguay.Rivers.png) [💧](/maps/Paraguay.Lakes.png) [🏛️](/maps/Paraguay.StatesProvinces.png) | 68 |
| [MapBundle.Peru](https://www.nuget.org/packages/MapBundle.Peru) | 394 KB | 765 KB | [🗺️](/maps/Peru.Borders.png) [🏙️](/maps/Peru.Cities.png) [〰️](/maps/Peru.Rivers.png) [💧](/maps/Peru.Lakes.png) [🏛️](/maps/Peru.StatesProvinces.png) [🏖️](/maps/Peru.Coastline.png) [🟩](/maps/Peru.Land.png) [🌊](/maps/Peru.Ocean.png) | 406 |
| [MapBundle.Philippines](https://www.nuget.org/packages/MapBundle.Philippines) | 1.4 MB | 2.5 MB | [🗺️](/maps/Philippines.Borders.png) [🏙️](/maps/Philippines.Cities.png) [〰️](/maps/Philippines.Rivers.png) [💧](/maps/Philippines.Lakes.png) [🏛️](/maps/Philippines.StatesProvinces.png) [🏖️](/maps/Philippines.Coastline.png) [🟩](/maps/Philippines.Land.png) [🌊](/maps/Philippines.Ocean.png) | 2,188 |
| [MapBundle.PitcairnIslands](https://www.nuget.org/packages/MapBundle.PitcairnIslands) | 76 KB | 140 KB | [🗺️](/maps/PitcairnIslands.Borders.png) [🏙️](/maps/PitcairnIslands.Cities.png) [🏖️](/maps/PitcairnIslands.Coastline.png) [🟩](/maps/PitcairnIslands.Land.png) [🌊](/maps/PitcairnIslands.Ocean.png) | 319 |
| [MapBundle.Poland](https://www.nuget.org/packages/MapBundle.Poland) | 141 KB | 264 KB | [🗺️](/maps/Poland.Borders.png) [🏙️](/maps/Poland.Cities.png) [〰️](/maps/Poland.Rivers.png) [💧](/maps/Poland.Lakes.png) [🏛️](/maps/Poland.StatesProvinces.png) [🏖️](/maps/Poland.Coastline.png) [🟩](/maps/Poland.Land.png) [🌊](/maps/Poland.Ocean.png) | 157 |
| [MapBundle.PolynesieFrancaise](https://www.nuget.org/packages/MapBundle.PolynesieFrancaise) | 129 KB | 213 KB | [🗺️](/maps/PolynesieFrancaise.Borders.png) [🏙️](/maps/PolynesieFrancaise.Cities.png) [🏛️](/maps/PolynesieFrancaise.StatesProvinces.png) [🏖️](/maps/PolynesieFrancaise.Coastline.png) [🟩](/maps/PolynesieFrancaise.Land.png) [🌊](/maps/PolynesieFrancaise.Ocean.png) | 193 |
| [MapBundle.Portugal](https://www.nuget.org/packages/MapBundle.Portugal) | 199 KB | 357 KB | [🗺️](/maps/Portugal.Borders.png) [🏙️](/maps/Portugal.Cities.png) [〰️](/maps/Portugal.Rivers.png) [🏛️](/maps/Portugal.StatesProvinces.png) [🏖️](/maps/Portugal.Coastline.png) [🟩](/maps/Portugal.Land.png) [🌊](/maps/Portugal.Ocean.png) | 177 |
| [MapBundle.Romania](https://www.nuget.org/packages/MapBundle.Romania) | 101 KB | 212 KB | [🗺️](/maps/Romania.Borders.png) [🏙️](/maps/Romania.Cities.png) [〰️](/maps/Romania.Rivers.png) [🏛️](/maps/Romania.StatesProvinces.png) [🏖️](/maps/Romania.Coastline.png) [🟩](/maps/Romania.Land.png) [🌊](/maps/Romania.Ocean.png) | 108 |
| [MapBundle.Rwanda](https://www.nuget.org/packages/MapBundle.Rwanda) | 39 KB | 60 KB | [🗺️](/maps/Rwanda.Borders.png) [🏙️](/maps/Rwanda.Cities.png) [〰️](/maps/Rwanda.Rivers.png) [💧](/maps/Rwanda.Lakes.png) [🏛️](/maps/Rwanda.StatesProvinces.png) | 22 |
| [MapBundle.SaintHelenaAscensionAndTristanDaCunha](https://www.nuget.org/packages/MapBundle.SaintHelenaAscensionAndTristanDaCunha) | 29 KB | 25 KB | [🗺️](/maps/SaintHelenaAscensionAndTristanDaCunha.Borders.png) [🏛️](/maps/SaintHelenaAscensionAndTristanDaCunha.StatesProvinces.png) [🏖️](/maps/SaintHelenaAscensionAndTristanDaCunha.Coastline.png) [🟩](/maps/SaintHelenaAscensionAndTristanDaCunha.Land.png) [🌊](/maps/SaintHelenaAscensionAndTristanDaCunha.Ocean.png) | 72 |
| [MapBundle.Samoa](https://www.nuget.org/packages/MapBundle.Samoa) | 35 KB | 32 KB | [🗺️](/maps/Samoa.Borders.png) [🏙️](/maps/Samoa.Cities.png) [🏖️](/maps/Samoa.Coastline.png) [🟩](/maps/Samoa.Land.png) [🌊](/maps/Samoa.Ocean.png) | 18 |
| [MapBundle.SaoTomeAndPrincipe](https://www.nuget.org/packages/MapBundle.SaoTomeAndPrincipe) | 31 KB | 23 KB | [🗺️](/maps/SaoTomeAndPrincipe.Borders.png) [🏙️](/maps/SaoTomeAndPrincipe.Cities.png) [🏛️](/maps/SaoTomeAndPrincipe.StatesProvinces.png) [🏖️](/maps/SaoTomeAndPrincipe.Coastline.png) [🟩](/maps/SaoTomeAndPrincipe.Land.png) [🌊](/maps/SaoTomeAndPrincipe.Ocean.png) | 15 |
| [MapBundle.SenegalAndGambia](https://www.nuget.org/packages/MapBundle.SenegalAndGambia) | 79 KB | 156 KB | [🗺️](/maps/SenegalAndGambia.Borders.png) [🏙️](/maps/SenegalAndGambia.Cities.png) [〰️](/maps/SenegalAndGambia.Rivers.png) [🏛️](/maps/SenegalAndGambia.StatesProvinces.png) [🏖️](/maps/SenegalAndGambia.Coastline.png) [🟩](/maps/SenegalAndGambia.Land.png) [🌊](/maps/SenegalAndGambia.Ocean.png) | 77 |
| [MapBundle.Serbia](https://www.nuget.org/packages/MapBundle.Serbia) | 58 KB | 123 KB | [🗺️](/maps/Serbia.Borders.png) [🏙️](/maps/Serbia.Cities.png) [〰️](/maps/Serbia.Rivers.png) [💧](/maps/Serbia.Lakes.png) [🏛️](/maps/Serbia.StatesProvinces.png) [🏖️](/maps/Serbia.Coastline.png) [🟩](/maps/Serbia.Land.png) [🌊](/maps/Serbia.Ocean.png) | 50 |
| [MapBundle.Seychelles](https://www.nuget.org/packages/MapBundle.Seychelles) | 49 KB | 63 KB | [🗺️](/maps/Seychelles.Borders.png) [🏙️](/maps/Seychelles.Cities.png) [🏖️](/maps/Seychelles.Coastline.png) [🟩](/maps/Seychelles.Land.png) [🌊](/maps/Seychelles.Ocean.png) | 125 |
| [MapBundle.SierraLeone](https://www.nuget.org/packages/MapBundle.SierraLeone) | 69 KB | 114 KB | [🗺️](/maps/SierraLeone.Borders.png) [🏙️](/maps/SierraLeone.Cities.png) [〰️](/maps/SierraLeone.Rivers.png) [🏛️](/maps/SierraLeone.StatesProvinces.png) [🏖️](/maps/SierraLeone.Coastline.png) [🟩](/maps/SierraLeone.Land.png) [🌊](/maps/SierraLeone.Ocean.png) | 94 |
| [MapBundle.Slovakia](https://www.nuget.org/packages/MapBundle.Slovakia) | 41 KB | 72 KB | [🗺️](/maps/Slovakia.Borders.png) [🏙️](/maps/Slovakia.Cities.png) [〰️](/maps/Slovakia.Rivers.png) [🏛️](/maps/Slovakia.StatesProvinces.png) | 20 |
| [MapBundle.Slovenia](https://www.nuget.org/packages/MapBundle.Slovenia) | 60 KB | 126 KB | [🗺️](/maps/Slovenia.Borders.png) [🏙️](/maps/Slovenia.Cities.png) [〰️](/maps/Slovenia.Rivers.png) [🏛️](/maps/Slovenia.StatesProvinces.png) [🏖️](/maps/Slovenia.Coastline.png) [🟩](/maps/Slovenia.Land.png) [🌊](/maps/Slovenia.Ocean.png) | 223 |
| [MapBundle.SolomonIslands](https://www.nuget.org/packages/MapBundle.SolomonIslands) | 390 KB | 705 KB | [🗺️](/maps/SolomonIslands.Borders.png) [🏙️](/maps/SolomonIslands.Cities.png) [🏛️](/maps/SolomonIslands.StatesProvinces.png) [🏖️](/maps/SolomonIslands.Coastline.png) [🟩](/maps/SolomonIslands.Land.png) [🌊](/maps/SolomonIslands.Ocean.png) | 727 |
| [MapBundle.Somalia](https://www.nuget.org/packages/MapBundle.Somalia) | 111 KB | 180 KB | [🗺️](/maps/Somalia.Borders.png) [🏙️](/maps/Somalia.Cities.png) [〰️](/maps/Somalia.Rivers.png) [💧](/maps/Somalia.Lakes.png) [🏛️](/maps/Somalia.StatesProvinces.png) [🏖️](/maps/Somalia.Coastline.png) [🟩](/maps/Somalia.Land.png) [🌊](/maps/Somalia.Ocean.png) | 109 |
| [MapBundle.SouthAfrica](https://www.nuget.org/packages/MapBundle.SouthAfrica) | 238 KB | 476 KB | [🗺️](/maps/SouthAfrica.Borders.png) [🏙️](/maps/SouthAfrica.Cities.png) [〰️](/maps/SouthAfrica.Rivers.png) [💧](/maps/SouthAfrica.Lakes.png) [🏛️](/maps/SouthAfrica.StatesProvinces.png) [🏖️](/maps/SouthAfrica.Coastline.png) [🟩](/maps/SouthAfrica.Land.png) [🌊](/maps/SouthAfrica.Ocean.png) | 218 |
| [MapBundle.SouthKorea](https://www.nuget.org/packages/MapBundle.SouthKorea) | 753 KB | 1.2 MB | [🗺️](/maps/SouthKorea.Borders.png) [🏙️](/maps/SouthKorea.Cities.png) [〰️](/maps/SouthKorea.Rivers.png) [🏛️](/maps/SouthKorea.StatesProvinces.png) [🏖️](/maps/SouthKorea.Coastline.png) [🟩](/maps/SouthKorea.Land.png) [🌊](/maps/SouthKorea.Ocean.png) | 1,292 |
| [MapBundle.SouthSudan](https://www.nuget.org/packages/MapBundle.SouthSudan) | 78 KB | 181 KB | [🗺️](/maps/SouthSudan.Borders.png) [🏙️](/maps/SouthSudan.Cities.png) [〰️](/maps/SouthSudan.Rivers.png) [💧](/maps/SouthSudan.Lakes.png) [🏛️](/maps/SouthSudan.StatesProvinces.png) | 46 |
| [MapBundle.Spain](https://www.nuget.org/packages/MapBundle.Spain) | 649 KB | 1.1 MB | [🗺️](/maps/Spain.Borders.png) [🏙️](/maps/Spain.Cities.png) [〰️](/maps/Spain.Rivers.png) [💧](/maps/Spain.Lakes.png) [🏛️](/maps/Spain.StatesProvinces.png) [🏖️](/maps/Spain.Coastline.png) [🟩](/maps/Spain.Land.png) [🌊](/maps/Spain.Ocean.png) | 354 |
| [MapBundle.SriLanka](https://www.nuget.org/packages/MapBundle.SriLanka) | 95 KB | 170 KB | [🗺️](/maps/SriLanka.Borders.png) [🏙️](/maps/SriLanka.Cities.png) [〰️](/maps/SriLanka.Rivers.png) [💧](/maps/SriLanka.Lakes.png) [🏛️](/maps/SriLanka.StatesProvinces.png) [🏖️](/maps/SriLanka.Coastline.png) [🟩](/maps/SriLanka.Land.png) [🌊](/maps/SriLanka.Ocean.png) | 126 |
| [MapBundle.Sudan](https://www.nuget.org/packages/MapBundle.Sudan) | 126 KB | 252 KB | [🗺️](/maps/Sudan.Borders.png) [🏙️](/maps/Sudan.Cities.png) [〰️](/maps/Sudan.Rivers.png) [💧](/maps/Sudan.Lakes.png) [🏛️](/maps/Sudan.StatesProvinces.png) [🏖️](/maps/Sudan.Coastline.png) [🟩](/maps/Sudan.Land.png) [🌊](/maps/Sudan.Ocean.png) | 121 |
| [MapBundle.Suriname](https://www.nuget.org/packages/MapBundle.Suriname) | 52 KB | 83 KB | [🗺️](/maps/Suriname.Borders.png) [🏙️](/maps/Suriname.Cities.png) [〰️](/maps/Suriname.Rivers.png) [💧](/maps/Suriname.Lakes.png) [🏛️](/maps/Suriname.StatesProvinces.png) [🏖️](/maps/Suriname.Coastline.png) [🟩](/maps/Suriname.Land.png) [🌊](/maps/Suriname.Ocean.png) | 33 |
| [MapBundle.Swaziland](https://www.nuget.org/packages/MapBundle.Swaziland) | 21 KB | 10 KB | [🗺️](/maps/Swaziland.Borders.png) [🏙️](/maps/Swaziland.Cities.png) [〰️](/maps/Swaziland.Rivers.png) [🏛️](/maps/Swaziland.StatesProvinces.png) | 13 |
| [MapBundle.Sweden](https://www.nuget.org/packages/MapBundle.Sweden) | 4.7 MB | 7.3 MB | [🗺️](/maps/Sweden.Borders.png) [🏙️](/maps/Sweden.Cities.png) [〰️](/maps/Sweden.Rivers.png) [💧](/maps/Sweden.Lakes.png) [🏛️](/maps/Sweden.StatesProvinces.png) [🏖️](/maps/Sweden.Coastline.png) [🟩](/maps/Sweden.Land.png) [🌊](/maps/Sweden.Ocean.png) | 17,040 |
| [MapBundle.Switzerland](https://www.nuget.org/packages/MapBundle.Switzerland) | 53 KB | 109 KB | [🗺️](/maps/Switzerland.Borders.png) [🏙️](/maps/Switzerland.Cities.png) [〰️](/maps/Switzerland.Rivers.png) [💧](/maps/Switzerland.Lakes.png) [🏛️](/maps/Switzerland.StatesProvinces.png) | 67 |
| [MapBundle.Syria](https://www.nuget.org/packages/MapBundle.Syria) | 64 KB | 101 KB | [🗺️](/maps/Syria.Borders.png) [🏙️](/maps/Syria.Cities.png) [〰️](/maps/Syria.Rivers.png) [💧](/maps/Syria.Lakes.png) [🏛️](/maps/Syria.StatesProvinces.png) [🏖️](/maps/Syria.Coastline.png) [🟩](/maps/Syria.Land.png) [🌊](/maps/Syria.Ocean.png) | 51 |
| [MapBundle.Taiwan](https://www.nuget.org/packages/MapBundle.Taiwan) | 220 KB | 310 KB | [🗺️](/maps/Taiwan.Borders.png) [🏙️](/maps/Taiwan.Cities.png) [〰️](/maps/Taiwan.Rivers.png) [🏛️](/maps/Taiwan.StatesProvinces.png) [🏖️](/maps/Taiwan.Coastline.png) [🟩](/maps/Taiwan.Land.png) [🌊](/maps/Taiwan.Ocean.png) | 335 |
| [MapBundle.Tajikistan](https://www.nuget.org/packages/MapBundle.Tajikistan) | 67 KB | 144 KB | [🗺️](/maps/Tajikistan.Borders.png) [🏙️](/maps/Tajikistan.Cities.png) [〰️](/maps/Tajikistan.Rivers.png) [💧](/maps/Tajikistan.Lakes.png) [🏛️](/maps/Tajikistan.StatesProvinces.png) | 25 |
| [MapBundle.Tanzania](https://www.nuget.org/packages/MapBundle.Tanzania) | 267 KB | 596 KB | [🗺️](/maps/Tanzania.Borders.png) [🏙️](/maps/Tanzania.Cities.png) [〰️](/maps/Tanzania.Rivers.png) [💧](/maps/Tanzania.Lakes.png) [🏛️](/maps/Tanzania.StatesProvinces.png) [🏖️](/maps/Tanzania.Coastline.png) [🟩](/maps/Tanzania.Land.png) [🌊](/maps/Tanzania.Ocean.png) | 249 |
| [MapBundle.Thailand](https://www.nuget.org/packages/MapBundle.Thailand) | 600 KB | 1.1 MB | [🗺️](/maps/Thailand.Borders.png) [🏙️](/maps/Thailand.Cities.png) [〰️](/maps/Thailand.Rivers.png) [💧](/maps/Thailand.Lakes.png) [🏛️](/maps/Thailand.StatesProvinces.png) [🏖️](/maps/Thailand.Coastline.png) [🟩](/maps/Thailand.Land.png) [🌊](/maps/Thailand.Ocean.png) | 1,347 |
| [MapBundle.Togo](https://www.nuget.org/packages/MapBundle.Togo) | 50 KB | 80 KB | [🗺️](/maps/Togo.Borders.png) [🏙️](/maps/Togo.Cities.png) [〰️](/maps/Togo.Rivers.png) [💧](/maps/Togo.Lakes.png) [🏛️](/maps/Togo.StatesProvinces.png) [🏖️](/maps/Togo.Coastline.png) [🟩](/maps/Togo.Land.png) [🌊](/maps/Togo.Ocean.png) | 21 |
| [MapBundle.Tokelau](https://www.nuget.org/packages/MapBundle.Tokelau) | 129 KB | 213 KB | [🗺️](/maps/Tokelau.Borders.png) [🏙️](/maps/Tokelau.Cities.png) [🏛️](/maps/Tokelau.StatesProvinces.png) [🏖️](/maps/Tokelau.Coastline.png) [🟩](/maps/Tokelau.Land.png) [🌊](/maps/Tokelau.Ocean.png) | 193 |
| [MapBundle.Tonga](https://www.nuget.org/packages/MapBundle.Tonga) | 53 KB | 73 KB | [🗺️](/maps/Tonga.Borders.png) [🏙️](/maps/Tonga.Cities.png) [🏛️](/maps/Tonga.StatesProvinces.png) [🏖️](/maps/Tonga.Coastline.png) [🟩](/maps/Tonga.Land.png) [🌊](/maps/Tonga.Ocean.png) | 146 |
| [MapBundle.Tunisia](https://www.nuget.org/packages/MapBundle.Tunisia) | 124 KB | 220 KB | [🗺️](/maps/Tunisia.Borders.png) [🏙️](/maps/Tunisia.Cities.png) [🏛️](/maps/Tunisia.StatesProvinces.png) [🏖️](/maps/Tunisia.Coastline.png) [🟩](/maps/Tunisia.Land.png) [🌊](/maps/Tunisia.Ocean.png) | 108 |
| [MapBundle.Turkey](https://www.nuget.org/packages/MapBundle.Turkey) | 582 KB | 1.0 MB | [🗺️](/maps/Turkey.Borders.png) [🏙️](/maps/Turkey.Cities.png) [〰️](/maps/Turkey.Rivers.png) [💧](/maps/Turkey.Lakes.png) [🏛️](/maps/Turkey.StatesProvinces.png) [🏖️](/maps/Turkey.Coastline.png) [🟩](/maps/Turkey.Land.png) [🌊](/maps/Turkey.Ocean.png) | 634 |
| [MapBundle.Turkmenistan](https://www.nuget.org/packages/MapBundle.Turkmenistan) | 104 KB | 171 KB | [🗺️](/maps/Turkmenistan.Borders.png) [🏙️](/maps/Turkmenistan.Cities.png) [〰️](/maps/Turkmenistan.Rivers.png) [💧](/maps/Turkmenistan.Lakes.png) [🏛️](/maps/Turkmenistan.StatesProvinces.png) [🏖️](/maps/Turkmenistan.Coastline.png) [🟩](/maps/Turkmenistan.Land.png) [🌊](/maps/Turkmenistan.Ocean.png) | 60 |
| [MapBundle.Tuvalu](https://www.nuget.org/packages/MapBundle.Tuvalu) | 28 KB | 24 KB | [🗺️](/maps/Tuvalu.Borders.png) [🏙️](/maps/Tuvalu.Cities.png) [🏛️](/maps/Tuvalu.StatesProvinces.png) [🏖️](/maps/Tuvalu.Coastline.png) [🟩](/maps/Tuvalu.Land.png) [🌊](/maps/Tuvalu.Ocean.png) | 48 |
| [MapBundle.Uganda](https://www.nuget.org/packages/MapBundle.Uganda) | 128 KB | 337 KB | [🗺️](/maps/Uganda.Borders.png) [🏙️](/maps/Uganda.Cities.png) [〰️](/maps/Uganda.Rivers.png) [💧](/maps/Uganda.Lakes.png) [🏛️](/maps/Uganda.StatesProvinces.png) | 175 |
| [MapBundle.Ukraine](https://www.nuget.org/packages/MapBundle.Ukraine) | 291 KB | 545 KB | [🗺️](/maps/Ukraine.Borders.png) [🏙️](/maps/Ukraine.Cities.png) [〰️](/maps/Ukraine.Rivers.png) [💧](/maps/Ukraine.Lakes.png) [🏛️](/maps/Ukraine.StatesProvinces.png) [🏖️](/maps/Ukraine.Coastline.png) [🟩](/maps/Ukraine.Land.png) [🌊](/maps/Ukraine.Ocean.png) | 229 |
| [MapBundle.UnitedKingdom](https://www.nuget.org/packages/MapBundle.UnitedKingdom) | 2.0 MB | 4.0 MB | [🗺️](/maps/UnitedKingdom.Borders.png) [🏙️](/maps/UnitedKingdom.Cities.png) [〰️](/maps/UnitedKingdom.Rivers.png) [💧](/maps/UnitedKingdom.Lakes.png) [🏛️](/maps/UnitedKingdom.StatesProvinces.png) [🏖️](/maps/UnitedKingdom.Coastline.png) [🟩](/maps/UnitedKingdom.Land.png) [🌊](/maps/UnitedKingdom.Ocean.png) | 1,677 |
| [MapBundle.Us](https://www.nuget.org/packages/MapBundle.Us) | 54.5 MB | 74.6 MB | [🗺️](/maps/Us.Borders.png) [🏙️](/maps/Us.Cities.png) [〰️](/maps/Us.Rivers.png) [💧](/maps/Us.Lakes.png) [🏛️](/maps/Us.StatesProvinces.png) [🏖️](/maps/Us.Coastline.png) [🟩](/maps/Us.Land.png) [🌊](/maps/Us.Ocean.png) | 113,978 |
| [MapBundle.Uruguay](https://www.nuget.org/packages/MapBundle.Uruguay) | 84 KB | 154 KB | [🗺️](/maps/Uruguay.Borders.png) [🏙️](/maps/Uruguay.Cities.png) [〰️](/maps/Uruguay.Rivers.png) [💧](/maps/Uruguay.Lakes.png) [🏛️](/maps/Uruguay.StatesProvinces.png) [🏖️](/maps/Uruguay.Coastline.png) [🟩](/maps/Uruguay.Land.png) [🌊](/maps/Uruguay.Ocean.png) | 78 |
| [MapBundle.Uzbekistan](https://www.nuget.org/packages/MapBundle.Uzbekistan) | 89 KB | 194 KB | [🗺️](/maps/Uzbekistan.Borders.png) [🏙️](/maps/Uzbekistan.Cities.png) [〰️](/maps/Uzbekistan.Rivers.png) [💧](/maps/Uzbekistan.Lakes.png) [🏛️](/maps/Uzbekistan.StatesProvinces.png) | 61 |
| [MapBundle.Vanuatu](https://www.nuget.org/packages/MapBundle.Vanuatu) | 129 KB | 213 KB | [🗺️](/maps/Vanuatu.Borders.png) [🏙️](/maps/Vanuatu.Cities.png) [🏛️](/maps/Vanuatu.StatesProvinces.png) [🏖️](/maps/Vanuatu.Coastline.png) [🟩](/maps/Vanuatu.Land.png) [🌊](/maps/Vanuatu.Ocean.png) | 193 |
| [MapBundle.Venezuela](https://www.nuget.org/packages/MapBundle.Venezuela) | 357 KB | 709 KB | [🗺️](/maps/Venezuela.Borders.png) [🏙️](/maps/Venezuela.Cities.png) [〰️](/maps/Venezuela.Rivers.png) [💧](/maps/Venezuela.Lakes.png) [🏛️](/maps/Venezuela.StatesProvinces.png) [🏖️](/maps/Venezuela.Coastline.png) [🟩](/maps/Venezuela.Land.png) [🌊](/maps/Venezuela.Ocean.png) | 374 |
| [MapBundle.Vietnam](https://www.nuget.org/packages/MapBundle.Vietnam) | 655 KB | 1.1 MB | [🗺️](/maps/Vietnam.Borders.png) [🏙️](/maps/Vietnam.Cities.png) [〰️](/maps/Vietnam.Rivers.png) [💧](/maps/Vietnam.Lakes.png) [🏛️](/maps/Vietnam.StatesProvinces.png) [🏖️](/maps/Vietnam.Coastline.png) [🟩](/maps/Vietnam.Land.png) [🌊](/maps/Vietnam.Ocean.png) | 1,077 |
| [MapBundle.WallisEtFutuna](https://www.nuget.org/packages/MapBundle.WallisEtFutuna) | 129 KB | 213 KB | [🗺️](/maps/WallisEtFutuna.Borders.png) [🏙️](/maps/WallisEtFutuna.Cities.png) [🏛️](/maps/WallisEtFutuna.StatesProvinces.png) [🏖️](/maps/WallisEtFutuna.Coastline.png) [🟩](/maps/WallisEtFutuna.Land.png) [🌊](/maps/WallisEtFutuna.Ocean.png) | 193 |
| [MapBundle.WesternSahara](https://www.nuget.org/packages/MapBundle.WesternSahara) | 49 KB | 56 KB | [🗺️](/maps/WesternSahara.Borders.png) [🏙️](/maps/WesternSahara.Cities.png) [🏖️](/maps/WesternSahara.Coastline.png) [🟩](/maps/WesternSahara.Land.png) [🌊](/maps/WesternSahara.Ocean.png) | 17 |
| [MapBundle.Yemen](https://www.nuget.org/packages/MapBundle.Yemen) | 181 KB | 297 KB | [🗺️](/maps/Yemen.Borders.png) [🏙️](/maps/Yemen.Cities.png) [🏛️](/maps/Yemen.StatesProvinces.png) [🏖️](/maps/Yemen.Coastline.png) [🟩](/maps/Yemen.Land.png) [🌊](/maps/Yemen.Ocean.png) | 307 |
| [MapBundle.Zambia](https://www.nuget.org/packages/MapBundle.Zambia) | 121 KB | 281 KB | [🗺️](/maps/Zambia.Borders.png) [🏙️](/maps/Zambia.Cities.png) [〰️](/maps/Zambia.Rivers.png) [💧](/maps/Zambia.Lakes.png) [🏛️](/maps/Zambia.StatesProvinces.png) | 75 |
| [MapBundle.Zimbabwe](https://www.nuget.org/packages/MapBundle.Zimbabwe) | 79 KB | 175 KB | [🗺️](/maps/Zimbabwe.Borders.png) [🏙️](/maps/Zimbabwe.Cities.png) [〰️](/maps/Zimbabwe.Rivers.png) [💧](/maps/Zimbabwe.Lakes.png) [🏛️](/maps/Zimbabwe.StatesProvinces.png) | 44 |
| [MapBundle.UsPuertoRico](https://www.nuget.org/packages/MapBundle.UsPuertoRico) | 59 KB | 72 KB | [🗺️](/maps/UsPuertoRico.Borders.png) [🏙️](/maps/UsPuertoRico.Cities.png) [🏖️](/maps/UsPuertoRico.Coastline.png) [🟩](/maps/UsPuertoRico.Land.png) [🌊](/maps/UsPuertoRico.Ocean.png) | 37 |
| [MapBundle.UsUsVirginIslands](https://www.nuget.org/packages/MapBundle.UsUsVirginIslands) | 38 KB | 36 KB | [🗺️](/maps/UsUsVirginIslands.Borders.png) [🏙️](/maps/UsUsVirginIslands.Cities.png) [🏖️](/maps/UsUsVirginIslands.Coastline.png) [🟩](/maps/UsUsVirginIslands.Land.png) [🌊](/maps/UsUsVirginIslands.Ocean.png) | 44 |
| [MapBundle.IleDeClipperton](https://www.nuget.org/packages/MapBundle.IleDeClipperton) | 129 KB | 213 KB | [🗺️](/maps/IleDeClipperton.Borders.png) [🏙️](/maps/IleDeClipperton.Cities.png) [🏛️](/maps/IleDeClipperton.StatesProvinces.png) [🏖️](/maps/IleDeClipperton.Coastline.png) [🟩](/maps/IleDeClipperton.Land.png) [🌊](/maps/IleDeClipperton.Ocean.png) | 193 |
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
