$ErrorActionPreference = 'Stop'
$validationDirectory = Join-Path (Get-Location) 'Logs/InventoryValidation'
New-Item -ItemType Directory -Force $validationDirectory | Out-Null
$trait = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/Data/TraitDefinition.cs'))
$active = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/Data/ReinforcementDefinition.cs'))
$traitUtility = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/RunRuntime/RunLevelTraitSelectionUI.cs'))
$parts = 'using System.Text; using UnityEngine;' + "`n" +
    [regex]::Match($trait, '(?s)public enum TraitEffectType\s*\{.*?\}').Value + "`n" +
    [regex]::Match($active, '(?s)public enum ReinforcementEffectType\s*\{.*?\}').Value + "`n" +
    $traitUtility.Substring($traitUtility.IndexOf('public static class TraitEffectTextUtility')) + "`n" +
    $active.Substring($active.IndexOf('public static class ReinforcementEffectTextUtility'))
[IO.File]::WriteAllText((Join-Path $validationDirectory 'ProductionPresentationParts.cs'), $parts)
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.0.69f1/Editor/Data'
$runtime = Join-Path $editor 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
$response = @('-target:exe', '-nologo', '-langversion:latest', '-out:Logs/InventoryValidation/InventoryPresentationChecks.dll')
foreach ($assembly in @('System.Private.CoreLib','System.Runtime','System.Console','System.Collections','System.Linq','System.Text.RegularExpressions','System.Memory')) {
    $response += '-r:"' + (Join-Path $runtime ($assembly + '.dll')) + '"'
}
$response += @('Tools/Validation/InventoryPresentationChecks.cs', 'Logs/InventoryValidation/ProductionPresentationParts.cs',
    'Assets/02_Scripts/UI/StatPresentation.cs', 'Assets/02_Scripts/UI/StructuralFrameText.cs', 'Assets/02_Scripts/Data/StructuralFrameProfile.cs')
[IO.File]::WriteAllLines((Join-Path $validationDirectory 'PresentationChecks.rsp'), $response)
& (Join-Path $editor 'NetCoreRuntime/dotnet.exe') (Join-Path $editor 'DotNetSdkRoslyn/csc.dll') '@Logs/InventoryValidation/PresentationChecks.rsp'
if ($LASTEXITCODE -ne 0) { throw 'Presentation check compilation failed.' }
[IO.File]::WriteAllText((Join-Path $validationDirectory 'InventoryPresentationChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}')
& (Join-Path $editor 'NetCoreRuntime/dotnet.exe') 'Logs/InventoryValidation/InventoryPresentationChecks.dll'
if ($LASTEXITCODE -ne 0) { throw 'Presentation checks failed.' }
