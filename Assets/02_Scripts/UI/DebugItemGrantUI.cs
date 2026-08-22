using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DebugItemGrantUI : MonoBehaviour
{
    private enum ItemKind
    {
        Auto,
        Trait,
        Reinforcement
    }

    private static readonly Color PanelColor = new Color(0.045f, 0.06f, 0.09f, 0.98f);
    private static readonly Color FieldColor = new Color(0.08f, 0.11f, 0.15f, 1f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.18f, 0.24f, 1f);
    private static readonly Color AccentColor = new Color(0.2f, 0.9f, 0.72f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.42f, 0.42f, 1f);

    [Header("Catalogs")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;

    [Header("Presentation")]
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private int canvasSortOrder = 650;

    private readonly Dictionary<string, TraitDefinition> traitsById =
        new Dictionary<string, TraitDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, ReinforcementDefinition> reinforcementsById =
        new Dictionary<string, ReinforcementDefinition>(StringComparer.Ordinal);
    private readonly List<TraitDefinition> traitBuffer = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> reinforcementBuffer = new List<ReinforcementDefinition>();

    private GameObject canvasRoot;
    private TMP_Dropdown itemTypeDropdown;
    private TMP_InputField idInputField;
    private TMP_InputField amountInputField;
    private Toggle allowPersistentStoryToggle;
    private Button grantButton;
    private Button closeButton;
    private TextMeshProUGUI resultText;
    private GameplayPauseManager pauseManager;
    private GameObject playerObject;
    private PlayerReinforcementController reinforcementController;
    private bool isOpen;
    private bool ownsPause;
    private bool built;
    private bool storedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public bool IsOpen => isOpen;

    private static bool IsDevelopmentToolAvailable
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }
    }

    private void Awake()
    {
        if (!IsDevelopmentToolAvailable)
        {
            enabled = false;
            return;
        }

        BuildCatalogIndexes();
        BuildView();
        SetClosedPresentation();
    }

    private void OnEnable()
    {
        if (!IsDevelopmentToolAvailable || !built)
        {
            return;
        }

        grantButton.onClick.AddListener(GrantCurrentId);
        closeButton.onClick.AddListener(Close);
        idInputField.onSubmit.AddListener(HandleIdSubmitted);
        amountInputField.onSubmit.AddListener(HandleAmountSubmitted);
    }

    private void OnDisable()
    {
        if (grantButton != null)
        {
            grantButton.onClick.RemoveListener(GrantCurrentId);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (idInputField != null)
        {
            idInputField.onSubmit.RemoveListener(HandleIdSubmitted);
        }

        if (amountInputField != null)
        {
            amountInputField.onSubmit.RemoveListener(HandleAmountSubmitted);
        }

        CloseImmediate();
    }

    private void OnDestroy()
    {
        ReleasePauseOwnership();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f10Key.wasPressedThisFrame)
        {
            return;
        }

        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (!IsDevelopmentToolAvailable || !built || isOpen || !CanOpen())
        {
            return;
        }

        pauseManager = GameplayPauseManager.Instance;
        if (pauseManager.Paused && !pauseManager.IsPausedBy(this))
        {
            pauseManager = null;
            return;
        }

        ResolveCurrentPlayer();
        StoreCursorState();
        isOpen = true;
        canvasRoot.SetActive(true);
        pauseManager.PushPause(this, "Debug Item Grant UI");
        pauseManager.RegisterCancelHandler(this, Close);
        ownsPause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        FocusIdInput();
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        SetClosedPresentation();
        ReleasePauseOwnership();
        RestoreCursorState();
    }

    private bool CanOpen()
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null && hud.IsCinematicMode)
        {
            return false;
        }

        if (GameStateManager.Instance == null)
        {
            return true;
        }

        GameState state = GameStateManager.Instance.CurrentState;
        return state == GameState.Tutorial ||
               state == GameState.Expedition ||
               state == GameState.BossBattle;
    }

    private void GrantCurrentId()
    {
        string id = idInputField != null ? idInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(id))
        {
            SetResult("ERROR\nID를 입력하세요.", false);
            FocusIdInput();
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (TryExecuteDevelopmentCommand(id))
        {
            return;
        }
#endif

        ItemKind kind = itemTypeDropdown != null
            ? (ItemKind)Mathf.Clamp(itemTypeDropdown.value, 0, 2)
            : ItemKind.Auto;
        bool hasTrait = traitsById.TryGetValue(id, out TraitDefinition trait);
        bool hasReinforcement = reinforcementsById.TryGetValue(id, out ReinforcementDefinition reinforcement);

        if (kind == ItemKind.Auto)
        {
            if (hasTrait && hasReinforcement)
            {
                SetResult($"ERROR\nAmbiguous ID: {id}", false);
                return;
            }

            if (hasTrait)
            {
                GrantTrait(trait, ResolveAmount());
                return;
            }

            if (hasReinforcement)
            {
                GrantReinforcement(reinforcement);
                return;
            }
        }
        else if (kind == ItemKind.Trait && hasTrait)
        {
            GrantTrait(trait, ResolveAmount());
            return;
        }
        else if (kind == ItemKind.Reinforcement && hasReinforcement)
        {
            GrantReinforcement(reinforcement);
            return;
        }

        SetResult($"ERROR\nUnknown {kind} ID: {id}", false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool TryExecuteDevelopmentCommand(string input)
    {
        string[] tokens = input.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (tokens.Length == 0)
        {
            return false;
        }

        switch (tokens[0].ToLowerInvariant())
        {
            case "corespawn":
                ExecuteCoreSpawnCommand();
                return true;

            case "bossreward":
                ExecuteBossRewardCommand();
                return true;

            case "shop":
                ExecuteShopCommand();
                return true;

            case "teleport":
                ExecuteTeleportCommand(tokens);
                return true;

            case "revealmap":
                ExecuteRevealMapCommand();
                return true;

            default:
                return false;
        }
    }

    private void ExecuteCoreSpawnCommand()
    {
        if (!HasActiveRunForCommand("corespawn"))
        {
            return;
        }

        ExpeditionMapGenerator generator = FindFirstObjectByType<ExpeditionMapGenerator>();

        if (generator == null || !generator.TryPrepareCoreForDebug(out CoreObject core))
        {
            FailCommand("corespawn", "Core generator/prefab is unavailable.");
            return;
        }

        MapDiscoveryController.Instance?.DiscoverTarget(core.RadarTarget);
        Debug.Log($"[DevCommand] corespawn prepared Core at {core.transform.position}.", core);
        Close();
    }

    private void ExecuteBossRewardCommand()
    {
        if (!HasActiveRunForCommand("bossreward"))
        {
            return;
        }

        RunLevelTraitSelectionUI rewardUI =
            FindFirstObjectByType<RunLevelTraitSelectionUI>(FindObjectsInactive.Include);

        if (rewardUI == null)
        {
            FailCommand("bossreward", "RunLevelTraitSelectionUI was not found.");
            return;
        }

        ResolveCurrentPlayer();
        Vector2 sourcePosition = playerObject != null
            ? playerObject.transform.position
            : Vector2.zero;
        ExpeditionObjectiveDirector director = ExpeditionObjectiveDirector.Instance;
        int choiceCount = director != null ? director.ResolveBossChoiceCount(3) : 3;
        bool rareGuaranteed = director == null || director.BossRareGuaranteed;

        Close();

        bool opened = rewardUI.ShowBossRewardChoices(
            choiceCount,
            rareGuaranteed,
            sourcePosition,
            result => Debug.Log($"[DevCommand] bossreward selected {result.Option?.Id}.")
        );

        if (!opened)
        {
            Debug.LogWarning("[DevCommand] bossreward FAILED: reward options could not be generated or the panel is already open.", this);
        }
    }

    private void ExecuteShopCommand()
    {
        if (!HasActiveRunForCommand("shop") || !ResolveCurrentPlayer())
        {
            if (playerObject == null)
            {
                FailCommand("shop", "No current Player was found.");
            }

            return;
        }

        ShopStructure[] shops = FindObjectsByType<ShopStructure>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        ShopStructure selectedShop = null;

        for (int i = 0; i < shops.Length; i++)
        {
            if (shops[i] != null && shops[i].CanInteract(playerObject))
            {
                selectedShop = shops[i];
                break;
            }
        }

        if (selectedShop == null)
        {
            FailCommand("shop", "No neutral active Shop is available.");
            return;
        }

        Close();
        selectedShop.Interact(playerObject);
        Debug.Log($"[DevCommand] shop opened {selectedShop.DisplayName}.", selectedShop);
    }

    private void ExecuteTeleportCommand(string[] tokens)
    {
        if (!HasActiveRunForCommand("teleport") || !ResolveCurrentPlayer())
        {
            if (playerObject == null)
            {
                FailCommand("teleport", "No current Player was found.");
            }

            return;
        }

        if (!TryResolveTeleportTarget(tokens, out Vector2 targetPosition, out string targetLabel))
        {
            FailCommand("teleport", "Use: teleport, teleport x y, teleport core, teleport shop, or teleport boss.");
            return;
        }

        Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = targetPosition;
        }
        else
        {
            playerObject.transform.position = targetPosition;
        }

        Physics2D.SyncTransforms();
        Debug.Log($"[DevCommand] teleport moved Player to {targetLabel} at {targetPosition}.", playerObject);
        Close();
    }

    private bool TryResolveTeleportTarget(
        string[] tokens,
        out Vector2 targetPosition,
        out string targetLabel)
    {
        targetPosition = Vector2.zero;
        targetLabel = string.Empty;

        if (tokens.Length == 1)
        {
            ExpeditionMapGenerator generator = FindFirstObjectByType<ExpeditionMapGenerator>();

            if (generator == null || generator.MapBounds.size.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            targetPosition = generator.MapBounds.center;
            targetLabel = "map center";
            return true;
        }

        if (tokens.Length >= 3 &&
            float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            targetPosition = new Vector2(x, y);
            targetLabel = "coordinates";
            return true;
        }

        string targetKind = tokens[1].ToLowerInvariant();
        Transform target = targetKind switch
        {
            "core" => FindFirstObjectByType<CoreObject>(FindObjectsInactive.Include)?.transform,
            "shop" => FindFirstObjectByType<ShopStructure>(FindObjectsInactive.Include)?.transform,
            "boss" => FindFirstObjectByType<BossDummyController>(FindObjectsInactive.Include)?.transform,
            _ => null
        };

        if (target == null)
        {
            return false;
        }

        targetPosition = (Vector2)target.position + Vector2.right * 1.5f;
        targetLabel = targetKind;
        return true;
    }

    private void ExecuteRevealMapCommand()
    {
        if (!HasActiveRunForCommand("revealmap"))
        {
            return;
        }

        MapDiscoveryController discovery = MapDiscoveryController.Instance;

        if (discovery == null || !discovery.RevealAllForDebug())
        {
            FailCommand("revealmap", "Map discovery is unavailable or not initialized.");
            return;
        }

        Debug.Log("[DevCommand] revealmap revealed the current run map and active targets.", discovery);
        Close();
    }

    private bool HasActiveRunForCommand(string command)
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return true;
        }

        FailCommand(command, "No active Expedition run exists.");
        return false;
    }

    private void FailCommand(string command, string reason)
    {
        string message = $"[DevCommand] {command} FAILED: {reason}";
        Debug.LogWarning(message, this);
        SetResult($"ERROR\n{command}: {reason}", false);
    }
#endif

    private void GrantTrait(TraitDefinition trait, int amount)
    {
        if (trait == null)
        {
            SetResult("ERROR\nTrait definition is missing.", false);
            return;
        }

        if (!ResolveCurrentPlayer())
        {
            SetResult("ERROR\nNo current player found.", false);
            return;
        }

        if (trait.IsPersistentStoryTrait)
        {
            if (allowPersistentStoryToggle == null || !allowPersistentStoryToggle.isOn)
            {
                SetResult("PERSISTENT STORY TRAIT\nAllow Persistent Story를 활성화하세요.", false);
                return;
            }

            bool acquired = RunTraitAcquisitionService.TryAcquirePersistentStoryTrait(trait, playerObject);
            bool owned = PermanentProgress.Instance != null &&
                         PermanentProgress.Instance.HasPersistentStoryTrait(trait);
            SetResult(
                acquired
                    ? $"SUCCESS\nTrait: {trait.TraitId}\nLevel: 1/1"
                    : owned
                        ? $"NO CHANGE\nTrait already owned: {trait.TraitId}"
                        : $"ERROR\nPersistent Trait grant failed: {trait.TraitId}",
                acquired || owned);
            return;
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int before = store.GetLevel(trait.TraitId);
        int acquiredCount = 0;
        int finalLevel = before;

        for (int i = 0; i < amount; i++)
        {
            if (!RunTraitAcquisitionService.TryAcquireForDebug(
                    trait,
                    playerObject,
                    out _,
                    out int newLevel))
            {
                break;
            }

            acquiredCount++;
            finalLevel = newLevel;
        }

        if (acquiredCount <= 0)
        {
            finalLevel = store.GetLevel(trait.TraitId);
            SetResult($"NO CHANGE\nTrait: {trait.TraitId}\nLevel: {finalLevel}/{trait.MaxLevel}", true);
            return;
        }

        SetResult(
            $"SUCCESS\nTrait: {trait.TraitId}\nLevel: {finalLevel}/{trait.MaxLevel} (+{acquiredCount})",
            true);
    }

    private void GrantReinforcement(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            SetResult("ERROR\nReinforcement definition is missing.", false);
            return;
        }

        ResolveCurrentPlayer();
        if (reinforcementController == null)
        {
            SetResult("ERROR\nNo PlayerReinforcementController found.", false);
            return;
        }

        if (!reinforcementController.EquipWithoutDropping(definition))
        {
            SetResult($"ERROR\nEquip failed: {definition.EquipmentId}", false);
            return;
        }

        SetResult(
            $"SUCCESS\nReinforcement: {definition.EquipmentId}\nCharges: " +
            $"{reinforcementController.CurrentCharges}/{reinforcementController.MaxCharges}",
            true);
    }

    private int ResolveAmount()
    {
        if (amountInputField == null || !int.TryParse(amountInputField.text, out int amount))
        {
            return 1;
        }

        return Mathf.Clamp(amount, 1, 100);
    }

    private bool ResolveCurrentPlayer()
    {
        if (playerObject != null)
        {
            if (reinforcementController == null)
            {
                reinforcementController = playerObject.GetComponent<PlayerReinforcementController>();
            }

            return true;
        }

        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            playerObject = health.gameObject;
            reinforcementController = playerObject.GetComponent<PlayerReinforcementController>();
            return true;
        }

        reinforcementController = FindFirstObjectByType<PlayerReinforcementController>();
        playerObject = reinforcementController != null ? reinforcementController.gameObject : null;
        return playerObject != null;
    }

    private void BuildCatalogIndexes()
    {
        traitsById.Clear();
        traitBuffer.Clear();
        traitCatalog?.AppendAllTo(traitBuffer);

        for (int i = 0; i < traitBuffer.Count; i++)
        {
            TraitDefinition trait = traitBuffer[i];
            if (trait != null &&
                !string.IsNullOrWhiteSpace(trait.TraitId) &&
                !traitsById.ContainsKey(trait.TraitId))
            {
                traitsById.Add(trait.TraitId, trait);
            }
        }

        reinforcementsById.Clear();
        reinforcementBuffer.Clear();
        reinforcementCatalog?.AppendAllTo(reinforcementBuffer);

        for (int i = 0; i < reinforcementBuffer.Count; i++)
        {
            ReinforcementDefinition definition = reinforcementBuffer[i];
            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.EquipmentId) &&
                !reinforcementsById.ContainsKey(definition.EquipmentId))
            {
                reinforcementsById.Add(definition.EquipmentId, definition);
            }
        }

        if (traitCatalog == null || reinforcementCatalog == null)
        {
            Debug.LogError("Debug Item Grant UI requires both master catalogs.", this);
        }
    }

    private void BuildView()
    {
        built = true;
        canvasRoot = new GameObject("Canvas_DebugItemGrant", typeof(RectTransform));
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = canvasSortOrder;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject dimmer = CreateStretchObject("Dimmer", canvasRoot.transform);
        AddImage(dimmer, new Color(0f, 0f, 0f, 0.55f));
        GameObject panel = CreateRectObject("Panel", canvasRoot.transform, Vector2.zero, new Vector2(390f, 220f));
        AddImage(panel, PanelColor);

        CreateText("TitleText", panel.transform, "DEV ITEM GRANT", new Vector2(0f, 88f), new Vector2(340f, 24f), 15f, TextAlignmentOptions.Center, AccentColor);
        itemTypeDropdown = CreateDropdown("ItemTypeDropdown", panel.transform, new Vector2(-105f, 52f), new Vector2(145f, 24f));
        itemTypeDropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Auto"),
            new TMP_Dropdown.OptionData("Trait"),
            new TMP_Dropdown.OptionData("Reinforcement")
        };
        itemTypeDropdown.value = 0;
        itemTypeDropdown.RefreshShownValue();

        allowPersistentStoryToggle = CreateToggle("AllowPersistentStoryToggle", panel.transform, new Vector2(57f, 52f));
        CreateText("AllowPersistentStoryLabel", panel.transform, "Persistent Story 허용", new Vector2(127f, 52f), new Vector2(130f, 20f), 8f, TextAlignmentOptions.Left, TextColor);

        idInputField = CreateInputField("IdInputField", panel.transform, "Trait / Reinforcement ID / command", new Vector2(0f, 18f), new Vector2(330f, 27f), false);
        amountInputField = CreateInputField("AmountOrLevelInput", panel.transform, "Count", new Vector2(-105f, -18f), new Vector2(120f, 24f), true);
        amountInputField.text = "1";
        CreateText("AmountHelpText", panel.transform, "Trait 반복 횟수 (1-100)", new Vector2(63f, -18f), new Vector2(190f, 20f), 8f, TextAlignmentOptions.Left, new Color(0.68f, 0.75f, 0.82f, 1f));

        resultText = CreateText(
            "ResultText",
            panel.transform,
            "ID 또는 corespawn / bossreward / shop / teleport / revealmap",
            new Vector2(0f, -55f),
            new Vector2(340f, 44f),
            9f,
            TextAlignmentOptions.Center,
            TextColor
        );
        resultText.textWrappingMode = TextWrappingModes.Normal;

        grantButton = CreateButton("GrantButton", panel.transform, "GRANT", new Vector2(-62f, -91f), new Vector2(110f, 26f));
        closeButton = CreateButton("CloseButton", panel.transform, "CLOSE", new Vector2(62f, -91f), new Vector2(110f, 26f));
    }

    private void HandleIdSubmitted(string _)
    {
        GrantCurrentId();
    }

    private void HandleAmountSubmitted(string _)
    {
        GrantCurrentId();
    }

    private void SetResult(string message, bool success)
    {
        if (resultText == null)
        {
            return;
        }

        resultText.text = message;
        resultText.color = success ? AccentColor : ErrorColor;
    }

    private void FocusIdInput()
    {
        if (idInputField == null)
        {
            return;
        }

        EventSystem.current?.SetSelectedGameObject(idInputField.gameObject);
        idInputField.Select();
        idInputField.ActivateInputField();
        idInputField.MoveTextEnd(false);
    }

    private void SetClosedPresentation()
    {
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(false);
        }
    }

    private void CloseImmediate()
    {
        isOpen = false;
        SetClosedPresentation();
        ReleasePauseOwnership();
        RestoreCursorState();
    }

    private void ReleasePauseOwnership()
    {
        if (!ownsPause || pauseManager == null)
        {
            return;
        }

        pauseManager.UnregisterCancelHandler(this);
        pauseManager.PopPause(this);
        ownsPause = false;
        pauseManager = null;
    }

    private void StoreCursorState()
    {
        if (storedCursorState)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        storedCursorState = true;
    }

    private void RestoreCursorState()
    {
        if (!storedCursorState)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        storedCursorState = false;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject target = CreateRectObject(objectName, parent, position, size);
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        ApplyUiFont(text);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject target = CreateRectObject(objectName, parent, position, size);
        Image image = AddImage(target, ButtonColor);
        Button button = target.AddComponent<Button>();
        button.targetGraphic = image;
        GameObject labelObject = CreateStretchObject("Label", target.transform);
        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        ApplyUiFont(labelText);
        labelText.text = label;
        labelText.fontSize = 10f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = TextColor;
        labelText.raycastTarget = false;
        return button;
    }

    private Toggle CreateToggle(string objectName, Transform parent, Vector2 position)
    {
        GameObject target = CreateRectObject(objectName, parent, position, new Vector2(18f, 18f));
        Image background = AddImage(target, FieldColor);
        GameObject checkObject = CreateStretchObject("Checkmark", target.transform);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.offsetMin = new Vector2(3f, 3f);
        checkRect.offsetMax = new Vector2(-3f, -3f);
        Image checkmark = AddImage(checkObject, AccentColor);
        Toggle toggle = target.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.isOn = false;
        return toggle;
    }

    private TMP_InputField CreateInputField(
        string objectName,
        Transform parent,
        string placeholderValue,
        Vector2 position,
        Vector2 size,
        bool integerOnly)
    {
        GameObject target = TMP_DefaultControls.CreateInputField(default);
        target.name = objectName;
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        SetCenteredRect(rect, position, size);
        ConfigureSolidImage(target.GetComponent<Image>(), FieldColor);
        TMP_InputField input = target.GetComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = integerOnly ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
        input.characterLimit = integerOnly ? 3 : 128;
        ApplyUiFont(input.textComponent);
        input.textComponent.fontSize = 9f;
        input.textComponent.color = TextColor;

        if (input.placeholder is TMP_Text placeholder)
        {
            ApplyUiFont(placeholder);
            placeholder.text = placeholderValue;
            placeholder.fontSize = 9f;
            placeholder.color = new Color(0.55f, 0.62f, 0.68f, 1f);
        }

        return input;
    }

    private TMP_Dropdown CreateDropdown(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject target = TMP_DefaultControls.CreateDropdown(default);
        target.name = objectName;
        target.transform.SetParent(parent, false);
        SetCenteredRect(target.GetComponent<RectTransform>(), position, size);
        TMP_Dropdown dropdown = target.GetComponent<TMP_Dropdown>();
        ConfigureDropdownPresentation(target);
        ApplyUiFont(dropdown.captionText);
        ApplyUiFont(dropdown.itemText);

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 9f;
            dropdown.captionText.color = TextColor;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.fontSize = 9f;
        }

        return dropdown;
    }

    private void ConfigureDropdownPresentation(GameObject dropdownObject)
    {
        ConfigureSolidImage(dropdownObject.GetComponent<Image>(), ButtonColor);
        Transform template = FindChildRecursive(dropdownObject.transform, "Template");
        if (template != null)
        {
            ConfigureSolidImage(template.GetComponent<Image>(), PanelColor);
        }

        Transform viewport = FindChildRecursive(dropdownObject.transform, "Viewport");
        if (viewport != null)
        {
            Mask mask = viewport.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = false;
            }

            Image image = viewport.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
            }

            if (viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
        }

        Transform arrow = FindChildRecursive(dropdownObject.transform, "Arrow");
        if (arrow != null)
        {
            Image image = arrow.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
            }

            Transform glyph = arrow.Find("Glyph") ?? CreateStretchObject("Glyph", arrow).transform;
            TextMeshProUGUI glyphText = glyph.GetComponent<TextMeshProUGUI>();
            if (glyphText == null)
            {
                glyphText = glyph.gameObject.AddComponent<TextMeshProUGUI>();
            }

            ApplyUiFont(glyphText);
            glyphText.text = "v";
            glyphText.fontSize = 9f;
            glyphText.alignment = TextAlignmentOptions.Center;
            glyphText.color = TextColor;
            glyphText.raycastTarget = false;
        }

        Transform itemBackground = FindChildRecursive(dropdownObject.transform, "Item Background");
        if (itemBackground != null)
        {
            ConfigureSolidImage(itemBackground.GetComponent<Image>(), ButtonColor);
        }

        Transform itemCheckmark = FindChildRecursive(dropdownObject.transform, "Item Checkmark");
        if (itemCheckmark != null)
        {
            ConfigureSolidImage(itemCheckmark.GetComponent<Image>(), AccentColor);
        }
    }

    private void ApplyUiFont(TMP_Text text)
    {
        if (text != null && uiFont != null)
        {
            text.font = uiFont;
        }
    }

    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        ConfigureSolidImage(image, color);
        return image;
    }

    private static void ConfigureSolidImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
    }

    private static GameObject CreateRectObject(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        SetCenteredRect(target.GetComponent<RectTransform>(), position, size);
        return target;
    }

    private static GameObject CreateStretchObject(string objectName, Transform parent)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return target;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
