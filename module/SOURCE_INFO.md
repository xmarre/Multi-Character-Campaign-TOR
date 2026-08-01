# SOURCE_INFO — Multi-Character Campaign TOR v1.0.41

## What this archive contains

This source snapshot corresponds to the exact Multi-Character Campaign - TOR **v1.0.41** release baseline.

Target runtime:

- Mount & Blade II: Bannerlord 1.3.15
- The Old Realms: War in the Mountains 1.16
- Bannerlord.Harmony present

The installable module continues to ship the exact six authoritative v1.0.41 binaries. Source additions do not change runtime behavior.

## Two source layers

### `CanonicalIL/` — immutable v1.0.41 authority

For each of the six assemblies:

- `Authoritative.ikdasm.il` is the full IL/metadata disassembly extracted directly from the exact shipped v1.0.41 DLL.
- `Assembly.mono.il` is the mechanically normalized ILAsm form retained from the canonical preservation package.
- the `.csproj` files in this directory are **ILAsm build projects**, not recovered C# projects.

When reconstructed C# is ambiguous, CanonicalIL answers the question: **what exactly did v1.0.41 execute?**

### `CSharp/` — reconstructed maintainable development source

All six current assemblies now also have normal C# source trees and SDK-style `.csproj` files.

These files were reconstructed from the exact authoritative v1.0.41 DLLs using:

```text
ILSpyCmd 11.0.0.9225
language output: C# 7.3
project output with nested directories
```

They are **not** the lost original authoring source. Every runtime `.cs` file carries a reconstruction header and every project contains `RECONSTRUCTED_SOURCE.md`.

This C# layer is intended to become the maintained development baseline for v1.0.42 and later. The authoritative v1.0.41 DLLs and CanonicalIL remain permanently retained for comparison.

## Why the original C# is not here

The temporary current-version scratch/build workspace used during the v1.0.12-v1.0.41 development sequence was no longer present and could not be recovered from the surviving workspace or available upload history. An older v1.0.11 source archive is known to have existed, but it does not represent the final six-assembly v1.0.41 implementation and was not substituted as current source.

No reconstructed/decompiled C# is silently presented as original source.

## Project mapping

| C# project | Release DLL | Internal assembly identity | Assembly version |
|---|---|---|---|
| `CSharp/MultiCharacterCampaignTOR/` | `MultiCharacterCampaignTOR.dll` | `MultiCharacterCampaignTOR` | 1.0.16.0 |
| `CSharp/RuntimeCompatibility/` | `MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll` | `MultiCharacterCampaignTOR.WaywatcherFix` | 1.0.32.0 |
| `CSharp/IdentityGuard/` | `MultiCharacterCampaignTOR.IdentityGuard.v140.dll` | `MultiCharacterCampaignTOR.IdentityGuard.v140` | 1.0.40.0 |
| `CSharp/NativeCreation/` | `MultiCharacterCampaignTOR.NativeCreation.dll` | `MultiCharacterCampaignTOR.NativeCreation` | 1.0.16.0 |
| `CSharp/NativeCreation.Legacy/` | `MultiCharacterCampaignTOR.NativeCreation.Legacy.dll` | `MultiCharacterCampaignTOR.NativeCreation.Legacy` | 1.0.16.0 |
| `CSharp/SettlementPresence/` | `MultiCharacterCampaignTOR.SettlementPresence.v141.dll` | `MultiCharacterCampaignTOR.SettlementPresence.v141` | 1.0.41.0 |

The RuntimeCompatibility filename/identity mismatch is intentional and preserved. The project compiles internal identity `MultiCharacterCampaignTOR.WaywatcherFix`; the build script stages that output under the release filename `MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll`.

## Reconstruction-only source normalization

Only one decompiled runtime source file required a manual C#-legality normalization:

`CSharp/MultiCharacterCampaignTOR/MultiCharacterCampaignTOR/Reflection.cs`

Two helper declarations were changed from `private static` in the decompiler output to `internal static`:

- `FindField(Type, string, bool)`
- `ToBool(object)`

Reason: the authoritative v1.0.41 IL contains direct calls to these methods from sibling type `FinanceCompatibilityBridge` while also marking the methods private. C# cannot express that cross-type private call. The method bodies were not changed. This normalization is explicitly documented in `RECONSTRUCTION_REPORT.md`; CanonicalIL preserves the exact authoritative accessibility metadata.

No other runtime C# body was manually redesigned, cleaned up, or refactored.

## Compile-time dependencies

The reconstructed projects use configurable paths into the user's installed game rather than redistributing third-party DLLs.

Authoritative v1.0.41 assembly references include:

- Bannerlord/TaleWorlds 1.3.15 assemblies
- Harmony 2.4.2.0 from the core DLL
- Harmony 2.3.3.0 from RuntimeCompatibility

The current six DLLs have no direct compile-time assembly references to ButterLib, UIExtenderEx, or MCM.

TOR_Core is a required runtime module dependency through `SubModule.xml`, although the six assemblies do not directly reference `TOR_Core.dll` at metadata level because TOR integration is largely reflection-based.

## Build commands

Reconstructed C# development build:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 `
  -Configuration Release `
  -BannerlordRoot "D:\Games\Mount & Blade II Bannerlord"
```

Canonical IL preservation rebuild:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-canonical-il.ps1 `
  -Configuration Release `
  -BannerlordRoot "D:\Games\Mount & Blade II Bannerlord"
```

See `CSharp/README_BUILD.md` and `CanonicalIL/README_BUILD.md`.

## Reproducibility status

### Canonical IL

All six canonical IL projects were previously reassembled successfully. Byte-for-byte equality is not claimed because assembler/PE metadata generation differs by toolchain. The shipped DLLs remain authoritative.

### Reconstructed C#

All six reconstructed C# source sets were compiler-validated successfully with Roslyn C# 7.3 after the two explicitly documented accessibility normalizations above.

The local validation environment had the exact uploaded Bannerlord 1.3.15 assemblies available except `TaleWorlds.Core.dll`, `TaleWorlds.DotNet.dll`, and the exact Harmony binaries. Minimal non-shipped compile-only stubs were used for those missing references. Therefore:

- C# syntax and project source completeness were validated;
- all six source sets emitted validation DLLs;
- assembly names and versions matched the intended identities;
- this is **not** a claim that locally rebuilt C# binaries are byte-identical or runtime-equivalent to v1.0.41.

A full Windows rebuild against the user's actual Bannerlord 1.3.15, TOR 1.16, and Harmony dependency files remains the final environment-specific build check.

The installable module in this archive deliberately continues shipping the exact proven v1.0.41 binaries, not the reconstructed-source validation outputs.
