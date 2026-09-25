$ErrorActionPreference = 'Stop'
$validation = Join-Path (Get-Location) 'Logs/ResearchSpecialValidation'
New-Item -ItemType Directory -Force $validation | Out-Null
$core = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/Core/Config/CoreTypes.cs'))
$branches = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/Settlement/ShipTraitNodeButton.cs'))
$enums = @('BossStoryPart','WeaponTreeType') | ForEach-Object { [regex]::Match($core,'(?s)public enum '+$_+'\s*\{.*?\}').Value }
$enums += [regex]::Match($branches,'(?s)public enum ShipTraitBranchKind\s*\{.*?\}').Value
[IO.File]::WriteAllText((Join-Path $validation 'ProductionEnums.cs'),($enums -join "`n"))
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.0.69f1/Editor/Data'
$runtime = Join-Path $editor 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
$response = @('-target:exe','-nologo','-langversion:latest','-define:UNITY_EDITOR','-out:Logs/ResearchSpecialValidation/ResearchSpecialContentChecks.dll')
foreach ($assembly in @('System.Private.CoreLib','System.Runtime','System.Console','System.Collections','System.Linq','System.Text.RegularExpressions','System.Memory','System.Security.Cryptography.Algorithms','System.Security.Cryptography.Primitives')) {
    $response += '-r:"' + (Join-Path $runtime ($assembly + '.dll')) + '"'
}
$response += @('Tools/Validation/ResearchSpecialContentChecks.cs','Logs/ResearchSpecialValidation/ProductionEnums.cs','Assets/02_Scripts/Data/TraitDefinition.cs',
    'Assets/02_Scripts/Localization/LocalizationCatalog.cs','Assets/02_Scripts/Localization/NamedPlaceholderUtility.cs','Assets/02_Scripts/Localization/Editor/LocalizationCsvParser.cs','Assets/02_Scripts/Localization/Editor/LocalizationContentValidator.cs')
[IO.File]::WriteAllLines((Join-Path $validation 'ContentChecks.rsp'),$response)
& (Join-Path $editor 'NetCoreRuntime/dotnet.exe') (Join-Path $editor 'DotNetSdkRoslyn/csc.dll') '@Logs/ResearchSpecialValidation/ContentChecks.rsp' *> (Join-Path $validation 'content-compile.txt')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $validation 'content-compile.txt'); throw 'Content check compilation failed.' }
[IO.File]::WriteAllText((Join-Path $validation 'ResearchSpecialContentChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}')
& (Join-Path $editor 'NetCoreRuntime/dotnet.exe') 'Logs/ResearchSpecialValidation/ResearchSpecialContentChecks.dll'
if ($LASTEXITCODE -ne 0) { throw 'Content checks failed.' }
