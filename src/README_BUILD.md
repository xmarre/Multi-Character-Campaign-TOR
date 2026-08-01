# Reconstructed C# build — MCC TOR v1.0.41

This directory is the maintainable **reconstructed C# development layer** for the exact v1.0.41 release baseline. It is not the lost original authoring workspace.

## Targets

- Mount & Blade II: Bannerlord 1.3.15
- The Old Realms: War in the Mountains 1.16
- Bannerlord.Harmony / `0Harmony.dll`
- Core authoritative DLL references: Harmony 2.4.2.0 in `MultiCharacterCampaignTOR.dll`; Harmony 2.3.3.0 in `RuntimeCompatibility.v140.dll`

The six current DLLs do not directly reference ButterLib, UIExtenderEx, or MCM at compile time.

## Prerequisites

- Windows build environment with Visual Studio Build Tools or Visual Studio 2022
- .NET Framework 4.7.2 targeting pack
- .NET SDK capable of building `netstandard2.0` SDK-style projects
- Installed Bannerlord 1.3.15 and TOR WiTM 1.16

No TaleWorlds, TOR, Harmony, ButterLib, UIExtenderEx, MCM, or other third-party DLL is redistributed in the source tree.

## Build

From this directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-csharp.ps1 `
  -Configuration Release `
  -BannerlordRoot "D:\Games\Mount & Blade II Bannerlord"
```

Or set:

```powershell
$env:BANNERLORD_ROOT = "D:\Games\Mount & Blade II Bannerlord"
.\build-csharp.ps1
```

Optional Harmony overrides:

```powershell
$env:MCC_HARMONY_DLL = "D:\path\to\0Harmony-2.4.2.dll"
$env:MCC_RUNTIME_HARMONY_DLL = "D:\path\to\0Harmony-2.3.3.dll"
```

`MCC_RUNTIME_HARMONY_DLL` exists because the authoritative RuntimeCompatibility assembly references Harmony 2.3.3.0 while the authoritative core assembly references Harmony 2.4.2.0. If only the installed Bannerlord.Harmony DLL is supplied, both projects compile against that file and the resulting assembly-reference metadata may differ from v1.0.41 even though the reconstructed source logic is unchanged.

Outputs are staged in:

```text
CSharp\artifacts\bin\
```

The RuntimeCompatibility project intentionally builds internal assembly identity `MultiCharacterCampaignTOR.WaywatcherFix`; the build script stages it under the release filename `MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll`.

## Authority and validation

The exact v1.0.41 execution baseline is **not** these newly compiled C# outputs. It is:

1. the six authoritative DLLs shipped in `bin/Win64_Shipping_Client`;
2. their hashes in `../BASELINE_SHA256.txt`;
3. the exact disassembly in `../CanonicalIL/*/Authoritative.ikdasm.il`.

The reconstructed C# was compiler-validated as described in `../RECONSTRUCTION_REPORT.md`. Future changes should start here, but suspicious decompiler output must be checked against CanonicalIL before modifying behavior.
