param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\19_WorldPickups'
$runDir = Join-Path $ToolRoot ('Temp\world_pickups_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\02_Core\green\core_green_shard.aseprite';Name='core_green_shard.aseprite';SHA256='023D1EC79D642E84E31A9B2836749511E559CEC5B0501FD59B86E1035E2B93D4'},
    [pscustomobject]@{Relative='Output\02_Core\green\core_green_shard.png';Name='core_green_shard.png';SHA256='07780F9A54CA10EA63BBAF0E31D03777679EEC6427E2CA9940794A1D251F99A9'},
    [pscustomobject]@{Relative='Output\02_Core\green\core_green_icon.png';Name='core_green_icon.png';SHA256='0133C4DCB43A18918DC3B27D4327E5BA6F99613A486C870E020B9DCCF9688DE8'},
    [pscustomobject]@{Relative='Output\02_Core\orange\core_orange_icon.png';Name='core_orange_icon.png';SHA256='D901F9D0A6957E9F64F8370F7CC07E22E03AB1A13A0AE5D9192BDC69A12A0E33'},
    [pscustomobject]@{Relative='Output\02_Core\blue\core_blue_icon.png';Name='core_blue_icon.png';SHA256='007E76DC46717DC5470292785C7C9C22E18C1CC5EAB11A50B56429FF02E0497F'},
    [pscustomobject]@{Relative='Output\14_FieldEvents\black_box_dormant.png';Name='black_box_dormant.png';SHA256='A5399A224F24408834F979CADBD66EB3A055D3F57E06FCF37AE1E0FE516A39A5'}
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
$approvedFiles = @(Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot 'Output') -Directory | Where-Object { $_.Name -match '^(0[2-9]|1[0-8])_' } | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse })
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + $approvedFiles | Sort-Object -Property FullName -Unique | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-world-pickups-v1') { throw 'Output belongs to another generator.' }
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
Invoke-VfxLua 'build_world_pickups.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-VfxLua 'validate_world_pickups.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'VFX validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
