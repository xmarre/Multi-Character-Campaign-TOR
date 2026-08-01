param(
    [Parameter(Mandatory = $true)]
    [string]$CampaignDll,
    [Parameter(Mandatory = $true)]
    [string]$BannerlordBin
)

$ErrorActionPreference = 'Stop'
$env:MCC_INSPECT_BIN = $BannerlordBin
[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name + '.dll'
    $candidate = Join-Path $env:MCC_INSPECT_BIN $name
    if (Test-Path $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    return $null
})
foreach ($reference in (Get-ChildItem $BannerlordBin -Filter '*.dll')) {
    try { [void][System.Reflection.Assembly]::LoadFrom($reference.FullName) } catch { }
}
$assembly = [System.Reflection.Assembly]::LoadFrom($CampaignDll)
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static'

$oneByte = @{}
$twoByte = @{}
foreach ($field in [System.Reflection.Emit.OpCodes].GetFields([System.Reflection.BindingFlags]'Public,Static')) {
    $opcode = [System.Reflection.Emit.OpCode]$field.GetValue($null)
    $value = ([int]$opcode.Value) -band 0xffff
    if ($value -le 0xff) { $oneByte[[int]$value] = $opcode }
    elseif (($value -band 0xff00) -eq 0xfe00) { $twoByte[[int]($value -band 0xff)] = $opcode }
}

function Resolve-Token([System.Reflection.Module]$module, [int]$token, [System.Reflection.Emit.OperandType]$operandType) {
    try {
        switch ($operandType) {
            'InlineString' { return '"' + $module.ResolveString($token) + '"' }
            'InlineField' { return $module.ResolveField($token).ToString() }
            'InlineMethod' { return $module.ResolveMethod($token).ToString() }
            'InlineType' { return $module.ResolveType($token).FullName }
            'InlineTok' { return $module.ResolveMember($token).ToString() }
            'InlineSig' { return ('signature 0x{0:x8}' -f $token) }
        }
    } catch {
        return ('token 0x{0:x8} ({1})' -f $token, $_.Exception.Message)
    }
    return ('token 0x{0:x8}' -f $token)
}

function Show-MethodIL([System.Reflection.MethodBase]$method) {
    Write-Host "===== $($method.DeclaringType.FullName).$method ====="
    $body = $method.GetMethodBody()
    if ($null -eq $body) { Write-Host '<no IL body>'; return }
    $bytes = $body.GetILAsByteArray()
    $module = $method.Module
    $position = 0
    while ($position -lt $bytes.Length) {
        $offset = $position
        $first = [int]$bytes[$position++]
        if ($first -eq 0xfe) {
            $opcode = $twoByte[[int]$bytes[$position++]]
        } else {
            $opcode = $oneByte[$first]
        }
        if ($null -eq $opcode) { throw "Unknown IL opcode at $offset" }
        $operand = ''
        switch ($opcode.OperandType.ToString()) {
            'InlineNone' { }
            'ShortInlineI' { $operand = [sbyte]$bytes[$position]; $position += 1 }
            'InlineI' { $operand = [BitConverter]::ToInt32($bytes, $position); $position += 4 }
            'InlineI8' { $operand = [BitConverter]::ToInt64($bytes, $position); $position += 8 }
            'ShortInlineR' { $operand = [BitConverter]::ToSingle($bytes, $position); $position += 4 }
            'InlineR' { $operand = [BitConverter]::ToDouble($bytes, $position); $position += 8 }
            'ShortInlineBrTarget' {
                $delta = [sbyte]$bytes[$position]; $position += 1
                $operand = ('IL_{0:x4}' -f ($position + $delta))
            }
            'InlineBrTarget' {
                $delta = [BitConverter]::ToInt32($bytes, $position); $position += 4
                $operand = ('IL_{0:x4}' -f ($position + $delta))
            }
            'InlineSwitch' {
                $count = [BitConverter]::ToInt32($bytes, $position); $position += 4
                $base = $position + (4 * $count)
                $targets = @()
                for ($i = 0; $i -lt $count; $i++) {
                    $delta = [BitConverter]::ToInt32($bytes, $position); $position += 4
                    $targets += ('IL_{0:x4}' -f ($base + $delta))
                }
                $operand = $targets -join ', '
            }
            'ShortInlineVar' { $operand = [int]$bytes[$position]; $position += 1 }
            'InlineVar' { $operand = [BitConverter]::ToUInt16($bytes, $position); $position += 2 }
            { $_ -in @('InlineString','InlineField','InlineMethod','InlineType','InlineTok','InlineSig') } {
                $token = [BitConverter]::ToInt32($bytes, $position); $position += 4
                $operand = Resolve-Token $module $token $opcode.OperandType
            }
            default { throw "Unsupported operand type $($opcode.OperandType) at $offset" }
        }
        Write-Host ('IL_{0:x4}: {1,-12} {2}' -f $offset, $opcode.Name, $operand)
    }
}

$encounter = $assembly.GetType('TaleWorlds.CampaignSystem.Encounters.PlayerEncounter', $true)
Write-Host '===== PlayerEncounter API ====='
$methods = @($encounter.GetMethods($flags) | Sort-Object Name, @{ Expression = { $_.GetParameters().Length } })
foreach ($method in $methods) {
    $parameters = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
    Write-Host "$($method.ReturnType.FullName) $($method.Name)($parameters) static=$($method.IsStatic)"
}

foreach ($name in @('RestartPlayerEncounter','Init','Start','StartBattleInternal','ContinueBattle')) {
    foreach ($method in @($methods | Where-Object Name -eq $name)) {
        Show-MethodIL $method
    }
}

Write-Host '===== Encounter-related menu APIs ====='
foreach ($type in @($assembly.GetTypes() | Where-Object { $_.FullName -match 'Encounter|GameMenu' })) {
    foreach ($method in @($type.GetMethods($flags) | Where-Object { $_.Name -match 'Attack|Battle|Encounter|Menu' })) {
        $parameters = ($method.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ', '
        Write-Host "$($type.FullName).$($method.Name)($parameters)"
    }
}
