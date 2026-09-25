$ErrorActionPreference = 'Stop'
$script:checks = 0
function Assert-Check([bool]$ok, [string]$message) {
    if (-not $ok) { throw $message }
    $script:checks++
}
function Read-Blocks([string]$path) {
    $result = @{}
    $source = [IO.File]::ReadAllText((Join-Path (Get-Location) $path)).Replace("`r", "")
    foreach ($block in [regex]::Split($source, '(?m)(?=^--- !u!)')) {
        if ($block -match '^--- !u!\d+ &(\d+)') {
            Assert-Check (-not $result.ContainsKey($Matches[1])) ($path + ': duplicate file ID ' + $Matches[1])
            $result[$Matches[1]] = $block
        }
    }
    return ,$result
}
function Bound([string]$text, [string]$field) {
    return [regex]::Match($text, '(?m)^  '+$field+': \{fileID: (\d+)').Groups[1].Value
}
function Rect-For($blocks, [string]$go) {
    return [regex]::Match($blocks[$go], 'component: \{fileID: (\d+)').Groups[1].Value
}
$assetRoot = 'Assets/02_Scripts/Config/TraitDefinition'
$catalog = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset'))
$assets = @(Get-ChildItem -LiteralPath $assetRoot -Filter '*.asset' -Recurse)
$ids = @{}
$normalCount = 0
$specialCount = 0
$expected = @('special_sector_stabilization','special_matter_compression','special_phase_navigation')
$costs = @(24,32,40)
$expectedEffects = @(@(6,7,26),@(23,24,8),@(27,2,9))
foreach ($asset in $assets) {
    $text = [IO.File]::ReadAllText($asset.FullName)
    $id = [regex]::Match($text, '(?m)^  traitId: (.+)').Groups[1].Value.Trim()
    if (-not $id) { continue }
    Assert-Check (-not $ids.ContainsKey($id)) ('Duplicate Trait ID '+$id)
    $ids[$id] = $text
    if ($text -match '(?m)^  developmentRoster: 1') { $normalCount++ }
    $index = [array]::IndexOf($expected, $id)
    if ($index -lt 0) { continue }
    $specialCount++
    $meta = [IO.File]::ReadAllText($asset.FullName + '.meta')
    $guid = [regex]::Match($meta, 'guid: (\w+)').Groups[1].Value
    Assert-Check ($catalog.Contains('guid: '+$guid)) ('Catalog missing '+$id)
    foreach ($pair in @(@('rarity',2),@('category',0),@('maxLevel',3),@('shopItemType',0),@('persistentStoryTrait',0),@('preventFieldDrop',0),@('preventDismantle',0),@('developmentRoster',0),@('requiredStoryPartAnalysis',($index+1)),@('manufacturingScrapCost',$costs[$index]),@('manufacturingCoreCost',($index+1)))) {
        Assert-Check ($text -match ('(?m)^  '+$pair[0]+': '+$pair[1]+'\r?$')) ($id+': '+$pair[0])
    }
    $effects = @([regex]::Matches($text,'(?m)^    effectType: (\d+)') | ForEach-Object {[int]$_.Groups[1].Value})
    Assert-Check ($effects.Count -eq 9) ($id+': nine authored level increments')
    for ($level = 1; $level -le 3; $level++) {
        Assert-Check ([regex]::Matches($text,'(?m)^  - level: '+$level+'\r?$').Count -eq 3) ($id+': level '+$level)
        for ($i=0; $i -lt 3; $i++) {
            Assert-Check ($effects[($level-1)*3+$i] -eq $expectedEffects[$index][$i]) ($id+': supported effect '+$i)
        }
    }
    Assert-Check ($text -match 'icon: \{fileID: [^0]') ($id+': authored icon')
}
Assert-Check ($normalCount -eq 48) 'Final board must retain 48 entries'
Assert-Check ($specialCount -eq 3) 'Exactly three research Special modules'
foreach ($id in @('shared_salvage_protocol','shared_reinforced_plating','shared_repair_foam')) {
    Assert-Check ($ids.ContainsKey($id) -and $ids[$id] -match 'developmentRoster: 0') ('Legacy asset retained '+$id)
}
$rows = @(Import-Csv -LiteralPath 'Assets/02_Scripts/Config/Localization/Source/Localization.csv' -Encoding UTF8)
Assert-Check (($rows.TextKey | Sort-Object -Unique).Count -eq $rows.Count) 'Duplicate localization keys'
foreach ($id in $expected) {
    foreach ($suffix in @('name','description')) {
        $entry = @($rows | Where-Object TextKey -eq ('equipment.'+$id+'.'+$suffix))
        Assert-Check ($entry.Count -eq 1 -and $entry[0].ko -and $entry[0].en) ($id+': Korean/English '+$suffix)
    }
}
foreach ($path in @('Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab','Assets/01_Scenes/Expedition.unity')) {
    $blocks = Read-Blocks $path
    $panel = $blocks['116849717']
    foreach ($field in @('equipmentTabButton','cargoTabButton','equipmentContentRoot','cargoContentRoot','storyProgressInspectButton','storyProgressInspectionRoot','storyProgressSummaryText','storyProgressCloseButton')) {
        $id = Bound $panel $field
        Assert-Check ($id -ne '0' -and $blocks.ContainsKey($id)) ($path+': '+$field)
    }
    $overlay = Bound $panel 'storyProgressInspectionRoot'
    Assert-Check ($blocks[$overlay] -match 'm_IsActive: 0') ($path+': recovery is closed by default')
    $overlayRect = Rect-For $blocks $overlay
    Assert-Check ($blocks['950100002'] -match ('m_Father: \{fileID: '+$overlayRect+'\}')) ($path+': recovery slots moved intact')
    Assert-Check (-not $blocks['1160451699'].Contains('  - {fileID: 950100002}')) ($path+': no always-visible top strip')
    Assert-Check ($blocks['920000031'] -match 'm_SizeDelta: \{x: 418, y: 196\}') ($path+': Equipment body reclaimed')
    Assert-Check ($blocks['1334581683'] -match 'm_SizeDelta: \{x: 418, y: 196\}') ($path+': Cargo body reclaimed')
    Assert-Check ($panel -match '(?s)storyRecoverySlots:.*part: 1.*part: 2.*part: 3') ($path+': original story bindings retained')
    Assert-Check ($blocks[(Bound $panel 'storyProgressCloseButton')] -match 'm_Mode: 0') ($path+': modal keyboard focus contained')
    foreach ($id in $blocks.Keys) {
        if ($id -notmatch '^970300') { continue }
        foreach ($reference in [regex]::Matches($blocks[$id], '\{fileID: (-?\d+)\}')) {
            $target = $reference.Groups[1].Value
            Assert-Check ($target -eq '0' -or $blocks.ContainsKey($target)) ($path+': new local reference '+$id+' -> '+$target)
        }
    }
}
$settlement = Read-Blocks 'Assets/01_Scenes/Settlement.unity'
$panel = @($settlement.Values | Where-Object { $_.Contains('  clearEquipmentButton:') })[0]
$button = Bound $panel 'researchSpecialEquipmentButton'
Assert-Check ($settlement.ContainsKey($button)) 'Settlement Special subsection is authored'
$buttonGo = Bound $settlement[$button] 'm_GameObject'
Assert-Check ($settlement[$buttonGo] -match 'm_Name: ResearchSpecial') 'Special subsection binding'
$prefabMeta = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab.meta'))
$prefabGuid = [regex]::Match($prefabMeta,'guid: (\w+)').Groups[1].Value
Assert-Check ([IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/01_Scenes/Tutorial.unity')).Contains($prefabGuid)) 'Tutorial continues using the shared prefab'
# Preservation checks are available when the task-start current-tree snapshot exists.
$baseline = Join-Path (Get-Location) 'Logs/ResearchSpecialBaseline'
if (Test-Path -LiteralPath $baseline) {
    $protected = @('Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset','Assets/01_Scenes/Tutorial.unity')
    $protected += @(Get-ChildItem -LiteralPath (Join-Path $baseline $assetRoot) -Filter '*.asset' -Recurse | ForEach-Object {$_.FullName.Substring($baseline.Length+1)})
    $protected += @('Assets/02_Scripts/Core/Flow/RunContext.cs','Assets/02_Scripts/Save/SaveData.cs','Assets/02_Scripts/Save/SaveManager.cs','Assets/02_Scripts/RunRuntime/RunRuntimeTraitStore.cs','Assets/02_Scripts/RunRuntime/RunTraitEffectApplier.cs','Assets/02_Scripts/RunRuntime/RunTraitAcquisitionService.cs','Assets/02_Scripts/Player/PlayerRuntimeStatApplier.cs','Assets/02_Scripts/Player/PlayerCargoController.cs','Assets/02_Scripts/Player/PlayerRuntimeBonusState.cs','Assets/02_Scripts/Data/ReinforcementDefinition.cs')
    foreach ($path in $protected) {
        $before = Join-Path $baseline $path
        if (-not (Test-Path -LiteralPath $before)) { continue }
        Assert-Check ((Get-FileHash -LiteralPath $path).Hash -eq (Get-FileHash -LiteralPath $before).Hash) ('Unrelated baseline changed: '+$path)
    }
}
Write-Output ("Research Special asset checks passed: "+$script:checks)
