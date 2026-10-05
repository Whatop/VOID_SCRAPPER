param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$InputDir = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $InputDir) { $InputDir = Join-Path $ToolRoot 'Input' }
$outputDir = Join-Path $ToolRoot 'Output\04_SystemSupport'
$runDir = Join-Path $ToolRoot ('Temp\system_support_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Name='enemy_elite4.png'; Relative='03_SpecialEnemy\enemy_elite4.png'; SHA256='B46FC73C8899CB621E86BAEE800CE70AA6041D25A38043CD792335164FF3461A'},
    [pscustomobject]@{Name='enemy_elite7.png'; Relative='03_SpecialEnemy\enemy_elite7.png'; SHA256='C2C04235A2E6D8745F32BDDF3D8BE42EC0A1E5AD99FE86BE171B7FA1518CAC13'},
    [pscustomobject]@{Name='core9.png'; Relative='02_Core\core9.png'; SHA256='9975C29913FC970A5F91982240E6190A8E30C5139A95B310F9750495A1198562'}
)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
if (-not (Test-Path -LiteralPath $InputDir -PathType Container)) { throw "Missing Input: $InputDir" }
foreach ($source in $sources) {
    $path = Join-Path $InputDir $source.Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source: $path" }
    Write-Output "SOURCE: $path"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source.SHA256) { throw "Source changed; inspect before generating: $path" }
}
$originals = @(Get-ChildItem -LiteralPath $InputDir -File -Recurse | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-system-support-v1') { throw 'Output belongs to another generator.' }
}
$copies = Join-Path $runDir 'copies'
New-Item -ItemType Directory -Path $copies -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
foreach ($source in $sources) {
    $copy = Join-Path $copies $source.Name
    Copy-Item -LiteralPath (Join-Path $InputDir $source.Relative) -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $source.SHA256) { throw 'Copy hash differs.' }
}
[System.IO.File]::WriteAllText((Join-Path $runDir 'input_hashes.json'), ($originals | ConvertTo-Json -Depth 4), $utf8NoBom)
function Invoke-SystemLua([string]$name, [string]$label) {
    $script = Join-Path $PSScriptRoot $name
    $bytes = [System.IO.File]::ReadAllBytes($script)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw "Lua must be UTF-8 without BOM: $script" }
    $arguments = @('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),
        '--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script',('"'+$script+'"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$label.stdout.log") -RedirectStandardError (Join-Path $runDir "$label.stderr.log")
    if ($process.ExitCode -ne 0) { throw "Aseprite $label failed ($($process.ExitCode)). Logs: $runDir" }
}
Invoke-SystemLua 'build_system_support.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-SystemLua 'validate_system_support.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'System support validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'input_hashes.json') -Destination (Join-Path $outputDir 'input_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
