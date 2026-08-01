param(
    [string]$Configuration = "Release",
    [string]$BannerlordRoot = $env:BANNERLORD_ROOT,
    [string]$HarmonyDll = $env:MCC_HARMONY_DLL,
    [string]$RuntimeHarmonyDll = $env:MCC_RUNTIME_HARMONY_DLL,
    [string]$MSBuildPath = ""
)

$ErrorActionPreference = "Stop"
$SourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModuleRoot = Split-Path (Split-Path $SourceRoot -Parent) -Parent
$Solution = Join-Path $SourceRoot "MultiCharacterCampaignTOR.CSharp.sln"
$Artifacts = Join-Path $SourceRoot "artifacts\bin"
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null

if (-not $BannerlordRoot) { throw "Set BANNERLORD_ROOT or pass -BannerlordRoot." }
$GameBin = Join-Path $BannerlordRoot "bin\Win64_Shipping_Client"
if (-not $HarmonyDll) { $HarmonyDll = Join-Path $BannerlordRoot "Modules\Bannerlord.Harmony\bin\Win64_Shipping_Client\0Harmony.dll" }
if (-not $RuntimeHarmonyDll) { $RuntimeHarmonyDll = $HarmonyDll }

$required = @(
    (Join-Path $GameBin "TaleWorlds.Core.dll"),
    (Join-Path $GameBin "TaleWorlds.CampaignSystem.dll"),
    (Join-Path $GameBin "TaleWorlds.MountAndBlade.dll"),
    (Join-Path $GameBin "TaleWorlds.Library.dll"),
    (Join-Path $GameBin "TaleWorlds.Localization.dll"),
    (Join-Path $GameBin "TaleWorlds.InputSystem.dll"),
    (Join-Path $GameBin "TaleWorlds.ObjectSystem.dll"),
    (Join-Path $BannerlordRoot "Modules\TOR_Core\bin\Win64_Shipping_Client\TOR_Core.dll"),
    $HarmonyDll,
    $RuntimeHarmonyDll
)
$missing = @($required | Where-Object { -not (Test-Path $_) })
if ($missing.Count -gt 0) { throw "Missing build/runtime dependencies:`n$($missing -join "`n")" }

function Find-BuildTool([string]$Explicit) {
    if ($Explicit) { return @{ Kind="msbuild"; Path=(Resolve-Path $Explicit).Path } }
    $msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuild) { return @{ Kind="msbuild"; Path=$msbuild.Source } }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { return @{ Kind="dotnet"; Path=$dotnet.Source } }
    throw "MSBuild/dotnet was not found. Install Visual Studio Build Tools with the .NET Framework 4.7.2 targeting pack and a .NET SDK."
}

$tool = Find-BuildTool $MSBuildPath
$props = @(
    "/p:Configuration=$Configuration",
    "/p:BannerlordRoot=$BannerlordRoot",
    "/p:HarmonyDll=$HarmonyDll",
    "/p:RuntimeHarmonyDll=$RuntimeHarmonyDll"
)
Write-Host "Building reconstructed C# solution with $($tool.Path)"
if ($tool.Kind -eq "msbuild") {
    & $tool.Path $Solution /restore /m @props
} else {
    & $tool.Path build $Solution -c $Configuration --nologo --property:BannerlordRoot="$BannerlordRoot" --property:HarmonyDll="$HarmonyDll" --property:RuntimeHarmonyDll="$RuntimeHarmonyDll"
}
if ($LASTEXITCODE -ne 0) { throw "Reconstructed C# build failed." }

$outputs = @(
    @{ Source="MultiCharacterCampaignTOR\bin\$Configuration\MultiCharacterCampaignTOR.dll"; Release="MultiCharacterCampaignTOR.dll"; Name="MultiCharacterCampaignTOR"; Version="1.0.16.0" },
    @{ Source="RuntimeCompatibility\bin\$Configuration\MultiCharacterCampaignTOR.WaywatcherFix.dll"; Release="MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll"; Name="MultiCharacterCampaignTOR.WaywatcherFix"; Version="1.0.32.0" },
    @{ Source="IdentityGuard\bin\$Configuration\MultiCharacterCampaignTOR.IdentityGuard.v140.dll"; Release="MultiCharacterCampaignTOR.IdentityGuard.v140.dll"; Name="MultiCharacterCampaignTOR.IdentityGuard.v140"; Version="1.0.40.0" },
    @{ Source="NativeCreation\bin\$Configuration\MultiCharacterCampaignTOR.NativeCreation.dll"; Release="MultiCharacterCampaignTOR.NativeCreation.dll"; Name="MultiCharacterCampaignTOR.NativeCreation"; Version="1.0.16.0" },
    @{ Source="NativeCreation.Legacy\bin\$Configuration\MultiCharacterCampaignTOR.NativeCreation.Legacy.dll"; Release="MultiCharacterCampaignTOR.NativeCreation.Legacy.dll"; Name="MultiCharacterCampaignTOR.NativeCreation.Legacy"; Version="1.0.16.0" },
    @{ Source="SettlementPresence\bin\$Configuration\MultiCharacterCampaignTOR.SettlementPresence.v141.dll"; Release="MultiCharacterCampaignTOR.SettlementPresence.v141.dll"; Name="MultiCharacterCampaignTOR.SettlementPresence.v141"; Version="1.0.41.0" }
)

foreach ($o in $outputs) {
    $src = Join-Path $SourceRoot $o.Source
    if (-not (Test-Path $src)) { throw "Expected output missing: $src" }
    $dst = Join-Path $Artifacts $o.Release
    Copy-Item -Force $src $dst
    $an = [System.Reflection.AssemblyName]::GetAssemblyName($dst)
    if ($an.Name -ne $o.Name) { throw "Assembly identity mismatch for $($o.Release): $($an.Name) != $($o.Name)" }
    if ($an.Version.ToString() -ne $o.Version) { throw "Assembly version mismatch for $($o.Release): $($an.Version) != $($o.Version)" }
    Write-Host "PASS $($o.Release) -> $($an.Name) $($an.Version)"
}

Write-Host ""
Write-Host "Reconstructed C# outputs staged at: $Artifacts"
Write-Warning "These are reconstructed-source development builds. They are NOT claimed byte-identical to the immutable v1.0.41 authoritative binaries in the module bin folder."
Write-Warning "The exact v1.0.41 release baseline remains Source\CanonicalIL plus the shipped DLL hashes in Source\BASELINE_SHA256.txt."
