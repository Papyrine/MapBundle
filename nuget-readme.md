# MapBundle

Bundled, offline map data for .NET apps — borders, cities, waterways and base layers — shipped as [FlatGeobuf](https://flatgeobuf.org/) inside NuGet packages. Data is derived from [OpenStreetMap](https://www.openstreetmap.org/) under the [ODbL](https://opendatacommons.org/licenses/odbl/); cities, rivers and lakes come from [Natural Earth](https://www.naturalearthdata.com/) (public domain).


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

A data package copies its FlatGeobuf files into a `maps/<Region>` folder beside the app at build time, and the `MapBundle` core reads them from there. Layers can instead be [converted to another format and/or rendered to an image at build time](https://github.com/Papyrine/MapBundle#build-time-format-conversion-and-images).


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


## Layers

🗺️ Borders · 🏛️ StatesProvinces · 🏙️ Cities · 〰️ Rivers · 💧 Lakes · 🏖️ Coastline · 🟩 Land · 🌊 Ocean

A layer is omitted from a package when the source has nothing for that region (for example **Ocean** and **Coastline** for landlocked countries). Roads, railways, buildings, land use and terrain are intentionally excluded.


## More

Full documentation, the complete per-region package list, data sources and licensing details are on the project page:

<https://github.com/Papyrine/MapBundle>


## License

The `MapBundle` core library is MIT. The data packages contain OpenStreetMap data and are licensed under the [ODbL](https://opendatacommons.org/licenses/odbl/) — © OpenStreetMap contributors.
