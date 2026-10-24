# ADR-0001: Target the net48 runtime, build with the .NET 10 toolchain

- Status: accepted
- Date: 2026-10-24

## Context
SimHub 9.x is a .NET Framework 4.8 WPF application and loads plugins in-process, so the plugin assembly must target
.NET Framework. We still want modern C#, `dotnet build` / `dotnet test` from the CLI, and fast tests. This is the same
constraint `DevilDogTG/SimHub.Plugins.TruckSimulator` solved in its ADR-0001.

## Decision
- `DevDogs.SystemInfo` (SimHub glue, WPF settings page): `net48`, `UseWPF`.
- `DevDogs.SystemInfo.Core` (shared-memory parsing, sensor matching, static hardware info): `netstandard2.0`, so it loads in
  SimHub and is testable on modern .NET.
- Tests: `net10.0`.
- .NET 10 SDK, SDK-style projects, `LangVersion=latest` with PolySharp for language features the old runtime lacks.
- SimHub assemblies are referenced from a local install through `$(SimHubPath)` (default: `HKCU\Software\SimHub\InstallDirectory`)
  with `Private=false`; they are never copied or committed.

## Consequences
- Almost all logic lives in Core and is tested without SimHub or HWiNFO running.
- The plugin cannot use net10-only BCL APIs at runtime.
- Building the plugin project needs SimHub installed; Core and tests do not.
