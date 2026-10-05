param(
    [string]$ToolRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReferenceRoot = '',
    [switch]$ReplaceGenerated
)
$ErrorActionPreference = 'Stop'
$exe = 'D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
if (-not $ReferenceRoot) { $ReferenceRoot = $ToolRoot }
$inputDir = Join-Path $ReferenceRoot 'Input'
$outputDir = Join-Path $ToolRoot 'Output\17_SystemBossVFX_Revisions'
$runDir = Join-Path $ToolRoot ('Temp\system_vfx_revision_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sources = @(
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Barrage_HeavyImpact.aseprite';Name='VFX_Barrage_HeavyImpact.aseprite';SHA256='9A5F8D385E2594B2ED5065B2F96CB500018929F6679FB118E64D962B8606AD42'},
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Phase_Portal.aseprite';Name='VFX_Phase_Portal.aseprite';SHA256='3913F4BEF51FD4D67EF63E7A4D5A91C165EA42AF84C25C74EC5F9AFF00D0BB32'},
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Phase_RedirectFlash.aseprite';Name='VFX_Phase_RedirectFlash.aseprite';SHA256='0C0A412A4B36AC829F1C4088773BA00A83F35340B940126D4920CFC341A857A9'},
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Barrage_HeavyImpact.png';Name='VFX_Barrage_HeavyImpact.png';SHA256='062E42732218FB264ECB4BF97481EF3775656D0662034450BD2C08782A6D35FD'},
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Phase_Portal_Open.png';Name='VFX_Phase_Portal_Open.png';SHA256='20402790FA64790028EE0F0E446A16D8BD93D32B60C0F5994437D6A65EF9CBE9'},
    [pscustomobject]@{Relative='Output\16_SystemBossVFX\VFX_Phase_RedirectFlash.png';Name='VFX_Phase_RedirectFlash.png';SHA256='039FBB1588B0924E7D277D3FA3861B883A3C8E5DF1222B4C4973790BE9742AF8'},
    [pscustomobject]@{Relative='Output\15_GameplayVFX\VFX_Explosion_Medium.png';Name='VFX_Explosion_Medium.png';SHA256='A331C9D669D3CF4BF0136C4215455411FEB84342AF498DCB7B3F8C875862918E'}
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
$approvedFiles = @(Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot 'Output') -Directory | Where-Object { $_.Name -match '^(0[2-9]|1[0-6])_' } | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse })
$originals = @(@(Get-ChildItem -LiteralPath $inputDir -File -Recurse) + $approvedFiles | Sort-Object -Property FullName -Unique | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0) {
    if (-not $ReplaceGenerated) { throw 'Output already exists. Use -ReplaceGenerated only to rebuild this generated set.' }
    $manifestPath = Join-Path $outputDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Refusing to overwrite an unrecognized output folder.' }
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.generatorId -ne 'void-scrapper-system-boss-vfx-revisions-v1') { throw 'Output belongs to another generator.' }
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
Invoke-VfxLua 'revise_system_boss_vfx.lua' 'build'
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'generation_complete.txt'))) { throw "Generation did not complete. Logs: $runDir" }
Invoke-VfxLua 'validate_system_vfx_revisions.lua' 'validate'
$validation = Get-Content -LiteralPath (Join-Path $outputDir 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $validation.passed) { throw 'VFX validation failed.' }
foreach ($entry in $originals) {
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Original changed: $($entry.Path)" }
}
Copy-Item -LiteralPath (Join-Path $runDir 'reference_hashes.json') -Destination (Join-Path $outputDir 'reference_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Originals unchanged: $($originals.Count); exported pixel comparisons: $($validation.pixelComparisons)"
Write-Output "Copies and logs: $runDir"
