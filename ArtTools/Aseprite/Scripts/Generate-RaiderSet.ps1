param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$InputDir = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $InputDir) { $InputDir = Join-Path $ToolRoot 'Input' }
$outputDir = Join-Path $ToolRoot 'Output\03_RaiderEnemy'
$runDir = Join-Path $ToolRoot ('Temp\raider_set_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$anchors = @(
    [pscustomobject]@{Name='common'; Relative='00_StyleAnchors\Pirate_Common_Candidates_32x32.png'; SHA256='569EEFB0F3A2E32929284C518DD70730F899902F2896E96689C438B77FDE4E69'},
    [pscustomobject]@{Name='elite'; Relative='00_StyleAnchors\Pirate_Elite_Candidates_64x64.png'; SHA256='CDF23746E01BDEF5E6DDD0088B284DE7F866CA535E427F465EC96DF2C85F7CE1'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $InputDir -PathType Container)) { throw "Missing Input: $InputDir" }
foreach ($anchor in $anchors) {
    $path = Join-Path $InputDir $anchor.Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source: $path" }
    Write-Output "SOURCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $anchor.SHA256) { throw "Anchor changed; inspect before generating: $path" }
}
$originals = @(Get-ChildItem -LiteralPath $InputDir -File -Recurse | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-raider-set-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'copies'
New-Item -ItemType Directory -Path $copies -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
foreach ($anchor in $anchors) {
    $copy = Join-Path $copies ($anchor.Name + '.png')
    Copy-Item -LiteralPath (Join-Path $InputDir $anchor.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $anchor.SHA256) { throw 'Copy hash differs.' }
}
[System.IO.File]::WriteAllText((Join-Path $runDir 'input_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-RaiderLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $bytes = [System.IO.File]::ReadAllBytes($script)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $script" }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
Invoke-RaiderLua 'build_raider_set.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-RaiderLua 'validate_raider_set.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'Raider set validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'input_hashes.json') -Destination (Join-Path $outputDir 'input_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
