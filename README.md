# CraftCrit-Meitou

Independent C# port of [CraftCrit – Critical crafts (RE_Kenshi)](https://steamcommunity.com/sharedfiles/filedetails/?id=3739021405),
using MeitouClient's native mod settings and shared Harmony instance.

## Configure

Open **Settings → Mods → CraftCrit-Meitou**:

- **Override critical craft chance:** On by default. Off restores the normal
  skill-based chance.
- **Chance mode:** choose **Fixed** or **Multiplier**. Only the selected mode's
  slider is shown and used; their values are never combined.
- **Fixed → Critical craft chance (%):** 0–100 in steps of 1; default **100%**.
  0 never crits, 37 gives a 37% roll, and 100 always crits.
- **Multiplier → Critical craft multiplier:** 0–10× in steps of 0.1; default **2×**.
  This multiplies MeitouClient's normal skill-based chance, capped at 100%.
  For example, 20% at 2× becomes 40%; 1× is normal, 0× never crits,
  and a 0% base chance stays 0% at any factor.

MeitouClient saves these preferences in `meitou.user.json`. Changes reach the
current world through a command and take effect in its next serial input phase
(unpause to apply changes).
New games and loaded saves start with the current preferences.

The chance applies when a craft first rolls. Crafts whose outcome has already
been rolled, including ones restored from a save, retain that outcome.
MeitouClient still controls skill-based quality, available recipes, weapon
models, materials, crafting time, and the effect a critical has on the item.
This applies to its crafting benches, including armour, weapons, crossbows,
and robotics. The Craft tab shows the configured chance wherever the game
normally displays a critical-success row.

The original mod's F10 toggle is provided here by the native override switch;
MeitouClient already uses F10 for its debug menu.

On older clients such as 0.20.1 and 0.22.1, a compatibility patch removes the obsolete bench-tech
grade cap, matching `ProductionSystem.Finish` in official source commit
`3b714bea6b382ffde5716a900498e4940adba4d0`. That release otherwise caps leather
bench output at quality 25 (Shoddy), regardless of skill or critical success.
The fix also applies with the chance override off; clients without the old
cap need no patch. It affects newly finished items, leaving existing items
unchanged. With skill 100, Fixed 0% produces Specialist and Fixed 100% produces
Masterwork, even at a tech-level-25 bench.

Supports standard .NET 11 MeitouClient builds from **0.20.1 onward**. Legacy
macOS builds using .NET 10 are outside this DLL's supported runtime.

## Build

The official source checkout is kept separately beside this repository:

```text
MeitouMods/
  MeitouClient/
  MeitouCraftCrit/
```

Build against the **oldest supported official release, 0.20.1**, even when
playing on a newer client. Download and extract the standard build from the
[official release](https://github.com/VlcekM/MeitouClient/releases/tag/v0.20.1),
then point `MeitouDirectory` at the folder containing its DLLs:

```sh
dotnet build CraftCrit-Meitou.csproj -c Release -t:Rebuild -p:MeitouDirectory="/path/to/Meitou-0.20.1"
```

For repeat builds, copy `Meitou.local.props.example` to `Meitou.local.props` and
set its `MeitouDirectory`. This ignored file is shared by the mod and integration
checks. Its default is `artifacts/meitou/0.20.1/`, where this machine's verified
baseline references are cached. Keep this baseline when updating the running
client. A .NET 11 SDK is required.

```sh
dotnet build CraftCrit-Meitou.csproj -c Release -t:Rebuild
```

The build checks all four Meitou reference assemblies are version `0.20.1.0`.
This prevents accidentally raising the DLL's minimum version while its manifest
still accepts older clients. An unversioned source build (`1.0.0.0`) also fails
this check. The source checkout remains useful for API inspection.

`MeitouDirectory` must contain `MeitouClient.dll`, `Meitou.Simulation.dll`,
`Meitou.Data.dll`, `Meitou.Core.dll`, and `0Harmony.dll`. All references use
`Private="false"`; MeitouClient supplies these at runtime. No separately packaged
Harmony, BepInEx, or RE_Kenshi dependency is needed.

## Install

After building, copy these files from `bin/Release/net11.0/` into
`<Kenshi>/mods/CraftCrit-Meitou/` (the Kenshi install, as specified by
`GameInstall.ModsDirectory`, rather than the MeitouClient source folder):

- `CraftCrit-Meitou.mod`
- `meitou.json`
- `CraftCrit-Meitou.dll`

Enable CraftCrit-Meitou in the Meitou launcher, then start the game and check
`log.txt` for the initialization message. A legal Kenshi installation and a
supported MeitouClient build are required to check actual game loading.

On this machine the Kenshi installation was verified at
`/home/daus/.local/share/Steam/steamapps/common/Kenshi`. From this repository,
with MeitouClient closed:

```sh
cd /home/daus/Documentos/Proyectos/MeitouMods/MeitouCraftCrit
kenshi_dir="$HOME/.local/share/Steam/steamapps/common/Kenshi"
mkdir -p "$kenshi_dir/mods/CraftCrit-Meitou"
cp bin/Release/net11.0/CraftCrit-Meitou.dll bin/Release/net11.0/meitou.json bin/Release/net11.0/CraftCrit-Meitou.mod "$kenshi_dir/mods/CraftCrit-Meitou/"
```

For the launcher, enable **CraftCrit-Meitou** in its Mods list and press Play.
For the installed client used by the integration checks, an isolated test list
can be used instead of changing your normal load order:

```sh
mkdir -p artifacts
printf '%s\n' 'CraftCrit-Meitou.mod' > artifacts/craftcrit-test-mods.cfg
KENSHI_PATH="$kenshi_dir" "$HOME/.local/share/Meitou/versions/0.22.1/MeitouClient" --mods-cfg "$PWD/artifacts/craftcrit-test-mods.cfg"
```

`--mods-cfg` is documented in MeitouClient's `docs/launch-options.md` and is read
by `ModList.UseModsCfg`. This test list enables only CraftCrit-Meitou; use a new
test game. Start the game, open Settings → Mods, change the mode/value, unpause,
and begin a new queued craft. Check Fixed 0%/100%, then Multiplier 1×/2× with a
smith whose normal chance is above zero. Replacing the DLL requires restarting
MeitouClient; settings changes need no restart.

A ready-to-copy three-file folder and tar archive are also available under
`artifacts/CraftCrit-Meitou/` and `artifacts/CraftCrit-Meitou.tar.gz` after this
setup's verification. Copy only the mod's files; the game's assemblies stay
with MeitouClient. Use the same three files for every supported client version;
upgrading Meitou alone does not require rebuilding the mod.

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
public package index still returned HTTP 404 on 2026-11-19. This project uses
the documented release-assembly reference approach.

The [reference-package documentation](https://github.com/VlcekM/MeitouClient/blob/master/docs/modding.md#reference-package)
explains that .NET requires game assembly versions at least as new as the mod's
references. A DLL built against `0.20.1.0` can bind to `0.22.1.0`; the reverse
cannot. The manifest's [gameVersion range](https://github.com/VlcekM/MeitouClient/blob/master/docs/modding-dependencies.md#manifest)
is an additional loader check, not an assembly-binding override. This mod uses
`"gameVersion": ">=0.20.1"` with API 2 and no upper bound. Nightlies count as
their release version. API 2 alone does not guarantee engine binary compatibility.

## Verification and compatibility

Run the integration checks against the built game assemblies, without Kenshi assets:

```sh
dotnet run --project tests/CraftCrit.Meitou.Checks/CraftCrit.Meitou.Checks.csproj -c Release
dotnet run --project tests/CraftCrit.Meitou.Checks/CraftCrit.Meitou.Checks.csproj -c Release --no-build -- --saved-settings
dotnet run --project tests/CraftCrit.Meitou.Checks/CraftCrit.Meitou.Checks.csproj -c Release --no-build -- --saved-multiplier
dotnet run --project tests/CraftCrit.Meitou.Checks/CraftCrit.Meitou.Checks.csproj -c Release --no-build -- --unsupported-game
```

To check the **same built mod DLL** against another extracted client, first
build the mod against 0.20.1 as above, then build/run only the harness against
the newer client's assemblies:

```sh
dotnet run --project tests/CraftCrit.Meitou.Checks/CraftCrit.Meitou.Checks.csproj -c Release -p:BuildProjectReferences=false -p:MeitouRuntimeDirectory="/path/to/newer/Meitou"
```

`MeitouRuntimeDirectory` affects only the harness. `MeitouDirectory` remains
the compilation baseline. Repeat the `--no-build` commands after each client
to check persisted preferences and rejection below the supported minimum.

The harness loads the mod through the real `ModHost` and executes its Harmony
patches against the actual `ProductionSystem.CraftStep` and `SmithsView` methods.
It first checks that the client's assembly versions can satisfy the mod's
references. To check the deployed copy, set `CRAFTCRIT_MOD_FOLDER` to its folder
when running the harness. Nested test output and artifacts are excluded from
the library's assembly candidates, and explicit client HintPaths take priority.
It checks 0%, 37%, and 100%; unchanged seeded rolls and counters; queued settings
changes; previously rolled crafts; vanilla mode; all 101 displayed percentages;
per-world isolation; hashed configuration; and restoring saved fixed/multiplier
preferences before the world's first roll. Multiplier checks cover 0×, 0.5×,
1×, 2×, 10×, the 100% cap, zero base chances, exclusive controls, and inactive
values having no effect. Preview checks run the actual calculation with a
synthetic smith and bench, including fractional skill values. They do not draw
the UI. Finished-item checks exercise the actual operator work path at a
tech-level-25 bench: Fixed 0%/100%, Multiplier 10× and override off at skill 100.
No test packages are required.

The 0.20.1 baseline build has zero warnings/errors. On Linux, the same DLL
(verified by SHA256) passes the full checks and both saved-setting checks
using each official client's own bundled .NET 11 runtime and Harmony:

| Client | Official revision | Result | Old grade-cap fix |
| --- | --- | --- | --- |
| 0.20.1 | `8c79093d` | PASS | Applied |
| 0.21.0 | `86345d18` | PASS | Applied |
| 0.22.1 | `9ee4074b` | PASS | Applied |
| 0.22.2-nightly.20261119.2 | `69d2688b` | PASS | Skipped; engine already fixed |

These are headless integration checks with synthetic items and smiths, not
interactive coverage on every OS. A read-only quicksave reproduction with real
Kenshi content also confirmed the 0.22.1 fix produces grade 80/95 and preserves
it after transfer into Inu's inventory; the user subsequently confirmed the
fix works in-game on 0.22.1. The only DLL shipped is `CraftCrit-Meitou.dll`.

The simulation patch replaces the chance call inside the large `CraftStep`
method, because patching a small helper can fail through JIT inlining. It leaves
the engine's deterministic RNG, roll counter, and saved craft outcomes intact.
A second call-site patch computes the displayed percentage in `SmithsView`
from the same raw normal chance, before the game's integer display truncation.
Each world's settings system is placed before production, applies commands in
`Inputs`, invalidates prediction caches, and implements `IStateHashed`.

The open range permits newer clients to try loading this DLL; it cannot promise
compatibility with unknown future code. The private `CraftStep` signature and
chance/preview IL patterns are checked when patches install. A missing method
or changed pattern refuses the mod with a log error and the loader rolls back
its patches. The grade fix recognizes the two obsolete caps and skips clients
without them. Recheck new releases using the same DLL; update the mod only
when a relevant API or patch target changes. Raising the baseline also requires
updating `gameVersion`, the build guard, and the verification matrix.
