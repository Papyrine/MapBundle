# MapBundle

Bundled, offline map data for .NET apps — borders, cities, waterways and base layers — shipped as
[FlatGeobuf](https://flatgeobuf.org/) inside NuGet packages. Data is derived from
[OpenStreetMap](https://www.openstreetmap.org/) and is available under the
[Open Database License (ODbL)](https://opendatacommons.org/licenses/odbl/).

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

A data package copies its FlatGeobuf files into a `maps/<Region>` folder beside the application at
build time; the `MapBundle` core reads them from there.

## Usage

```csharp
var map = Maps.Open().Load("Monaco");

var borders = map.Borders;        // country polygons
var cities = map.Cities;          // populated places
var states = map.StatesProvinces; // admin-1 polygons
var rivers = map.Rivers;          // rivers
// also: map.Lakes, map.Coastline, map.Land, map.Ocean
```

Layers are read on demand and returned as GeoConvert `FeatureCollection`s (coordinates are WGS84
longitude/latitude).

## Layers

The `MapLayer` enum (a layer is omitted from a package when the source has nothing for that region):

- **Borders** — country polygons (OSM admin level 2)
- **StatesProvinces** — state/province polygons (OSM admin level 4 / ISO 3166-2)
- **Cities** — populated places (`place=city`/`town`)
- **Rivers** — major waterways (`waterway=river`)
- **Lakes** — lake and reservoir polygons (`natural=water`, `reservoir`)
- **Coastline** — coastlines (derived from the land outlines)
- **Land** / **Ocean** — global base polygons

Roads, railways, buildings, land use and terrain are intentionally excluded.

## Data sources

- **Borders** and **StatesProvinces** come from
  [country-levels](https://github.com/hyperknot/country-levels) — OSM-derived, pre-simplified WGS84
  boundaries keyed by ISO code.
- **Cities**, **Rivers** and **Lakes** come from per-region
  [Geofabrik](https://download.geofabrik.de/) shapefile extracts of OpenStreetMap.
- **Land** and **Ocean** come from
  [osmdata.openstreetmap.de](https://osmdata.openstreetmap.de/); **Coastline** is derived from the land
  polygons.

## Regions

The region tree follows [Geofabrik's download index](https://download.geofabrik.de/index-v1.json):
the continents and their countries. `MapBundle.World` merges every continent. Sub-country levels (US
states, German Bundesländer) are not published. See `src/Tests/Builder/Regions.cs`.

## Building the data packages

The builder lives in the test project (`src/Tests/Builder/`) and runs as a gated test. It downloads
the OSM sources (cached locally by [Replicant](https://github.com/SimonCropp/Replicant)), filters and
simplifies each region, exports FlatGeobuf and writes the `.nupkg` files into `nugets/`:

```
MAPBUNDLE_BUILD=1 src/Tests/bin/Release/net10.0/Tests --treenode-filter "/*/*/PackageGeneration/Generate"
```

To validate the pipeline on a single region (default `monaco`) without building the whole tree:

```
MAPBUNDLE_SLICE=monaco src/Tests/bin/Debug/net10.0/Tests --treenode-filter "/*/*/PackageGeneration/Slice"
```

Geometry simplification and EPSG:3857→4326 reprojection use
[NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite), a **build-only** dependency;
the shipped `MapBundle` core depends only on GeoConvert.

## License

The `MapBundle` core library is MIT. The data packages contain OpenStreetMap data and are licensed
under the [ODbL](https://opendatacommons.org/licenses/odbl/) — © OpenStreetMap contributors.
