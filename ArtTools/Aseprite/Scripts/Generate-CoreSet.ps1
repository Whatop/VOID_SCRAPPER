param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$SourceDir = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $SourceDir) { $SourceDir = Join-Path $ToolRoot 'Input\02_Core' }
$outputDir = Join-Path $ToolRoot 'Output\02_Core'
$tempDir = Join-Path $ToolRoot ('Temp\core_set_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$approved = @(
    'B704EEE9BF59C7FBBC2316CF0839A89E8D6A377FDA2F75197C782A01AB493C35',
    '0528CDDB6AB29C7D78007E464A50F96950501D5A3871CD976E160DE02550E5B4',
    '7E35C5F60C34DF2BD777DABDDCF175A71A6386CB5412A3BE0DC62011C2A03D83',
    '30A13032B906F949A6CECD6003CCAFEEEF70A3CA1995F17A36A6E669856060AF',
    'AF590C3037BDB36F4B38766834C2797A0FC46629D6AD31B66FDF5087E8A499C7',
    '04A2D245E60FBCF806507D8F5619B545072D22E901C1AC00F13A2818D1443BC7',
    '78325821A38B27218BAB721F7A520CE05909678D829648B9DC6EBAB7EF69263A',
    '8837909FB64ABE78C099C54E1EA449E7663F9749965C6F3829ACF01587A2740E',
    '9975C29913FC970A5F91982240E6190A8E30C5139A95B310F9750495A1198562'
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $SourceDir -PathType Container)) { throw "Missing source directory: $SourceDir" }
$sourceFiles = @(Get-ChildItem -LiteralPath $SourceDir -File -Recurse)
Write-Output 'Exact source inventory:'
$sourceFiles | Select-Object FullName,Length | Format-Table -AutoSize | Out-String | Write-Output
if ($sourceFiles.Count -ne 9) { throw 'Source inventory changed: inspect before generating.' }
$sources = foreach ($n in 1..9) {
    $path = Join-Path $SourceDir "core$n.png"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source: $path" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $approved[$n-1]) { throw "Source differs from approved audit: $path" }
    [pscustomobject]@{Name="core$n.png"; Index=$n; Source=$path; SHA256=$hash}
}
if (Test-Path -LiteralPath $outputDir) {
    $existing = @(Get-ChildItem -LiteralPath $outputDir -Force)
    if ($existing.Count -gt 0) {
        if (-not $ReplaceGenerated) { throw 'Output already contains files. Use -ReplaceGenerated only to rebuild this generated set.' }
        $manifestPath = Join-Path $outputDir 'manifest.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
        $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($previous.generatorId -ne 'void-scrapper-core-set-v1') { throw 'Output folder belongs to another generator.' }
    }
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
$copies = Join-Path $tempDir 'sources'
foreach ($source in $sources) {
    $dir = Join-Path $copies "core$($source.Index)"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $copy = Join-Path $dir 'source.png'
    Copy-Item -LiteralPath $source.Source -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Source copy verification failed.' }
}
[System.IO.File]::WriteAllText((Join-Path $tempDir 'source_manifest.json'), ($sources | ConvertTo-Json -Depth 4), $utf8NoBom)
foreach ($family in @('green','orange','blue','purple_corrupted','gray_raider')) {
    New-Item -ItemType Directory -Path (Join-Path $outputDir $family) -Force | Out-Null
}
function Invoke-CoreLua([string]$scriptName, [string]$logName) {
    $lua = Join-Path $PSScriptRoot $scriptName
    $bytes = [System.IO.File]::ReadAllBytes($lua)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua has a BOM: $lua" }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script',('"'+$lua+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $tempDir "$logName.stdout.log") -RedirectStandardError (Join-Path $tempDir "$logName.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $scriptName failed with exit code $($process.ExitCode). Logs: $tempDir" }
}
Invoke-CoreLua 'build_core_set.lua' 'build'
$completion = Join-Path $outputDir 'generation_complete.txt'
if (-not (Test-Path -LiteralPath $completion -PathType Leaf)) { throw "No generation completion marker. Logs: $tempDir" }
Invoke-CoreLua 'validate_core_set.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw "Generated set failed validation. Logs: $tempDir" }
foreach ($source in $sources) {
    if ((Get-FileHash -LiteralPath $source.Source -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Original changed: $($source.Source)" }
}
Copy-Item -LiteralPath (Join-Path $tempDir 'source_manifest.json') -Destination (Join-Path $outputDir 'source_manifest.json')
Write-Output "GENERATED AND VERIFIED: $outputDir"
Write-Output "Assets: $($validation.assets); frames: $($validation.frames); pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Original source hashes unchanged. Logs and working copies: $tempDir"
