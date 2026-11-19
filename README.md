# CraftCrit-Meitou

Independent C# code mod for MeitouClient. The initial entry point only writes
`CraftCrit-Meitou initialized.` to the game log; gameplay changes are not implemented yet.

Requires a .NET 11 SDK and MeitouClient assemblies targeting `net11.0`.

## Build

The official source checkout is kept separately beside this repository:

```text
MeitouMods/
  MeitouClient/
  MeitouCraftCrit/
```

Run from this repository:

```sh
git clone https://github.com/VlcekM/MeitouClient.git ../MeitouClient # first setup only
dotnet build ../MeitouClient/src/Meitou.Game/Meitou.Game.csproj -c Release
dotnet build CraftCrit-Meitou.csproj -c Release
```

Alternatively, reference a matching standard MeitouClient release folder:

```sh
dotnet build CraftCrit-Meitou.csproj -c Release -p:MeitouDirectory="/path/to/Meitou"
```

`MeitouDirectory` must contain `MeitouClient.dll`, `Meitou.Simulation.dll`,
`Meitou.Data.dll`, `Meitou.Core.dll`, and `0Harmony.dll`. All references use
`Private="false"`; MeitouClient supplies these at runtime. No separate Harmony,
BepInEx, or other mod dependency is needed for this entry point.

## Install

Copy these files from `bin/Release/net11.0/` into
`<Kenshi>/mods/CraftCrit-Meitou/`:

- `CraftCrit-Meitou.mod`
- `meitou.json`
- `CraftCrit-Meitou.dll`

Enable CraftCrit-Meitou in the Meitou launcher, then start the game and check
`log.txt` for the initialization message. A legal Kenshi installation and a
matching MeitouClient build are required to check actual game loading.

`CraftCrit-Meitou.mod` is a valid V17 FCS file with no records, generated with
MeitouClient's `FcsWriter`. It lets the launcher discover the code mod.
Keep the manifest `id` stable once saves exist. Its entry type and DLL name
match the class library; `apiVersion` is the official code-mod API version.

## Official contract

Setup follows the [official code-modding guide](https://github.com/VlcekM/MeitouClient/wiki/Writing-code-mods)
and [engine contract](https://github.com/VlcekM/MeitouClient/blob/3b714bea6b382ffde5716a900498e4940adba4d0/docs/modding.md).
Source inspected: `3b714bea6b382ffde5716a900498e4940adba4d0`, including
`IMod.cs`, `ModContext.cs`, `CodeModManifest.cs`, `ModList.cs`, and `FcsWriter.cs`.

The engine contract also documents a `Meitou.References` NuGet package, but its
public package index returned HTTP 404 during setup. This project therefore
uses the documented assembly-reference approach. Build against the version
you will run; API version 2 alone does not guarantee binary compatibility.

## Setup verification

Both the upstream game and this library built in Release with zero warnings
and zero errors using SDK `11.0.100-rc.3.26522.148`. The official manifest
parser and FCS reader accepted the output files; reflection confirmed the
entry implements the game's `IMod` and has a public parameterless constructor.
The only DLL in the mod output is `CraftCrit-Meitou.dll`.
In-game loading has not been checked.
