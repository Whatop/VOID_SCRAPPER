param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\08_PhaseGatekeeper'
$runDir = Join-Path $ToolRoot ('Temp\phase_gatekeeper_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\04_SystemSupport\system_support_blue.png'; Name='system_support_blue.png'; SHA256='ED03EA4C429920B031272D5C78C6B5DF15EA554670D16656D77FAE300B26021D'},
    [pscustomobject]@{Relative='Output\05_SystemStructures\system_blue_navigation_node.png'; Name='system_blue_navigation_node.png'; SHA256='DAFB73542549431398D31CC7199D3585738D0472F3BCCD3A2477A885B6222C7A'},
    [pscustomobject]@{Relative='Output\02_Core\blue\core_blue_active.png'; Name='core_blue_active.png'; SHA256='0227A7B6D94A1D988EC6615F0DA11B530C23FA9EF9D01283C9B22E0FFDDA5A9B'},
    [pscustomobject]@{Relative='Output\02_Core\blue\core_blue_highlighted.png'; Name='core_blue_highlighted.png'; SHA256='8CC660B0A14A0C4BC5506AF5240B2B1E5DEA2D59B9D5AA1DCBFA54C4691B63EE'},
    [pscustomobject]@{Relative='Output\06_SectorAdministrator\sector_administrator_idle.png'; Name='sector_administrator_idle.png'; SHA256='B3FD582D3440A6F73E3847335785205C195057C7CCD1F417940B1A2ED235E594'},
    [pscustomobject]@{Relative='Output\07_DefenseOverseer\defense_overseer_idle.png'; Name='defense_overseer_idle.png'; SHA256='631CAB15241893A9EA210A416852F711F5B87C3FDE783503AA318F41EF409D6C'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $inputDir -PathType Container)) { throw "Missing Input directory: $inputDir" }
Write-Output 'Input inventory:'
Get-ChildItem -LiteralPath $inputDir -File -Recurse | ForEach-Object { Write-Output $_.FullName }
foreach ($source in $sources) {
    $path = Join-Path $ReferenceRoot $source.Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing approved reference: $path" }
    Write-Output "APPROVED REFERENCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Approved reference changed; inspect before generating: $path" }
}
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + @($sources | ForEach-Object { Get-Item -LiteralPath (Join-Path $ReferenceRoot $_.Relative) }) | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-phase-gatekeeper-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'approved_copies'
New-Item -ItemType Directory -Path $copies,$outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $ReferenceRoot $source.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
[System.IO.File]::WriteAllText((Join-Path $runDir 'reference_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-BossLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    foreach ($luaPath in @($script,(Join-Path $PSScriptRoot 'system_boss_pixel_helpers.lua'))) {
        $bytes = [System.IO.File]::ReadAllBytes($luaPath)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $luaPath" }
        $null = $strictUtf8.GetString($bytes)
    }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),
        '--script-param',('"qaDir='+$runDir.Replace('\','/')+'"'),
        '--script-param',('"helperPath='+(Join-Path $PSScriptRoot 'system_boss_pixel_helpers.lua').Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
Invoke-BossLua 'build_phase_gatekeeper.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-BossLua 'validate_phase_gatekeeper.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'Boss validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
