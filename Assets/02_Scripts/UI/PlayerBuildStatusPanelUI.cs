using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class PlayerBuildStatusPanelUI : MonoBehaviour
{
    private enum EntryType
    {
        Ship,
        Reinforcement,
        Trait
    }

    private sealed class Entry
    {
        public EntryType type;
        public Sprite icon;
        public string title;
        public string description;
        public TraitDefinition trait;
        public ReinforcementDefinition reinforcement;
        public ShipDefinition ship;
        public bool canDrop;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Input")]
    [SerializeField] private bool holdTabToOpen = true;
    [SerializeField] private Key fallbackKey = Key.Tab;
    [SerializeField] private bool pauseWhileOpen = true;

    [Header("Drop Input")]
    [SerializeField] private bool allowDropFromPanel = true;
    [SerializeField] private Key dropKey = Key.G;
    [Tooltip("필드드랍은 항상 G 즉시 입력입니다. 분해만 G 홀드를 사용합니다.")]
    [SerializeField] private bool instantDropFromPanel = true;
    [SerializeField] private bool allowActiveDrop = true;
    [SerializeField] private bool allowTraitDrop = true;
    [SerializeField] private float dropDistance = 1.35f;
    [SerializeField] private Vector2 fallbackDropDirection = Vector2.down;
    [SerializeField] private TraitPickup traitPickupPrefab;

    [Header("Drop Hint")]
    [SerializeField] private TextMeshProUGUI dropHintText;
    [SerializeField] private string dropReadyText = "[G] 필드드랍";
    [SerializeField] private string dropDisabledText = "필드드랍 불가";

    [Header("Selected Detail")]
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Scroll View")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private BuildStatusSlotButtonUI slotPrefab;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private bool forceHorizontalStrip = true;

    [Header("References")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private RunLevelSystem runLevelSystem;
    [SerializeField] private PlayerCargoController cargoController;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerRuntimeStatApplier runtimeStatApplier;
    [SerializeField] private PlayerController2D playerController;

    [Header("Catalogs")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private List<BuildingDefinition> buildingDefinitions = new List<BuildingDefinition>();
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeInspectorTraitDefinitions = true;
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private bool includeInspectorReinforcementDefinitions = true;
    [SerializeField] private List<ReinforcementDefinition> reinforcementDefinitions = new List<ReinforcementDefinition>();

    [Header("Active Slot")]
    [SerializeField] private bool alwaysShowActiveSlot = true;
    [SerializeField] private string fallbackDefaultReinforcementId = "rf_emergency_return_anchor";

    [Header("Fallback")]
    [SerializeField] private Sprite fallbackShipIcon;
    [SerializeField] private Sprite fallbackTraitIcon;
    [SerializeField] private Sprite fallbackReinforcementIcon;
    [TextArea]
    [SerializeField] private string fallbackShipDescription = "기체 설명이 없습니다.";

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<BuildStatusSlotButtonUI> slotInstances = new List<BuildStatusSlotButtonUI>();
    private readonly List<TraitDefinition> resolvedTraits = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> resolvedReinforcements = new List<ReinforcementDefinition>();

    private bool isOpen;
    private int selectedIndex;

    private void ForceInstantDropSetting()
    {
        instantDropFromPanel = true;
    }

    private void OnValidate()
    {
        ForceInstantDropSetting();
    }

    private void Awake()
    {
        ForceInstantDropSetting();

        if (root == null)
        {
            root = gameObject;
        }

        ResolveReferences();
        ApplyGridLayout();
        CloseImmediate();
    }

    private void Update()
    {
        bool pressed = IsOpenKeyPressed();

        if (holdTabToOpen)
        {
            if (pressed && !isOpen)
            {
                Open();
            }
            else if (!pressed && isOpen)
            {
                Close();
            }
        }
        else if (WasOpenKeyPressedThisFrame())
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        if (isOpen)
        {
            UpdateDropHold();
        }
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        ResolveReferences();
        Rebuild();

        if (root != null)
        {
            root.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (pauseWhileOpen && GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.PushPause(this, "BuildStatusPanel");
        }
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        if (pauseWhileOpen && GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }

        CloseImmediate();
    }

    private void CloseImmediate()
    {
        isOpen = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void Rebuild()
    {
        ResolveTraits();
        ResolveReinforcements();
        BuildEntries();
        BuildSlots();

        if (entries.Count > 0)
        {
            SelectEntry(Mathf.Clamp(selectedIndex, 0, entries.Count - 1));
        }
        else
        {
            SetDetail(null);
        }
    }

    private void BuildEntries()
    {
        entries.Clear();

        Entry shipEntry = BuildShipEntry();
        if (shipEntry != null)
        {
            entries.Add(shipEntry);
        }

        Entry reinforcementEntry = BuildReinforcementEntry();
        if (reinforcementEntry != null)
        {
            entries.Add(reinforcementEntry);
        }

        RunContext runContext = RunManager.Instance != null ? RunManager.Instance.CurrentRun : null;

        if (runContext != null && runContext.SelectedTraitIds != null)
        {
            foreach (string traitId in runContext.SelectedTraitIds)
            {
                TraitDefinition trait = FindTrait(traitId);

                if (trait == null)
                {
                    continue;
                }

                entries.Add(new Entry
                {
                    type = EntryType.Trait,
                    trait = trait,
                    icon = trait.Icon != null ? trait.Icon : fallbackTraitIcon,
                    title = trait.DisplayName,
                    description = BuildTraitDescription(trait),
                    canDrop = allowTraitDrop
                });
            }
        }
    }

    private Entry BuildShipEntry()
    {
        RunContext runContext = RunManager.Instance != null ? RunManager.Instance.CurrentRun : null;
        string selectedShipId = runContext != null ? runContext.SelectedShipId : null;
        ShipDefinition ship = FindShip(selectedShipId);

        Sprite icon = ship != null && ship.PreviewSprite != null ? ship.PreviewSprite : fallbackShipIcon;
        string title = ship != null ? ship.DisplayName : "현재 기체";
        string body = BuildShipStatusDescription(ship, runContext);

        return new Entry
        {
            type = EntryType.Ship,
            ship = ship,
            icon = icon,
            title = title,
            description = body,
            canDrop = false
        };
    }

    private Entry BuildReinforcementEntry()
    {
        RunContext runContext = RunManager.Instance != null ? RunManager.Instance.CurrentRun : null;
        ReinforcementDefinition definition = null;
        int charges = 0;
        int maxCharges = 0;
        bool actuallyEquipped = false;

        if (reinforcementController != null && reinforcementController.HasEquipment)
        {
            definition = reinforcementController.EquippedDefinition;
            charges = reinforcementController.CurrentCharges;
            maxCharges = reinforcementController.MaxCharges;
            actuallyEquipped = true;
        }

        if (definition == null && runContext != null && runContext.HasEquippedReinforcement)
        {
            definition = FindReinforcement(runContext.EquippedReinforcementId);
            charges = runContext.EquippedReinforcementCharges;
            maxCharges = definition != null ? definition.MaxCharges : 0;
            actuallyEquipped = definition != null;
        }

        // 런 중에는 실제 장착 상태가 우선이다.
        // G로 액티브를 필드드랍한 뒤 기본 긴급복귀 앵커가 다시 있는 것처럼 보이면 안 된다.
        if (definition == null && !IsRuntimePanelActive())
        {
            definition = FindReinforcement(ResolveDefaultReinforcementId(runContext));
            charges = definition != null && definition.StartWithFullCharges ? definition.MaxCharges : 0;
            maxCharges = definition != null ? definition.MaxCharges : 0;
            actuallyEquipped = false;
        }

        if (definition == null && !alwaysShowActiveSlot)
        {
            return null;
        }

        return new Entry
        {
            type = EntryType.Reinforcement,
            reinforcement = definition,
            icon = definition != null && definition.Icon != null ? definition.Icon : fallbackReinforcementIcon,
            title = definition != null ? definition.DisplayName : "액티브 없음",
            description = BuildReinforcementDescription(definition, charges, maxCharges, actuallyEquipped),
            canDrop = allowActiveDrop && actuallyEquipped && definition != null
        };
    }

    private bool IsRuntimePanelActive()
    {
        if (reinforcementController != null)
        {
            return true;
        }

        return RunManager.Instance != null && RunManager.Instance.HasActiveRun;
    }

    private string BuildShipStatusDescription(ShipDefinition ship, RunContext runContext)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(ship != null ? ship.DisplayName : "현재 기체");
        builder.AppendLine();

        if (playerHealth != null)
        {
            builder.AppendLine($"체력 {playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}");
        }
        else
        {
            builder.AppendLine("체력 정보 없음");
        }

        if (runLevelSystem != null)
        {
            builder.AppendLine($"EXP {runLevelSystem.CurrentExpInLevel}/{runLevelSystem.CurrentRequiredExp}");
        }
        else if (runContext != null && runContext.Wallet != null)
        {
            builder.AppendLine($"EXP {runContext.Wallet.Experience}");
        }
        else
        {
            builder.AppendLine("EXP 정보 없음");
        }

        if (cargoController != null)
        {
            builder.AppendLine($"적재량 {cargoController.CurrentLoad}/{cargoController.MaxCapacity}");
            builder.AppendLine($"긴급복귀 보존 한도 {cargoController.EmergencyReturnCapacityLimit}/{cargoController.MaxCapacity}");
        }
        else if (runContext != null)
        {
            builder.AppendLine($"적재량 {runContext.CurrentCargoLoad}/{runContext.MaxCargoCapacity}");
            builder.AppendLine($"긴급복귀 보존 한도 {Mathf.FloorToInt(runContext.MaxCargoCapacity * runContext.EmergencyReturnCapacityRatio)}/{runContext.MaxCargoCapacity}");
        }

        if (reinforcementController != null && reinforcementController.HasEquipment)
        {
            builder.AppendLine($"액티브 {reinforcementController.EquippedDefinition.DisplayName}");
        }
        else
        {
            builder.AppendLine("액티브 없음");
        }

        builder.AppendLine();
        builder.AppendLine(ship != null ? ship.Description : fallbackShipDescription);

        if (ship != null && !string.IsNullOrWhiteSpace(ship.PassiveDescription))
        {
            builder.AppendLine();
            builder.AppendLine(ship.PassiveDescription);
        }

        return builder.ToString();
    }

    private string BuildTraitDescription(TraitDefinition trait)
    {
        if (trait == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(trait.DisplayName);
        builder.AppendLine(trait.GetCategoryText());
        builder.AppendLine();
        builder.AppendLine(trait.Description);

        if (trait.LevelEffects != null && trait.LevelEffects.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("효과");

            for (int i = 0; i < trait.LevelEffects.Count; i++)
            {
                TraitLevelEffect effect = trait.LevelEffects[i];
                if (effect == null)
                {
                    continue;
                }

                builder.AppendLine($"Lv.{effect.Level} {FormatTraitEffect(effect.EffectType, effect.Value)}");
            }
        }

        if (allowTraitDrop)
        {
            builder.AppendLine();
            builder.AppendLine("[G] 패시브 필드드랍");
        }

        return builder.ToString();
    }

    private string BuildReinforcementDescription(ReinforcementDefinition definition, int charges, int maxCharges, bool actuallyEquipped)
    {
        if (definition == null)
        {
            return "액티브 장비가 없습니다.";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(definition.DisplayName);
        builder.AppendLine($"{definition.GetRarityText()} / {definition.GetUseTypeText()}");
        builder.AppendLine(definition.GetAvailabilityText());
        builder.AppendLine();
        builder.AppendLine(definition.Description);
        builder.AppendLine();
        builder.AppendLine("효과");
        builder.AppendLine(definition.BuildEffectSummary());
        builder.AppendLine();
        builder.AppendLine($"충전 {Mathf.Max(0, charges)}/{Mathf.Max(0, maxCharges)}");

        if (actuallyEquipped && allowActiveDrop)
        {
            builder.AppendLine();
            builder.AppendLine("[G] 액티브 필드드랍");
        }

        return builder.ToString();
    }

    private string FormatTraitEffect(TraitEffectType effectType, float value)
    {
        return effectType switch
        {
            TraitEffectType.DamagePercent => $"공격력 +{value:0.#}%",
            TraitEffectType.ProjectileSpeedPercent => $"탄속 +{value:0.#}%",
            TraitEffectType.RangePercent => $"사거리 +{value:0.#}%",
            TraitEffectType.MoveSpeedPercent => $"이동속도 +{value:0.#}%",
            TraitEffectType.DashCooldownReduction => $"대쉬 쿨다운 -{Mathf.Abs(value):0.##}",
            TraitEffectType.DashDistanceBonus => $"대쉬 거리 +{value:0.#}",
            TraitEffectType.MaxHpBonus => $"최대 체력 +{value:0.#}",
            TraitEffectType.HealEfficiencyPercent => $"회복 효율 +{value:0.#}%",
            TraitEffectType.PickupRangeBonus => $"흡수 범위 +{value:0.#}",
            TraitEffectType.CargoCapacityBonus => $"기체용량 +{value:0.#}",
            TraitEffectType.HarvestYieldPercent => $"수확량 +{value:0.#}%",
            TraitEffectType.HarvestObjectDamagePercent => $"수확 오브젝트 피해 +{value:0.#}%",
            TraitEffectType.EmergencyReturnCapacityRatioBonus => $"긴급복귀 보존 한도 +{value:0.#}%",
            TraitEffectType.ActiveCooldownReductionPercent => $"액티브 쿨다운 -{Mathf.Abs(value):0.#}%",
            _ => $"{effectType} {value:0.##}"
        };
    }

    private void BuildSlots()
    {
        ClearSlots();

        if (contentRoot == null || slotPrefab == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            int capturedIndex = i;
            BuildStatusSlotButtonUI slot = Instantiate(slotPrefab, contentRoot);
            slot.Bind(entries[i].icon, () => SelectEntry(capturedIndex));
            slotInstances.Add(slot);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slotInstances.Count; i++)
        {
            if (slotInstances[i] != null)
            {
                Destroy(slotInstances[i].gameObject);
            }
        }

        slotInstances.Clear();
    }

    private void SelectEntry(int index)
    {
        if (entries.Count == 0)
        {
            SetDetail(null);
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, entries.Count - 1);
        SetDetail(entries[selectedIndex]);

        for (int i = 0; i < slotInstances.Count; i++)
        {
            if (slotInstances[i] != null)
            {
                slotInstances[i].SetSelected(i == selectedIndex);
            }
        }
    }

    private void SetDetail(Entry entry)
    {
        if (selectedIconImage != null)
        {
            selectedIconImage.sprite = entry != null ? entry.icon : null;
            selectedIconImage.enabled = entry != null && entry.icon != null;
            selectedIconImage.preserveAspect = true;
        }

        if (descriptionText != null)
        {
            descriptionText.text = entry != null ? entry.description : string.Empty;
        }

        UpdateDropHintText(entry);
    }

    private void UpdateDropHold()
    {
        if (!allowDropFromPanel || !CanDropSelectedEntry())
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        KeyControl key = Keyboard.current[dropKey];

        if (key == null)
        {
            return;
        }

        // 필드드랍은 항상 G 즉시 입력이다.
        // 홀드 게이지는 필드 아이템 분해에만 사용한다.
        if (key.wasPressedThisFrame)
        {
            DropSelectedEntryToField();
        }
    }

    private bool CanDropSelectedEntry()
    {
        if (!allowDropFromPanel || entries.Count == 0 || selectedIndex < 0 || selectedIndex >= entries.Count)
        {
            return false;
        }

        Entry entry = entries[selectedIndex];
        return entry != null && entry.canDrop;
    }

    private void DropSelectedEntryToField()
    {
        if (!CanDropSelectedEntry())
        {
            return;
        }

        Entry entry = entries[selectedIndex];
        bool success = false;

        switch (entry.type)
        {
            case EntryType.Reinforcement:
                success = DropCurrentReinforcement();
                break;

            case EntryType.Trait:
                success = DropTrait(entry.trait);
                break;
        }

        if (success)
        {
            RefreshExternalHudAfterDrop();
            selectedIndex = 0;
            Rebuild();
        }
    }

    private void RefreshExternalHudAfterDrop()
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.RefreshAll();
        }
    }

    private bool DropCurrentReinforcement()
    {
        if (reinforcementController == null || !reinforcementController.HasEquipment)
        {
            return false;
        }

        return reinforcementController.DropCurrentEquipment(ResolveDropPosition());
    }

    private bool DropTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        if (!ShopRunBridge.RemoveRunTrait(trait.TraitId))
        {
            return false;
        }

        TraitPickup pickup = SpawnTraitPickup(ResolveDropPosition());

        if (pickup == null)
        {
            ShopRunBridge.AddRunTrait(trait.TraitId);
            return false;
        }

        pickup.Initialize(trait, 0.5f);
        ReapplyRuntimeStatsAfterTraitListChanged();
        return true;
    }

    private TraitPickup SpawnTraitPickup(Vector2 position)
    {
        if (traitPickupPrefab != null)
        {
            if (PoolManager.Instance != null)
            {
                GameObject pooledObject = PoolManager.Instance.Get(traitPickupPrefab.gameObject, position, Quaternion.identity);
                return pooledObject != null ? pooledObject.GetComponent<TraitPickup>() : null;
            }

            return Instantiate(traitPickupPrefab, position, Quaternion.identity);
        }

        GameObject pickupObject = new GameObject("TraitPickup_DroppedFromStatusUI");
        pickupObject.transform.position = position;

        CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.45f;

        Rigidbody2D rb = pickupObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        pickupObject.AddComponent<SpriteRenderer>();
        return pickupObject.AddComponent<TraitPickup>();
    }

    private void ReapplyRuntimeStatsAfterTraitListChanged()
    {
        if (runtimeStatApplier == null)
        {
            ResolveReferences();
        }

        if (runtimeStatApplier == null)
        {
            return;
        }

        RunContext runContext = RunManager.Instance != null ? RunManager.Instance.CurrentRun : null;
        runtimeStatApplier.Apply(
            runContext,
            PermanentProgress.Instance,
            shipDefinitions,
            buildingDefinitions,
            resolvedTraits,
            false
        );
    }

    private Vector2 ResolveDropPosition()
    {
        Vector2 origin = playerObject != null ? playerObject.transform.position : transform.position;
        Vector2 direction = fallbackDropDirection;

        if (playerController != null && playerController.AimDirection.sqrMagnitude > 0.001f)
        {
            direction = -playerController.AimDirection;
        }
        else if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        return origin + direction.normalized * Mathf.Max(0.1f, dropDistance);
    }

    private void UpdateDropHintText(Entry entry)
    {
        if (dropHintText == null)
        {
            return;
        }

        dropHintText.text = entry != null && entry.canDrop ? dropReadyText : dropDisabledText;
    }

    private void ResolveReferences()
    {
        if (playerObject == null)
        {
            PlayerHealth foundHealth = FindFirstObjectByType<PlayerHealth>();
            if (foundHealth != null)
            {
                playerObject = foundHealth.gameObject;
            }
        }

        if (playerObject == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        if (runLevelSystem == null)
        {
            runLevelSystem = playerObject.GetComponent<RunLevelSystem>();
        }

        if (cargoController == null)
        {
            cargoController = playerObject.GetComponent<PlayerCargoController>();
        }

        if (reinforcementController == null)
        {
            reinforcementController = playerObject.GetComponent<PlayerReinforcementController>();
        }

        if (runtimeStatApplier == null)
        {
            runtimeStatApplier = playerObject.GetComponent<PlayerRuntimeStatApplier>();
        }

        if (playerController == null)
        {
            playerController = playerObject.GetComponent<PlayerController2D>();
        }
    }

    private void ResolveTraits()
    {
        resolvedTraits.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraits);
        }

        if (includeInspectorTraitDefinitions && traitDefinitions != null)
        {
            for (int i = 0; i < traitDefinitions.Count; i++)
            {
                AppendUniqueTrait(resolvedTraits, traitDefinitions[i]);
            }
        }
    }

    private void ResolveReinforcements()
    {
        resolvedReinforcements.Clear();

        if (reinforcementCatalog != null)
        {
            reinforcementCatalog.AppendAllTo(resolvedReinforcements);
        }

        if (includeInspectorReinforcementDefinitions && reinforcementDefinitions != null)
        {
            for (int i = 0; i < reinforcementDefinitions.Count; i++)
            {
                AppendUniqueReinforcement(resolvedReinforcements, reinforcementDefinitions[i]);
            }
        }
    }

    private TraitDefinition FindTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition trait = resolvedTraits[i];
            if (trait != null && trait.TraitId == traitId)
            {
                return trait;
            }
        }

        return null;
    }

    private ReinforcementDefinition FindReinforcement(string reinforcementId)
    {
        if (string.IsNullOrWhiteSpace(reinforcementId))
        {
            return null;
        }

        for (int i = 0; i < resolvedReinforcements.Count; i++)
        {
            ReinforcementDefinition definition = resolvedReinforcements[i];
            if (definition != null && definition.EquipmentId == reinforcementId)
            {
                return definition;
            }
        }

        return null;
    }

    private ShipDefinition FindShip(string shipId)
    {
        if (shipDefinitions == null || shipDefinitions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(shipId))
        {
            for (int i = 0; i < shipDefinitions.Count; i++)
            {
                ShipDefinition ship = shipDefinitions[i];
                if (ship != null && ship.ShipId == shipId)
                {
                    return ship;
                }
            }
        }

        return shipDefinitions[0];
    }

    private string ResolveDefaultReinforcementId(RunContext runContext)
    {
        ShipDefinition ship = FindShip(runContext != null ? runContext.SelectedShipId : null);

        if (ship != null && !string.IsNullOrWhiteSpace(ship.DefaultReinforcementId))
        {
            return ship.DefaultReinforcementId;
        }

        return fallbackDefaultReinforcementId;
    }

    private void AppendUniqueTrait(List<TraitDefinition> target, TraitDefinition trait)
    {
        if (target == null || trait == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            TraitDefinition existing = target[i];
            if (existing != null && existing.TraitId == trait.TraitId)
            {
                return;
            }
        }

        target.Add(trait);
    }

    private void AppendUniqueReinforcement(List<ReinforcementDefinition> target, ReinforcementDefinition definition)
    {
        if (target == null || definition == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            ReinforcementDefinition existing = target[i];
            if (existing != null && existing.EquipmentId == definition.EquipmentId)
            {
                return;
            }
        }

        target.Add(definition);
    }

    private void ApplyGridLayout()
    {
        if (gridLayoutGroup == null)
        {
            return;
        }

        if (forceHorizontalStrip)
        {
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayoutGroup.constraintCount = 1;
            return;
        }

        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = 4;
    }

    private bool IsOpenKeyPressed()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.isPressed;
    }

    private bool WasOpenKeyPressedThisFrame()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.wasPressedThisFrame;
    }
}
