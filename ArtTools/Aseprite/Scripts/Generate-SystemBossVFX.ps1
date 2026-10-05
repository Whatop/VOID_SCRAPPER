param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\16_SystemBossVFX'
$runDir = Join-Path $ToolRoot ('Temp\system_boss_vfx_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\06_SectorAdministrator\sector_administrator_charged.png'; Name='sector_administrator_charged.png'; SHA256='75CF4CEB9D70F8F9F0C9B59DFE2226CDB0F3B9CE6AAE6785E18D335076979249'},
    [pscustomobject]@{Relative='Output\07_DefenseOverseer\defense_overseer_barrage_charged.png'; Name='defense_overseer_barrage_charged.png'; SHA256='DE800CE1A1CEFE94FCA4C18845A2BE02957D37BE3E9460B5DBC48E495537E97E'},
    [pscustomobject]@{Relative='Output\08_PhaseGatekeeper\phase_gatekeeper_phase_lock.png'; Name='phase_gatekeeper_phase_lock.png'; SHA256='79836AF785616FA9C31D56E0F407F726B6989876A683E07D0C574610D6E0961D'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_Explosion_Medium.png'; Name='VFX_Explosion_Medium.png'; SHA256='A331C9D669D3CF4BF0136C4215455411FEB84342AF498DCB7B3F8C875862918E'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_EnemyArrival_Telegraph.png'; Name='VFX_EnemyArrival_Telegraph.png'; SHA256='C1225524AE337EFAF5A894257C38D88B548B1C59F54441507F7CFDD1D57761A3'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_CoreActivation_Green.png'; Name='VFX_CoreActivation_Green.png'; SHA256='4A7311512FB7FE939E66A45AECC2395BA0E5E2588034E6E8D18A52E640AE1998'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_CoreActivation_Orange.png'; Name='VFX_CoreActivation_Orange.png'; SHA256='13FFFF3061A34F5ADBF8C9E246154F66B98913C42ED6F63C05E898C10B9EEBD5'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_CoreActivation_Blue.png'; Name='VFX_CoreActivation_Blue.png'; SHA256='E62C583E68B811E1CEE0D17EC9EF396035C0142B46DD572CBA8A80E65EA34B55'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $inputDir -PathType Container)) { throw "Missing Input directory: $inputDir" }
Write-Output 'Input inventory:'
Get-ChildItem -LiteralPath $inputDir -File -Recurse | ForEach-Object { Write-Output $_.FullName }
foreach ($source in $sources) {
    $path = Join-Path $ReferenceRoot $source.Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing inspected reference: $path" }
    Write-Output "INSPECTED WORLD REFERENCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Inspected reference changed; inspect before generating: $path" }
}
$approvedFiles = @(Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot 'Output') -Directory | Where-Object { $_.Name -match '^(0[2-9]|1[0-5])_' } | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse })
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + $approvedFiles | Sort-Object -Property FullName -Unique | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-system-boss-vfx-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'approved_copies'
New-Item -ItemType Directory -Path $copies,$outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $ReferenceRoot $source.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
[System.IO.File]::WriteAllText((Join-Path $runDir 'reference_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-VfxLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    foreach ($luaPath in @($script,(Join-Path $PSScriptRoot 'raider_boss_pixel_helpers.lua'))) {
        $bytes = [System.IO.File]::ReadAllBytes($luaPath)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $luaPath" }
        $null = $strictUtf8.GetString($bytes)
    }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),
        '--script-param',('"qaDir='+$runDir.Replace('\','/')+'"'),
        '--script-param',('"helperPath='+(Join-Path $PSScriptRoot 'raider_boss_pixel_helpers.lua').Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
foreach ($marker in @('generation_complete.txt','validation.json')) {
    $ownedMarker = Join-Path $outputDir $marker
    if (Test-Path -LiteralPath $ownedMarker -PathType Leaf) { Remove-Item -LiteralPath $ownedMarker }
}
Invoke-VfxLua 'build_system_boss_vfx.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-VfxLua 'validate_system_boss_vfx.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'VFX validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
