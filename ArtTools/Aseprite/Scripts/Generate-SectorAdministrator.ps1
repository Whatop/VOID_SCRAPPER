param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\06_SectorAdministrator'
$runDir = Join-Path $ToolRoot ('Temp\sector_administrator_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\04_SystemSupport\system_support_green.png'; Name='system_support_green.png'; SHA256='03584046D5F5FDEE096138D7B4469F544CC79C43D3300659CDD72C734E5FB4B1'},
    [pscustomobject]@{Relative='Output\05_SystemStructures\system_green_control_node.png'; Name='system_green_control_node.png'; SHA256='17969EA88CCC051176986E4F6AEBA4FC398DDC00F9FC424896A5D022F5332B25'},
    [pscustomobject]@{Relative='Output\02_Core\green\core_green_active.png'; Name='core_green_active.png'; SHA256='D24E1CEF64DBA833576260D4383A99FC49ADC48A22F11D630233F9A56AA34909'},
    [pscustomobject]@{Relative='Output\02_Core\green\core_green_highlighted.png'; Name='core_green_highlighted.png'; SHA256='FE994BDD6C889FB73EA2F8D8ECE537568FF51C86AA0FAC9FF6D98281EA2AB173'}
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
$referenceNames = @('Destroyer 2.png','Station 1.png','Station 2.png')
foreach ($name in $referenceNames) {
    $path = Join-Path $inputDir ('90_ReferenceOnly\'+$name)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing broad-layout reference: $path" }
}
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + @($sources | ForEach-Object { Get-Item -LiteralPath (Join-Path $ReferenceRoot $_.Relative) }) | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-sector-administrator-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'approved_copies'
$broadCopies = Join-Path $runDir 'layout_reference_copies'
New-Item -ItemType Directory -Path $copies,$broadCopies,$outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $ReferenceRoot $source.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
foreach ($name in $referenceNames) { Copy-Item -LiteralPath (Join-Path $inputDir ('90_ReferenceOnly\'+$name)) -Destination (Join-Path $broadCopies $name) }
[System.IO.File]::WriteAllText((Join-Path $runDir 'reference_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-BossLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $bytes = [System.IO.File]::ReadAllBytes($script)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $script" }
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $null = $strictUtf8.GetString($bytes)
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
Invoke-BossLua 'build_sector_administrator.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-BossLua 'validate_sector_administrator.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'Boss validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
