# ADR-0002: Plugin class `DDSystemInfoPlugin`, properties `DDSystemInfoPlugin.*`

- Status: accepted
- Date: 2026-10-24

## Context
SimHub prefixes every property with the plugin class name. SimHub already ships `SystemInfosProvider`, `HWinfoPlugin` and
`Aida64Plugin`; a generic name would be easy to confuse with them. The other DevDogs plugin uses `DDTruckPlugin`.

## Decision
- Class `DDSystemInfoPlugin`, so properties read `DDSystemInfoPlugin.CPU.Temperature`, `DDSystemInfoPlugin.GPU.HotSpot`, ...
- Property names describe the hardware role (`CPU`, `GPU`, `RAM`, `FPS`, `Status`), never a model or vendor.
- Assemblies `DevDogs.SystemInfo.dll` and `DevDogs.SystemInfo.Core.dll`, matching `DevDogs.TruckSimulator*.dll`.

## Consequences
- Renaming the class later breaks every dashboard binding, so the prefix is fixed from v1.
- Dashboards bound to these names work on any PC this plugin supports.
