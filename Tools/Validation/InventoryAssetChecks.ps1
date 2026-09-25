$ErrorActionPreference = 'Stop'
$script:assertions = 0
function Check($condition, [string]$message) { $script:assertions++; if (!$condition) { throw $message } }
function Read-Blocks([string]$path) {
    $blocks = @{}
    $source = [IO.File]::ReadAllText((Join-Path (Get-Location) $path)).Replace("`r`n","`n")
    foreach ($b in [regex]::Split($source,'(?m)(?=^--- !u!)')) {
        if ($b -match '^--- !u!\d+ &(-?\d+)') {
            Check (!$blocks.ContainsKey($matches[1])) "Duplicate serialized ID $($matches[1]) in $path"
            $blocks[$matches[1]] = $b
        }
    }
    return $blocks
}
function Ref($block, [string]$key) { return [regex]::Match($block, '(?m)^  '+$key+': \{fileID: (-?\d+)').Groups[1].Value }
function Children($block) { return @([regex]::Matches([regex]::Match($block, '(?s)m_Children:(.*?)  m_Father:').Groups[1].Value, 'fileID: (-?\d+)') | ForEach-Object {$_.Groups[1].Value}) }
function Missing-Refs($blocks) {
    $missing = @{}
    foreach ($id in $blocks.Keys) {
        foreach ($m in [regex]::Matches($blocks[$id], '\{fileID: (-?\d+)\}')) {
            $ref = $m.Groups[1].Value
            if ($ref -ne '0' -and !$blocks.ContainsKey($ref)) { $missing["$id->$ref"] = $true }
        }
    }
    return $missing
}
$paths = @('Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab','Assets/01_Scenes/Expedition.unity')
foreach ($path in $paths) {
    $blocks = Read-Blocks $path
    $baselinePath = 'Logs/InventoryPresentationBaseline/' + [IO.Path]::GetFileName($path)
    $baseline = Read-Blocks $baselinePath
    $oldMissing = Missing-Refs $baseline
    foreach ($missing in (Missing-Refs $blocks).Keys) { Check ($oldMissing.ContainsKey($missing)) "New dangling reference $missing in $path" }
    foreach ($id in $baseline.Keys) { Check ($blocks.ContainsKey($id)) "Existing authored ID was deleted: $id" }
    $panel = $blocks['116849717']
    foreach ($field in @('equipmentTabButton','cargoTabButton','equipmentContentRoot','cargoContentRoot','activeEffectText','activeChargeText','activeStateText','activeSlotSelectButton','activeFieldDropButton','passiveFieldDropButton','equipmentDetailScrollRect','activeEffectsScrollRect','runResourcesText','cargoReturnProjectionText')) {
        $ref = Ref $panel $field
        Check ($ref -ne '0' -and $blocks.ContainsKey($ref)) "Missing authored $field in $path"
    }
    $rows = [regex]::Match($panel,'(?s)  cargoManifestRows:(.*?)  cargoShowAllButton:').Groups[1].Value
    $types = @([regex]::Matches($rows,'currencyType: (\d+)') | ForEach-Object {[int]$_.Groups[1].Value})
    Check (($types -join ',') -eq '2,3,5') 'Manifest must contain only Scrap, Core and Alloy'
    Check ($blocks['920000030'] -match 'm_Name: EquipmentContentRoot') 'Equipment root'
    Check ($blocks['1334581682'] -match 'm_Name: CargoContentRoot') 'Cargo root'
    Check ($blocks['920000030'] -match 'm_IsActive: 1') 'Equipment defaults visible'
    Check ($blocks['1334581682'] -match 'm_IsActive: 0') 'Cargo defaults hidden'
    Check ((Ref $blocks['950100002'] 'm_Father') -eq '1160451699') 'Story must remain outside both tabs'
    foreach ($id in $blocks.Keys) {
        $b = $blocks[$id]
        if ($b -notmatch '^--- !u!224 ') { continue }
        $parent = Ref $b 'm_Father'
        # Check newly authored objects and every modified existing rectangle.
        if (!$baseline.ContainsKey($id) -or $baseline[$id] -cne $b) {
            if ($parent -ne '0' -and $blocks.ContainsKey($parent)) {
                Check ((Children $blocks[$parent]) -contains $id) "Parent is missing its child $id"
            }
            foreach ($child in (Children $b)) {
                Check ($blocks.ContainsKey($child)) "Missing child $child"
                Check ((Ref $blocks[$child] 'm_Father') -eq $id) "Child/parent mismatch $child -> $id"
            }
        }
    }
    function Bounds([string]$id) {
        $width = [regex]::Match($blocks[$id], 'm_SizeDelta: \{x: ([^,]+), y: ([^}]+)\}')
        $w = [double]::Parse($width.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
        $h = [double]::Parse($width.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture)
        $x=0.0; $y=0.0
        while ($id -ne '1160451699') {
            $position=[regex]::Match($blocks[$id], 'm_AnchoredPosition: \{x: ([^,]+), y: ([^}]+)\}')
            $x += [double]::Parse($position.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
            $y += [double]::Parse($position.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture)
            $id = Ref $blocks[$id] 'm_Father'
        }
        return @(($x-$w/2),($y-$h/2),($x+$w/2),($y+$h/2))
    }
    function Separate([string]$first,[string]$second) {
        $a=Bounds $first; $b=Bounds $second
        Check ($a[2] -le $b[0] -or $b[2] -le $a[0] -or $a[3] -le $b[1] -or $b[3] -le $a[1]) "Overlapping required rectangles $first / $second"
    }
    $tabRects=@()
    foreach($buttonField in @('equipmentTabButton','cargoTabButton')) {
        $button=Ref $panel $buttonField; $go=Ref $blocks[$button] 'm_GameObject'
        $rect=[regex]::Match($blocks[$go],'component: \{fileID: (\d+)').Groups[1].Value
        $tabRects += $rect
        Separate $rect '950100002'
        Separate $rect '920000021'
        Separate $rect '960200071'
    }
    Separate $tabRects[0] $tabRects[1]
    Separate '960200075' '459408833'
    Separate '960200075' '910000021'
    Separate '960200075' '932000011'
    Separate '2019793662' '919526142'
    Separate '2019793662' '196184621'
    Separate '919526142' '196184621'
    Write-Output "PASS: authored roots, actions, cargo rules, IDs, hierarchy and key control bounds: $path"
}
$meta = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab.meta'))
$guid = [regex]::Match($meta, 'guid: (\w+)').Groups[1].Value
$tutorial = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/01_Scenes/Tutorial.unity'))
Check ($tutorial.Contains($guid)) 'Tutorial must still inherit the shared inventory prefab'
Check ($tutorial -ceq [IO.File]::ReadAllText((Join-Path (Get-Location) 'Logs/InventoryPresentationBaseline/Tutorial.unity'))) 'Tutorial overrides changed unexpectedly'

function Method([string]$source, [string]$name) {
    $m = [regex]::Match($source, '(?m)^    (?:public|private) [^\r\n]+ '+$name+'\([^\r\n]*\)\s*\{')
    if (!$m.Success) { throw "Missing method $name" }
    $start = $source.IndexOf('{',$m.Index); $depth=1; $end=$start+1
    while ($depth -gt 0 -and $end -lt $source.Length) { if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++ }
    return $source.Substring($m.Index,$end-$m.Index).Replace("`r`n","`n")
}
$before = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Logs/InventoryPresentationBaseline/PlayerBuildStatusPanelUI.cs'))
$after = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/UI/PlayerBuildStatusPanelUI.cs'))
foreach ($method in @('TryDropActiveFieldItem','TryDropSelectedPassiveFieldItem','TryJettisonSelectedCargo','ToggleSelectedCargoAutoPickup')) {
    $old = Method $before $method
    $new = [regex]::Replace((Method $after $method),'(?m)^        if \(!CanUse(?:Equipment|Cargo)Actions[^\n]*\n','')
    Check ($new -ceq $old) "Gameplay operation changed beyond visibility guard: $method"
}
foreach ($method in @('SpawnTraitFieldPickup','SpawnCargoPickup','ResolveFieldDropPosition','ReleaseFieldDropObject','PresentStoryPartAcquired','StopStoryRecoveryFeedback')) {
    Check ((Method $before $method) -ceq (Method $after $method)) "Gameplay/story implementation changed: $method"
}
$tabs = [IO.File]::ReadAllText((Join-Path (Get-Location) 'Assets/02_Scripts/UI/PlayerBuildStatusPanelUI.InventoryTabs.cs'))
Check ($tabs -notmatch 'new GameObject|Instantiate\(|AddComponent|void Update\(') 'No runtime fallback hierarchy or tab polling'
Check ($tabs -notmatch 'SaveData|SaveManager') 'Tab state must remain UI-only'
Write-Output "PASS: $script:assertions serialized/preservation checks (includes all existing IDs); drop/jettison/auto-pickup bodies differ only by visibility guards."
