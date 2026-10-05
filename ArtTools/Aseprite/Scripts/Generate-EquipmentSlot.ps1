param(
 [string]$ToolRoot=(Split-Path -Parent $PSScriptRoot),
 [string]$ReferenceRoot='',
 [switch]$ReplaceGenerated
)
$ErrorActionPreference='Stop'
if(-not $ReferenceRoot){$ReferenceRoot=$ToolRoot}
$exe='D:\SteamFolder\steamapps\common\Aseprite\Aseprite.exe'
$relativeOutput='Output\33_EquipmentSlotUI'
$outputDir=Join-Path $ToolRoot $relativeOutput
$runDir=Join-Path $ToolRoot ('Temp\equipment_slot_'+(Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$utf8=New-Object Text.UTF8Encoding($false,$true)
if(-not(Test-Path -LiteralPath $exe)){throw 'Required Aseprite executable is missing.'}
$refs=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'equipment_slot_references.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot 'Input') -File -Recurse | ForEach-Object {Write-Output "INPUT: $($_.FullName)"}
foreach($ref in $refs){
 $path=Join-Path $ReferenceRoot $ref.Relative
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Required source missing: $path"}
 if((Get-FileHash -LiteralPath $path).Hash -ne $ref.SHA256){throw "Source changed; inspect before rebuilding: $path"}
 Write-Output "CONFIRMED: $path"
}
$excluded=[IO.Path]::GetFullPath((Join-Path $ReferenceRoot $relativeOutput))+'\'
$protected=@(foreach($folder in @('Input','Output','Scripts')){
 Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot $folder) -File -Recurse | Where-Object {-not $_.FullName.StartsWith($excluded,[StringComparison]::OrdinalIgnoreCase)} | ForEach-Object {
  [pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
 }
})
if((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Force).Count -gt 0){
 if(-not $ReplaceGenerated){throw 'Equipment Slot output exists; explicit -ReplaceGenerated is required.'}
 $m=Get-Content -LiteralPath (Join-Path $outputDir 'manifest.json') -Raw | ConvertFrom-Json
 if($m.generatorId -ne 'equipment-slot-v1'){throw 'Refusing an unrelated output.'}
}
$protected += @($refs | ForEach-Object { Get-Item -LiteralPath (Join-Path $ReferenceRoot $_.Relative) } | ForEach-Object { [pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$protected = @($protected | Sort-Object -Property Path -Unique)
$copies=Join-Path $runDir 'approved_copies'
New-Item -ItemType Directory -Path $copies,$outputDir -Force | Out-Null
foreach($ref in $refs){
 $dest=Join-Path $copies $ref.Name
 Copy-Item -LiteralPath (Join-Path $ReferenceRoot $ref.Relative) -Destination $dest
 if((Get-FileHash -LiteralPath $dest).Hash -ne $ref.SHA256){throw 'Source-copy hash mismatch.'}
}
[IO.File]::WriteAllText((Join-Path $runDir 'protected_hashes.json'),($protected | ConvertTo-Json -Depth 4),$utf8)
foreach($name in @('build_equipment_slot.lua','system_boss_pixel_helpers.lua')){
 $bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $name))
 if($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191){throw "BOM is not allowed: $name"}
 $null=$utf8.GetString($bytes)
}
$reportPath=Join-Path $outputDir 'validation.json'
if(Test-Path -LiteralPath $reportPath){Remove-Item -LiteralPath $reportPath}
$arguments=@('--batch','--script-param',('"sourceDir='+$copies.Replace('\','/')+'"'),'--script-param',('"outputDir='+$outputDir.Replace('\','/')+'"'),'--script-param',('"helperPath='+(Join-Path $PSScriptRoot 'system_boss_pixel_helpers.lua').Replace('\','/')+'"'),'--script',('"'+(Join-Path $PSScriptRoot 'build_equipment_slot.lua')+'"'))
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $runDir 'aseprite.stdout.log') -RedirectStandardError (Join-Path $runDir 'aseprite.stderr.log')
if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $reportPath)){throw "Aseprite Equipment Slot build did not complete. Inspect $runDir"}
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if(-not $report.passed){throw 'Revision validation failed.'}
foreach($file in $protected){if((Get-FileHash -LiteralPath $file.Path).Hash -ne $file.SHA256){throw "Protected file changed: $($file.Path)"}}
Copy-Item -LiteralPath (Join-Path $runDir 'protected_hashes.json') -Destination (Join-Path $outputDir 'protected_hashes.json')
Write-Output "GENERATED AND VALIDATED: $outputDir"
Write-Output "Protected files unchanged: $($protected.Count)"
Write-Output "Source copies and logs: $runDir"
