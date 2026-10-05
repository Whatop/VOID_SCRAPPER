param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\09_RaiderAssaultCommander'
$runDir = Join-Path $ToolRoot ('Temp\raider_assault_commander_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\03_RaiderEnemy\raider_basic.png'; Name='raider_basic.png'; SHA256='7296ED1738B50BDD809FF8D4A5BDCC558E9D809270999EE8B5ACD9B93B0C21D8'},
    [pscustomobject]@{Relative='Output\03_RaiderEnemy\raider_shotgun.png'; Name='raider_shotgun.png'; SHA256='D70772CF6C00051E0375C9853FA9B2FFB49D4EC4C55E4E6B9CC4B43CDD0F0465'},
    [pscustomobject]@{Relative='Output\03_RaiderEnemy\raider_sniper_charging.png'; Name='raider_sniper_charging.png'; SHA256='90AA4048A85B6822E9E905AA70C18772DC4C1CDD3474969226F46496997D6504'},
    [pscustomobject]@{Relative='Output\03_RaiderEnemy\raider_elite.png'; Name='raider_elite.png'; SHA256='06E3D5EFC33EEADD216A25E9015C58F6EB74BC820725BABA90A8F7F6BB714946'},
    [pscustomobject]@{Relative='Output\06_SectorAdministrator\sector_administrator_idle.png'; Name='sector_administrator_idle.png'; SHA256='B3FD582D3440A6F73E3847335785205C195057C7CCD1F417940B1A2ED235E594'},
    [pscustomobject]@{Relative='Output\07_DefenseOverseer\defense_overseer_idle.png'; Name='defense_overseer_idle.png'; SHA256='631CAB15241893A9EA210A416852F711F5B87C3FDE783503AA318F41EF409D6C'},
    [pscustomobject]@{Relative='Output\08_PhaseGatekeeper\phase_gatekeeper_idle.png'; Name='phase_gatekeeper_idle.png'; SHA256='3128FD1F5ACD195BB7EF58994313997F1758998216D9E7AB67C150F6C1EDAA5F'}
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
    if ($previous.generatorId -ne 'void-scrapper-raider-assault-commander-v1') { throw 'Output belongs to another generator.' }
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
Invoke-BossLua 'build_raider_assault_commander.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-BossLua 'validate_raider_assault_commander.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'Boss validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
