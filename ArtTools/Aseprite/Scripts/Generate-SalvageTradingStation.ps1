param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\12_SalvageTradingStation'
$runDir = Join-Path $ToolRoot ('Temp\salvage_trading_station_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Input\90_ReferenceOnly\Station 1.png'; Name='station_1_reference.png'; SHA256='21B15417FB76266607ECDD82C8EA0A849CFB55ECB5BF57DAAD9AA8891F554153'},
    [pscustomobject]@{Relative='Input\90_ReferenceOnly\Station 2.png'; Name='station_2_reference.png'; SHA256='F4ED5CB54B6D4D5604BAA07C22C5F1805AA9DE2CAF6DCC4CD5956391DD5960F7'},
    [pscustomobject]@{Relative='Output\05_SystemStructures\system_green_control_node.png'; Name='system_green_control_node.png'; SHA256='17969EA88CCC051176986E4F6AEBA4FC398DDC00F9FC424896A5D022F5332B25'},
    [pscustomobject]@{Relative='Output\10_RaiderSalvageCarrier\raider_salvage_carrier_idle.png'; Name='raider_salvage_carrier_idle.png'; SHA256='1B32DF4D4CDAC9DFE8F5390EBAE1CA151FAD39F4324A4623649DBCC21B532AF5'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $inputDir -PathType Container)) { throw "Missing Input directory: $inputDir" }
Write-Output 'Input inventory:'
Get-ChildItem -LiteralPath $inputDir -File -Recurse | ForEach-Object { Write-Output $_.FullName }
foreach ($source in $sources) {
    $path = Join-Path $ReferenceRoot $source.Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing inspected reference: $path" }
    Write-Output "INSPECTED REFERENCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Inspected reference changed; inspect before generating: $path" }
}
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + @($sources | ForEach-Object { Get-Item -LiteralPath (Join-Path $ReferenceRoot $_.Relative) }) | Sort-Object -Property FullName -Unique | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-salvage-trading-station-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'approved_copies'
New-Item -ItemType Directory -Path $copies,$outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $ReferenceRoot $source.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
[System.IO.File]::WriteAllText((Join-Path $runDir 'reference_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-StationLua([string]$name, [string]$label) {
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
Invoke-StationLua 'build_salvage_trading_station.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-StationLua 'validate_salvage_trading_station.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'Station validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
