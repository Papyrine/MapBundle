# MapBundle

Bundled, offline map data for .NET apps — borders, cities, waterways and base layers — shipped as
[FlatGeobuf](https://flatgeobuf.org/) inside NuGet packages. Data is derived from
[Natural Earth](https://www.naturalearthdata.com/) (public domain), so it can be freely redistributed.

## Packages

| Package | Contents |
| --- | --- |
| `MapBundle` | Core runtime. Loads the bundled `.fgb` layers. No data of its own. |
| `MapBundle.World` | The whole world. |
| `MapBundle.[Region]` | A single region (for example `MapBundle.EuropeWestern`). |

Install the core package plus the area required:

```
dotnet add package MapBundle.EuropeWestern
```

A data package copies its FlatGeobuf files into a `maps/<Region>` folder beside the application at
build time; the `MapBundle` core reads them from there.

## Usage

```csharp
var map = Maps.Open().Load("EuropeWestern");

var borders = map.Borders;        // country polygons
var cities = map.Cities;          // populated places
var states = map.StatesProvinces; // admin-1 polygons
var rivers = map.Rivers;          // river and lake centerlines
// also: map.Lakes, map.UrbanAreas, map.MinorIslands, map.Coastline, map.Land, map.Ocean
```

Layers are read on demand and returned as GeoConvert `FeatureCollection`s (coordinates are WGS84
longitude/latitude).

## Layers

All from Natural Earth (the `MapLayer` enum):

- **Borders** — country polygons (`admin_0_countries`)
- **Cities** — populated places (`populated_places`)
- **StatesProvinces** — state/province polygons (`admin_1_states_provinces`)
- **UrbanAreas** — built-up area polygons (`urban_areas`)
- **Rivers** — river and lake centerlines (`rivers_lake_centerlines`)
- **Lakes** — lake polygons (`lakes`)
- **MinorIslands** — small island polygons (`minor_islands`)
- **Coastline** — coastlines (`coastline`)
- **Land** / **Ocean** — global base polygons (`land`, `ocean`)

Terrain, roads and railways are intentionally excluded.

## Accuracy and size

Accuracy is set when a package is built, from Natural Earth's three scales — `110m` (coarse), `50m`
(medium) and `10m` (fine, the default). Coarser scales produce smaller packages. To change accuracy,
rebuild the data packages at a different scale.

## Regions

Grouping follows the UN M49 geoscheme, with a few deliberate calls (Mexico in Northern America; Iran in
Western Asia; Middle East = Western Asia + Egypt + Iran; Russia kept whole in Eastern Europe). Regions
may overlap. See `src/Tests/Builder/Regions.cs` for the exact table.

## Building the data packages

The builder lives in the test project (`src/Tests/Builder/`) and runs as a gated test. Set
`MAPBUNDLE_BUILD=1` and run that one test; it downloads Natural Earth (cached locally), filters each
region, exports FlatGeobuf and writes the `.nupkg` files into `nugets/`:

```
MAPBUNDLE_BUILD=1 src/Tests/bin/Release/net10.0/Tests --treenode-filter "/*/*/PackageGeneration/Generate"
```

Settings (1:10m scale, output and cache folders) are fixed in `PackageBuilder`.

## License

MIT. Map data is from Natural Earth (public domain).
