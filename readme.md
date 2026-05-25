# MapBundle

Bundled, offline map data for .NET apps — country borders, major cities, and major waterways — shipped
as [FlatGeobuf](https://flatgeobuf.org/) inside NuGet packages. Data is derived from
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

var borders = map.Borders; // FeatureCollection of country polygons
var cities = map.Cities;   // populated places
var rivers = map.Rivers;   // river and lake centerlines
var lakes = map.Lakes;     // lake polygons
```

Layers are read on demand and returned as GeoConvert `FeatureCollection`s (coordinates are WGS84
longitude/latitude).

## Layers

Each package contains up to four layers, all from Natural Earth:

- **Borders** — `admin_0_countries`
- **Cities** — `populated_places`
- **Rivers** — `rivers_lake_centerlines`
- **Lakes** — `lakes`

Terrain, roads and minor features are intentionally excluded.

## Accuracy and size

Accuracy is set when a package is built, from Natural Earth's three scales — `110m` (coarse), `50m`
(medium) and `10m` (fine, the default). Coarser scales produce smaller packages. To change accuracy,
rebuild the data packages at a different scale.

## Regions

Grouping follows the UN M49 geoscheme, with a few deliberate calls (Mexico in Northern America; Iran in
Western Asia; Middle East = Western Asia + Egypt + Iran; Russia kept whole in Eastern Europe). Regions
may overlap. See `src/MapBundle.Builder/Regions.cs` for the exact table.

## Building the data packages

The data packages are produced by the `MapBundle.Builder` tool, which downloads Natural Earth (cached
locally), filters each region, exports FlatGeobuf and writes the `.nupkg` files:

```
dotnet run --project src/MapBundle.Builder -- --scale 10m --output nugets --cache .cache
```

## License

MIT. Map data is from Natural Earth (public domain).
