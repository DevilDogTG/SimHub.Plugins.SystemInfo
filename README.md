# DevDogs System Info (SimHub plugin)

Reads HWiNFO64's shared memory and publishes the values as hardware-agnostic SimHub properties
(`DDSystemInfoPlugin.CPU.Temperature`, `DDSystemInfoPlugin.GPU.HotSpot`, ...), so a dashboard works on any PC
without model names in its bindings. Built for `DevilDogTG/SimHub.Dashboard.SystemMonitor`.

> Work in progress: only `DDSystemInfoPlugin.Status.Connected` is published so far.

## Requirements

- SimHub 9.x
- HWiNFO64 with **Shared Memory Support** enabled (Settings > General). HWiNFO **free** turns shared memory off after
  12 hours; HWiNFO Pro has no limit.

## Properties

| Property | Type | Meaning |
|---|---|---|
| `DDSystemInfoPlugin.Status.Connected` | bool | HWiNFO is running and publishing shared memory |

## Build

Needs the .NET 10 SDK and SimHub installed (its assemblies are referenced from the install folder, found through
`HKCU\Software\SimHub`; override with `-p:SimHubPath=...`).

```powershell
dotnet build -c Release
dotnet test -c Release
```

Deploy to the local SimHub (close SimHub first, it locks loaded plugin DLLs):

```powershell
dotnet build src/DevDogs.SystemInfo -c Release -p:DeployToSimHub=true
```

On the next start SimHub asks whether to enable the new plugin.

## Layout

| Path | Target | Contents |
|---|---|---|
| `src/DevDogs.SystemInfo.Core` | netstandard2.0 | Shared-memory reading, sensor matching; no SimHub dependency |
| `src/DevDogs.SystemInfo` | net48 | SimHub plugin `DDSystemInfoPlugin` |
| `tests/DevDogs.SystemInfo.Core.Tests` | net10.0 | xUnit tests for Core |
| `docs/adr` | | Design decisions |
