# MapBundle

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

A data package copies its FlatGeobuf files into a `maps/<Region>` folder beside the application at build time; the `MapBundle` core reads them from there.


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
| [MapBundle.World](https://www.nuget.org/packages/MapBundle.World) | 93.5 MB | 167.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 157,727 |

### Continents

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Africa](https://www.nuget.org/packages/MapBundle.Africa) | 5.4 MB | 12.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 6,864 |
| [MapBundle.Asia](https://www.nuget.org/packages/MapBundle.Asia) | 15.1 MB | 29.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 23,219 |
| [MapBundle.AustraliaOceania](https://www.nuget.org/packages/MapBundle.AustraliaOceania) | 16.8 MB | 25.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 32,773 |
| [MapBundle.CentralAmerica](https://www.nuget.org/packages/MapBundle.CentralAmerica) | 2.6 MB | 4.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,762 |
| [MapBundle.Europe](https://www.nuget.org/packages/MapBundle.Europe) | 71.3 MB | 101.9 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 141,399 |
| [MapBundle.NorthAmerica](https://www.nuget.org/packages/MapBundle.NorthAmerica) | 69.0 MB | 101.4 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 131,303 |
| [MapBundle.Russia](https://www.nuget.org/packages/MapBundle.Russia) | 45.7 MB | 62.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 94,266 |
| [MapBundle.SouthAmerica](https://www.nuget.org/packages/MapBundle.SouthAmerica) | 8.2 MB | 15.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 12,256 |

### Countries

| Bundle | NuGet | Data | Layers | Features |
| --- | --: | --: | --: | --: |
| [MapBundle.Afghanistan](https://www.nuget.org/packages/MapBundle.Afghanistan) | 119 KB | 282 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 82 |
| [MapBundle.Albania](https://www.nuget.org/packages/MapBundle.Albania) | 69 KB | 100 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 69 |
| [MapBundle.Algeria](https://www.nuget.org/packages/MapBundle.Algeria) | 264 KB | 426 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 203 |
| [MapBundle.AmericanOceania](https://www.nuget.org/packages/MapBundle.AmericanOceania) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Andorra](https://www.nuget.org/packages/MapBundle.Andorra) | 25 KB | 7 KB | 🗺️ 🏙️ 🏛️ 🟩 | 10 |
| [MapBundle.Angola](https://www.nuget.org/packages/MapBundle.Angola) | 156 KB | 336 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 101 |
| [MapBundle.Argentina](https://www.nuget.org/packages/MapBundle.Argentina) | 2.2 MB | 3.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 3,851 |
| [MapBundle.Armenia](https://www.nuget.org/packages/MapBundle.Armenia) | 45 KB | 64 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 28 |
| [MapBundle.Australia](https://www.nuget.org/packages/MapBundle.Australia) | 3.3 MB | 5.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,831 |
| [MapBundle.Austria](https://www.nuget.org/packages/MapBundle.Austria) | 63 KB | 123 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 30 |
| [MapBundle.Azerbaijan](https://www.nuget.org/packages/MapBundle.Azerbaijan) | 106 KB | 200 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 137 |
| [MapBundle.Bahamas](https://www.nuget.org/packages/MapBundle.Bahamas) | 749 KB | 1.2 MB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 1,825 |
| [MapBundle.Bangladesh](https://www.nuget.org/packages/MapBundle.Bangladesh) | 351 KB | 660 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 635 |
| [MapBundle.Belarus](https://www.nuget.org/packages/MapBundle.Belarus) | 73 KB | 150 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 34 |
| [MapBundle.Belgium](https://www.nuget.org/packages/MapBundle.Belgium) | 88 KB | 127 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 43 |
| [MapBundle.Belize](https://www.nuget.org/packages/MapBundle.Belize) | 106 KB | 154 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 161 |
| [MapBundle.Benin](https://www.nuget.org/packages/MapBundle.Benin) | 48 KB | 76 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 31 |
| [MapBundle.Bhutan](https://www.nuget.org/packages/MapBundle.Bhutan) | 39 KB | 51 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 27 |
| [MapBundle.Bolivia](https://www.nuget.org/packages/MapBundle.Bolivia) | 120 KB | 283 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 104 |
| [MapBundle.BosniaHerzegovina](https://www.nuget.org/packages/MapBundle.BosniaHerzegovina) | 139 KB | 196 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 183 |
| [MapBundle.Botswana](https://www.nuget.org/packages/MapBundle.Botswana) | 70 KB | 136 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 54 |
| [MapBundle.Brazil](https://www.nuget.org/packages/MapBundle.Brazil) | 1.4 MB | 2.6 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,602 |
| [MapBundle.Bulgaria](https://www.nuget.org/packages/MapBundle.Bulgaria) | 82 KB | 144 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.BurkinaFaso](https://www.nuget.org/packages/MapBundle.BurkinaFaso) | 87 KB | 215 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 100 |
| [MapBundle.Burundi](https://www.nuget.org/packages/MapBundle.Burundi) | 47 KB | 80 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 40 |
| [MapBundle.Cambodia](https://www.nuget.org/packages/MapBundle.Cambodia) | 126 KB | 228 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 179 |
| [MapBundle.Cameroon](https://www.nuget.org/packages/MapBundle.Cameroon) | 125 KB | 267 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 87 |
| [MapBundle.Canada](https://www.nuget.org/packages/MapBundle.Canada) | 23.9 MB | 37.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 43,817 |
| [MapBundle.CapeVerde](https://www.nuget.org/packages/MapBundle.CapeVerde) | 68 KB | 84 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.CentralAfricanRepublic](https://www.nuget.org/packages/MapBundle.CentralAfricanRepublic) | 101 KB | 239 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 60 |
| [MapBundle.Chad](https://www.nuget.org/packages/MapBundle.Chad) | 74 KB | 149 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 56 |
| [MapBundle.Chile](https://www.nuget.org/packages/MapBundle.Chile) | 4.4 MB | 7.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 7,276 |
| [MapBundle.China](https://www.nuget.org/packages/MapBundle.China) | 4.9 MB | 7.8 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 8,841 |
| [MapBundle.Colombia](https://www.nuget.org/packages/MapBundle.Colombia) | 700 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 760 |
| [MapBundle.CongoDemocraticRepublic](https://www.nuget.org/packages/MapBundle.CongoDemocraticRepublic) | 287 KB | 709 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 201 |
| [MapBundle.CongoBrazzaville](https://www.nuget.org/packages/MapBundle.CongoBrazzaville) | 90 KB | 188 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.CookIslands](https://www.nuget.org/packages/MapBundle.CookIslands) | 38 KB | 34 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 92 |
| [MapBundle.CostaRica](https://www.nuget.org/packages/MapBundle.CostaRica) | 107 KB | 155 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 80 |
| [MapBundle.Croatia](https://www.nuget.org/packages/MapBundle.Croatia) | 362 KB | 618 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 448 |
| [MapBundle.Cuba](https://www.nuget.org/packages/MapBundle.Cuba) | 545 KB | 971 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 1,132 |
| [MapBundle.Cyprus](https://www.nuget.org/packages/MapBundle.Cyprus) | 53 KB | 57 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 15 |
| [MapBundle.CzechRepublic](https://www.nuget.org/packages/MapBundle.CzechRepublic) | 73 KB | 177 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 112 |
| [MapBundle.Denmark](https://www.nuget.org/packages/MapBundle.Denmark) | 495 KB | 744 KB | 🗺️ 🏙️ 💧 🏛️ 🏖️ 🟩 🌊 | 619 |
| [MapBundle.Djibouti](https://www.nuget.org/packages/MapBundle.Djibouti) | 54 KB | 81 KB | 🗺️ 🏙️ 💧 🏛️ 🏖️ 🟩 🌊 | 31 |
| [MapBundle.EastTimor](https://www.nuget.org/packages/MapBundle.EastTimor) | 61 KB | 70 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 48 |
| [MapBundle.Ecuador](https://www.nuget.org/packages/MapBundle.Ecuador) | 329 KB | 564 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 390 |
| [MapBundle.Egypt](https://www.nuget.org/packages/MapBundle.Egypt) | 282 KB | 556 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 271 |
| [MapBundle.ElSalvador](https://www.nuget.org/packages/MapBundle.ElSalvador) | 92 KB | 120 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 96 |
| [MapBundle.EquatorialGuinea](https://www.nuget.org/packages/MapBundle.EquatorialGuinea) | 72 KB | 91 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Eritrea](https://www.nuget.org/packages/MapBundle.Eritrea) | 225 KB | 371 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 534 |
| [MapBundle.Estonia](https://www.nuget.org/packages/MapBundle.Estonia) | 238 KB | 343 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 247 |
| [MapBundle.Ethiopia](https://www.nuget.org/packages/MapBundle.Ethiopia) | 171 KB | 302 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 218 |
| [MapBundle.FaroeIslands](https://www.nuget.org/packages/MapBundle.FaroeIslands) | 81 KB | 88 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 47 |
| [MapBundle.Fiji](https://www.nuget.org/packages/MapBundle.Fiji) | 1.9 MB | 2.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 3,963 |
| [MapBundle.Finland](https://www.nuget.org/packages/MapBundle.Finland) | 2.6 MB | 4.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 9,587 |
| [MapBundle.France](https://www.nuget.org/packages/MapBundle.France) | 27.2 MB | 36.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 48,480 |
| [MapBundle.GccStates](https://www.nuget.org/packages/MapBundle.GccStates) | 605 KB | 937 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 788 |
| [MapBundle.Gabon](https://www.nuget.org/packages/MapBundle.Gabon) | 87 KB | 159 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Georgia](https://www.nuget.org/packages/MapBundle.Georgia) | 61 KB | 100 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 29 |
| [MapBundle.Germany](https://www.nuget.org/packages/MapBundle.Germany) | 392 KB | 654 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 445 |
| [MapBundle.Ghana](https://www.nuget.org/packages/MapBundle.Ghana) | 90 KB | 155 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 45 |
| [MapBundle.Greece](https://www.nuget.org/packages/MapBundle.Greece) | 924 KB | 1.5 MB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 961 |
| [MapBundle.Greenland](https://www.nuget.org/packages/MapBundle.Greenland) | 9.4 MB | 13.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 17,455 |
| [MapBundle.Guatemala](https://www.nuget.org/packages/MapBundle.Guatemala) | 73 KB | 104 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 85 |
| [MapBundle.Guinea](https://www.nuget.org/packages/MapBundle.Guinea) | 199 KB | 385 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 242 |
| [MapBundle.GuineaBissau](https://www.nuget.org/packages/MapBundle.GuineaBissau) | 180 KB | 273 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 199 |
| [MapBundle.Guyana](https://www.nuget.org/packages/MapBundle.Guyana) | 74 KB | 139 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 35 |
| [MapBundle.HaitiAndDomrep](https://www.nuget.org/packages/MapBundle.HaitiAndDomrep) | 195 KB | 401 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 145 |
| [MapBundle.Honduras](https://www.nuget.org/packages/MapBundle.Honduras) | 227 KB | 338 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 267 |
| [MapBundle.Hungary](https://www.nuget.org/packages/MapBundle.Hungary) | 61 KB | 114 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 59 |
| [MapBundle.Iceland](https://www.nuget.org/packages/MapBundle.Iceland) | 466 KB | 693 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 516 |
| [MapBundle.India](https://www.nuget.org/packages/MapBundle.India) | 1.2 MB | 2.4 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,103 |
| [MapBundle.Indonesia](https://www.nuget.org/packages/MapBundle.Indonesia) | 3.6 MB | 6.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 6,052 |
| [MapBundle.Iran](https://www.nuget.org/packages/MapBundle.Iran) | 549 KB | 892 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 575 |
| [MapBundle.Iraq](https://www.nuget.org/packages/MapBundle.Iraq) | 99 KB | 179 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 91 |
| [MapBundle.IrelandAndNorthernIreland](https://www.nuget.org/packages/MapBundle.IrelandAndNorthernIreland) | 494 KB | 786 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 531 |
| [MapBundle.IsraelAndPalestine](https://www.nuget.org/packages/MapBundle.IsraelAndPalestine) | 52 KB | 75 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 43 |
| [MapBundle.Italy](https://www.nuget.org/packages/MapBundle.Italy) | 771 KB | 1.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 818 |
| [MapBundle.IvoryCoast](https://www.nuget.org/packages/MapBundle.IvoryCoast) | 99 KB | 215 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 61 |
| [MapBundle.Jamaica](https://www.nuget.org/packages/MapBundle.Jamaica) | 58 KB | 68 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 34 |
| [MapBundle.Japan](https://www.nuget.org/packages/MapBundle.Japan) | 2.1 MB | 3.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,956 |
| [MapBundle.Jordan](https://www.nuget.org/packages/MapBundle.Jordan) | 42 KB | 46 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 32 |
| [MapBundle.Kazakhstan](https://www.nuget.org/packages/MapBundle.Kazakhstan) | 383 KB | 702 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 489 |
| [MapBundle.Kenya](https://www.nuget.org/packages/MapBundle.Kenya) | 148 KB | 285 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 171 |
| [MapBundle.Kiribati](https://www.nuget.org/packages/MapBundle.Kiribati) | 4.7 MB | 6.2 MB | 🗺️ 🏙️ 〰️ 💧 🏖️ 🟩 🌊 | 10,349 |
| [MapBundle.Kyrgyzstan](https://www.nuget.org/packages/MapBundle.Kyrgyzstan) | 81 KB | 167 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 36 |
| [MapBundle.Laos](https://www.nuget.org/packages/MapBundle.Laos) | 200 KB | 378 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 288 |
| [MapBundle.Latvia](https://www.nuget.org/packages/MapBundle.Latvia) | 82 KB | 146 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 141 |
| [MapBundle.Lebanon](https://www.nuget.org/packages/MapBundle.Lebanon) | 44 KB | 45 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 21 |
| [MapBundle.Lesotho](https://www.nuget.org/packages/MapBundle.Lesotho) | 42 KB | 57 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 23 |
| [MapBundle.Liberia](https://www.nuget.org/packages/MapBundle.Liberia) | 66 KB | 119 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 39 |
| [MapBundle.Libya](https://www.nuget.org/packages/MapBundle.Libya) | 101 KB | 163 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 73 |
| [MapBundle.Liechtenstein](https://www.nuget.org/packages/MapBundle.Liechtenstein) | 25 KB | 8 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 15 |
| [MapBundle.Lithuania](https://www.nuget.org/packages/MapBundle.Lithuania) | 70 KB | 140 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 88 |
| [MapBundle.Luxembourg](https://www.nuget.org/packages/MapBundle.Luxembourg) | 30 KB | 21 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 18 |
| [MapBundle.Macedonia](https://www.nuget.org/packages/MapBundle.Macedonia) | 43 KB | 70 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 79 |
| [MapBundle.Madagascar](https://www.nuget.org/packages/MapBundle.Madagascar) | 315 KB | 592 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 325 |
| [MapBundle.Malawi](https://www.nuget.org/packages/MapBundle.Malawi) | 89 KB | 209 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 62 |
| [MapBundle.MalaysiaSingaporeBrunei](https://www.nuget.org/packages/MapBundle.MalaysiaSingaporeBrunei) | 522 KB | 891 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 1,185 |
| [MapBundle.Maldives](https://www.nuget.org/packages/MapBundle.Maldives) | 104 KB | 220 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 465 |
| [MapBundle.Mali](https://www.nuget.org/packages/MapBundle.Mali) | 99 KB | 224 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 58 |
| [MapBundle.Malta](https://www.nuget.org/packages/MapBundle.Malta) | 37 KB | 34 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 79 |
| [MapBundle.MarshallIslands](https://www.nuget.org/packages/MapBundle.MarshallIslands) | 81 KB | 140 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 319 |
| [MapBundle.Mauritania](https://www.nuget.org/packages/MapBundle.Mauritania) | 90 KB | 139 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 76 |
| [MapBundle.Mauritius](https://www.nuget.org/packages/MapBundle.Mauritius) | 49 KB | 52 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 72 |
| [MapBundle.Mexico](https://www.nuget.org/packages/MapBundle.Mexico) | 1.5 MB | 2.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,388 |
| [MapBundle.Micronesia](https://www.nuget.org/packages/MapBundle.Micronesia) | 71 KB | 104 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 229 |
| [MapBundle.Moldova](https://www.nuget.org/packages/MapBundle.Moldova) | 54 KB | 93 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 49 |
| [MapBundle.Monaco](https://www.nuget.org/packages/MapBundle.Monaco) | 23 KB | 2 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 5 |
| [MapBundle.Mongolia](https://www.nuget.org/packages/MapBundle.Mongolia) | 126 KB | 264 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 94 |
| [MapBundle.Montenegro](https://www.nuget.org/packages/MapBundle.Montenegro) | 53 KB | 71 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 36 |
| [MapBundle.Morocco](https://www.nuget.org/packages/MapBundle.Morocco) | 196 KB | 321 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 137 |
| [MapBundle.Mozambique](https://www.nuget.org/packages/MapBundle.Mozambique) | 224 KB | 419 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 194 |
| [MapBundle.Myanmar](https://www.nuget.org/packages/MapBundle.Myanmar) | 841 KB | 1.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,621 |
| [MapBundle.Namibia](https://www.nuget.org/packages/MapBundle.Namibia) | 104 KB | 198 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 73 |
| [MapBundle.Nauru](https://www.nuget.org/packages/MapBundle.Nauru) | 24 KB | 5 KB | 🗺️ 🏛️ 🏖️ 🟩 🌊 | 18 |
| [MapBundle.Nepal](https://www.nuget.org/packages/MapBundle.Nepal) | 58 KB | 107 KB | 🗺️ 🏙️ 〰️ 🟩 | 25 |
| [MapBundle.Netherlands](https://www.nuget.org/packages/MapBundle.Netherlands) | 3.8 MB | 4.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 5,090 |
| [MapBundle.NewCaledonia](https://www.nuget.org/packages/MapBundle.NewCaledonia) | 161 KB | 250 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 200 |
| [MapBundle.NewZealand](https://www.nuget.org/packages/MapBundle.NewZealand) | 2.2 MB | 3.2 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 4,046 |
| [MapBundle.Nicaragua](https://www.nuget.org/packages/MapBundle.Nicaragua) | 187 KB | 265 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 189 |
| [MapBundle.Niger](https://www.nuget.org/packages/MapBundle.Niger) | 55 KB | 90 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 35 |
| [MapBundle.Nigeria](https://www.nuget.org/packages/MapBundle.Nigeria) | 151 KB | 366 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 154 |
| [MapBundle.Niue](https://www.nuget.org/packages/MapBundle.Niue) | 24 KB | 4 KB | 🗺️ 🏖️ 🟩 🌊 | 4 |
| [MapBundle.NorthKorea](https://www.nuget.org/packages/MapBundle.NorthKorea) | 225 KB | 372 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 210 |
| [MapBundle.Norway](https://www.nuget.org/packages/MapBundle.Norway) | 13.7 MB | 19.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 30,844 |
| [MapBundle.Pakistan](https://www.nuget.org/packages/MapBundle.Pakistan) | 248 KB | 509 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 160 |
| [MapBundle.Palau](https://www.nuget.org/packages/MapBundle.Palau) | 55 KB | 69 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 94 |
| [MapBundle.Panama](https://www.nuget.org/packages/MapBundle.Panama) | 274 KB | 455 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 261 |
| [MapBundle.PapuaNewGuinea](https://www.nuget.org/packages/MapBundle.PapuaNewGuinea) | 1.0 MB | 1.8 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,090 |
| [MapBundle.Paraguay](https://www.nuget.org/packages/MapBundle.Paraguay) | 84 KB | 174 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 69 |
| [MapBundle.Peru](https://www.nuget.org/packages/MapBundle.Peru) | 399 KB | 764 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 406 |
| [MapBundle.Philippines](https://www.nuget.org/packages/MapBundle.Philippines) | 1.4 MB | 2.5 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 2,186 |
| [MapBundle.PitcairnIslands](https://www.nuget.org/packages/MapBundle.PitcairnIslands) | 81 KB | 140 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 319 |
| [MapBundle.Poland](https://www.nuget.org/packages/MapBundle.Poland) | 145 KB | 263 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 155 |
| [MapBundle.PolynesieFrancaise](https://www.nuget.org/packages/MapBundle.PolynesieFrancaise) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Portugal](https://www.nuget.org/packages/MapBundle.Portugal) | 203 KB | 357 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 177 |
| [MapBundle.Romania](https://www.nuget.org/packages/MapBundle.Romania) | 105 KB | 212 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 108 |
| [MapBundle.Rwanda](https://www.nuget.org/packages/MapBundle.Rwanda) | 44 KB | 60 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 23 |
| [MapBundle.SaintHelenaAscensionAndTristanDaCunha](https://www.nuget.org/packages/MapBundle.SaintHelenaAscensionAndTristanDaCunha) | 33 KB | 25 KB | 🗺️ 🏛️ 🏖️ 🟩 🌊 | 72 |
| [MapBundle.Samoa](https://www.nuget.org/packages/MapBundle.Samoa) | 40 KB | 32 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 18 |
| [MapBundle.SaoTomeAndPrincipe](https://www.nuget.org/packages/MapBundle.SaoTomeAndPrincipe) | 36 KB | 22 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 15 |
| [MapBundle.SenegalAndGambia](https://www.nuget.org/packages/MapBundle.SenegalAndGambia) | 83 KB | 156 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 77 |
| [MapBundle.Serbia](https://www.nuget.org/packages/MapBundle.Serbia) | 63 KB | 122 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 50 |
| [MapBundle.Seychelles](https://www.nuget.org/packages/MapBundle.Seychelles) | 54 KB | 63 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 125 |
| [MapBundle.SierraLeone](https://www.nuget.org/packages/MapBundle.SierraLeone) | 73 KB | 114 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 94 |
| [MapBundle.Slovakia](https://www.nuget.org/packages/MapBundle.Slovakia) | 46 KB | 72 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 21 |
| [MapBundle.Slovenia](https://www.nuget.org/packages/MapBundle.Slovenia) | 65 KB | 126 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 223 |
| [MapBundle.SolomonIslands](https://www.nuget.org/packages/MapBundle.SolomonIslands) | 395 KB | 705 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 727 |
| [MapBundle.Somalia](https://www.nuget.org/packages/MapBundle.Somalia) | 116 KB | 179 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 109 |
| [MapBundle.SouthAfrica](https://www.nuget.org/packages/MapBundle.SouthAfrica) | 243 KB | 475 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 218 |
| [MapBundle.SouthKorea](https://www.nuget.org/packages/MapBundle.SouthKorea) | 758 KB | 1.2 MB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 1,292 |
| [MapBundle.SouthSudan](https://www.nuget.org/packages/MapBundle.SouthSudan) | 83 KB | 181 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 47 |
| [MapBundle.Spain](https://www.nuget.org/packages/MapBundle.Spain) | 655 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 354 |
| [MapBundle.SriLanka](https://www.nuget.org/packages/MapBundle.SriLanka) | 100 KB | 170 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 126 |
| [MapBundle.Sudan](https://www.nuget.org/packages/MapBundle.Sudan) | 131 KB | 252 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 121 |
| [MapBundle.Suriname](https://www.nuget.org/packages/MapBundle.Suriname) | 57 KB | 83 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 33 |
| [MapBundle.Swaziland](https://www.nuget.org/packages/MapBundle.Swaziland) | 26 KB | 10 KB | 🗺️ 🏙️ 〰️ 🏛️ 🟩 | 14 |
| [MapBundle.Sweden](https://www.nuget.org/packages/MapBundle.Sweden) | 4.8 MB | 7.3 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 17,038 |
| [MapBundle.Switzerland](https://www.nuget.org/packages/MapBundle.Switzerland) | 58 KB | 109 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 68 |
| [MapBundle.Syria](https://www.nuget.org/packages/MapBundle.Syria) | 68 KB | 101 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 51 |
| [MapBundle.Taiwan](https://www.nuget.org/packages/MapBundle.Taiwan) | 225 KB | 310 KB | 🗺️ 🏙️ 〰️ 🏛️ 🏖️ 🟩 🌊 | 335 |
| [MapBundle.Tajikistan](https://www.nuget.org/packages/MapBundle.Tajikistan) | 72 KB | 144 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 26 |
| [MapBundle.Tanzania](https://www.nuget.org/packages/MapBundle.Tanzania) | 271 KB | 595 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 249 |
| [MapBundle.Thailand](https://www.nuget.org/packages/MapBundle.Thailand) | 607 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,347 |
| [MapBundle.Togo](https://www.nuget.org/packages/MapBundle.Togo) | 55 KB | 79 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 21 |
| [MapBundle.Tokelau](https://www.nuget.org/packages/MapBundle.Tokelau) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Tonga](https://www.nuget.org/packages/MapBundle.Tonga) | 57 KB | 73 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 146 |
| [MapBundle.Tunisia](https://www.nuget.org/packages/MapBundle.Tunisia) | 129 KB | 220 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 108 |
| [MapBundle.Turkey](https://www.nuget.org/packages/MapBundle.Turkey) | 587 KB | 1.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 634 |
| [MapBundle.Turkmenistan](https://www.nuget.org/packages/MapBundle.Turkmenistan) | 109 KB | 171 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 60 |
| [MapBundle.Tuvalu](https://www.nuget.org/packages/MapBundle.Tuvalu) | 33 KB | 24 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 48 |
| [MapBundle.Uganda](https://www.nuget.org/packages/MapBundle.Uganda) | 134 KB | 337 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 176 |
| [MapBundle.Ukraine](https://www.nuget.org/packages/MapBundle.Ukraine) | 295 KB | 545 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 229 |
| [MapBundle.UnitedKingdom](https://www.nuget.org/packages/MapBundle.UnitedKingdom) | 2.0 MB | 4.0 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,677 |
| [MapBundle.Us](https://www.nuget.org/packages/MapBundle.Us) | 54.6 MB | 74.7 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 113,969 |
| [MapBundle.Uruguay](https://www.nuget.org/packages/MapBundle.Uruguay) | 88 KB | 154 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 78 |
| [MapBundle.Uzbekistan](https://www.nuget.org/packages/MapBundle.Uzbekistan) | 94 KB | 194 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 62 |
| [MapBundle.Vanuatu](https://www.nuget.org/packages/MapBundle.Vanuatu) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Venezuela](https://www.nuget.org/packages/MapBundle.Venezuela) | 362 KB | 708 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 374 |
| [MapBundle.Vietnam](https://www.nuget.org/packages/MapBundle.Vietnam) | 660 KB | 1.1 MB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🏖️ 🟩 🌊 | 1,075 |
| [MapBundle.WallisEtFutuna](https://www.nuget.org/packages/MapBundle.WallisEtFutuna) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
| [MapBundle.Yemen](https://www.nuget.org/packages/MapBundle.Yemen) | 186 KB | 297 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 307 |
| [MapBundle.Zambia](https://www.nuget.org/packages/MapBundle.Zambia) | 126 KB | 281 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 76 |
| [MapBundle.Zimbabwe](https://www.nuget.org/packages/MapBundle.Zimbabwe) | 85 KB | 175 KB | 🗺️ 🏙️ 〰️ 💧 🏛️ 🟩 | 45 |
| [MapBundle.UsPuertoRico](https://www.nuget.org/packages/MapBundle.UsPuertoRico) | 64 KB | 72 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 37 |
| [MapBundle.UsUsVirginIslands](https://www.nuget.org/packages/MapBundle.UsUsVirginIslands) | 42 KB | 36 KB | 🗺️ 🏙️ 🏖️ 🟩 🌊 | 44 |
| [MapBundle.IleDeClipperton](https://www.nuget.org/packages/MapBundle.IleDeClipperton) | 134 KB | 213 KB | 🗺️ 🏙️ 🏛️ 🏖️ 🟩 🌊 | 193 |
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

The `MapBundle` core library is MIT. The data packages contain OpenStreetMap data and are licensed under the [ODbL](https://opendatacommons.org/licenses/odbl/) — © OpenStreetMap contributors.
