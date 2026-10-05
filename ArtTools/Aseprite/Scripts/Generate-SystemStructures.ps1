param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$approvedDir = Join-Path $ReferenceRoot 'Output\04_SystemSupport'
$outputDir = Join-Path $ToolRoot 'Output\05_SystemStructures'
$runDir = Join-Path $ToolRoot ('Temp\system_structures_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Name='system_support_green.png'; SHA256='03584046D5F5FDEE096138D7B4469F544CC79C43D3300659CDD72C734E5FB4B1'},
    [pscustomobject]@{Name='system_support_orange.png'; SHA256='92D739B3D84DA7FDF6FF13E60D4CDE49C54976B35FB93EB1C28EC99A85171A68'},
    [pscustomobject]@{Name='system_support_blue.png'; SHA256='ED03EA4C429920B031272D5C78C6B5DF15EA554670D16656D77FAE300B26021D'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $inputDir -PathType Container)) { throw "Missing Input: $inputDir" }
Write-Output 'Input inventory:'
Get-ChildItem -LiteralPath $inputDir -File -Recurse | ForEach-Object { Write-Output $_.FullName }
foreach ($source in $sources) {
    $path = Join-Path $approvedDir $source.Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing approved reference: $path" }
    Write-Output "APPROVED STYLE SOURCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Approved source changed; inspect before generating: $path" }
}
$referenceNames = @('Destroyer 2.png','Station 1.png','Station 2.png')
foreach ($name in $referenceNames) {
    $path = Join-Path $inputDir ('90_ReferenceOnly\'+$name)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing broad-layout reference: $path" }
}
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + @(Get-ChildItem -LiteralPath $approvedDir -File) | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-system-structures-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'approved_copies'
$broadCopies = Join-Path $runDir 'layout_reference_copies'
New-Item -ItemType Directory -Path $copies,$broadCopies,$outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $approvedDir $source.Name) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
foreach ($name in $referenceNames) { Copy-Item -LiteralPath (Join-Path $inputDir ('90_ReferenceOnly\'+$name)) -Destination (Join-Path $broadCopies $name) }
[System.IO.File]::WriteAllText((Join-Path $runDir 'reference_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-StructureLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $bytes = [System.IO.File]::ReadAllBytes($script)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $script" }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
Invoke-StructureLua 'build_system_structures.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-StructureLua 'validate_system_structures.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'System structure validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Original references unchanged: $($originals.Count); pixel comparisons: $($validation.pixelComparisons); exact approved-core pixels: $($validation.approvedCorePixelComparisons)"
Write-Output "Copies and logs: $runDir"
