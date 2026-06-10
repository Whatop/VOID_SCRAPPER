using System.Collections.Generic;
using UnityEngine;

public enum ExpeditionEventType
{
    RescueSignal,
    UnknownDevice
}

public enum ExpeditionEventState
{
    Idle,
    Active,
    Completed
}

[RequireComponent(typeof(Collider2D))]
public class ExpeditionEventObject : MonoBehaviour, IInteractable
{
    [Header("Event")]
    [SerializeField] private ExpeditionEventType eventType = ExpeditionEventType.RescueSignal;
    [SerializeField] private ExpeditionEventState state = ExpeditionEventState.Idle;

    [Header("Interaction Text")]
    [SerializeField] private string rescueIdleText = "구조 신호 조사";
    [SerializeField] private string unknownDeviceIdleText = "미확인 장치 작동";
    [SerializeField] private string activeText = "이벤트 진행 중";
    [SerializeField] private string completedText = "이벤트 완료";

    [Header("Enemy Definitions")]
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;

    [Header("Enemy Prefab Fallback")]
    [SerializeField] private GameObject basicEnemyPrefabFallback;
    [SerializeField] private GameObject shotgunEnemyPrefabFallback;

    [Header("Rescue Signal")]
    [SerializeField] private int rescueBasicEnemyCount = 3;
    [SerializeField] private int rescueShotgunEnemyCount = 1;
    [SerializeField] private int rescueRewardXp = 5;
    [SerializeField] private int rescueRewardCredits = 10;
    [SerializeField] private int rescueRewardScrap = 2;

    [Header("Unknown Device")]
    [Range(0f, 1f)]
    [SerializeField] private float unknownImmediateRewardChance = 0.5f;
    [SerializeField] private int unknownImmediateCredits = 8;
    [SerializeField] private int unknownImmediateScrap = 1;
    [SerializeField] private int unknownCombatBasicEnemyCount = 2;
    [SerializeField] private int unknownCombatShotgunEnemyCount = 1;
    [SerializeField] private int unknownCombatRewardXp = 4;
    [SerializeField] private int unknownCombatRewardCredits = 12;
    [SerializeField] private int unknownCombatRewardScrap = 2;

    [Header("Spawn")]
    [SerializeField] private float spawnRadiusMin = 2.5f;
    [SerializeField] private float spawnRadiusMax = 4.5f;
    [SerializeField] private bool alertSpawnedEnemies = true;
    [SerializeField] private bool suppressSpawnedEnemyNormalRewards = true;
    [SerializeField] private float deepZoneEnemyHpMultiplier = 1.2f;

    [Header("Visual / Radar")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private bool hideVisualOnCompleted = true;
    [SerializeField] private bool autoConfigureRadarTarget = true;
    [SerializeField] private RadarTarget radarTarget;

    [Header("Debug")]
    [SerializeField] private bool logEventFlow;

    private readonly List<EnemyHealth> trackedEnemies = new List<EnemyHealth>();

    private Collider2D eventCollider;
    private Transform player;
    private ExpeditionHUD expeditionHUD;

    public string InteractionText
    {
        get
        {
            if (state == ExpeditionEventState.Completed)
            {
                return completedText;
            }

            if (state == ExpeditionEventState.Active)
            {
                return activeText;
            }

            return eventType == ExpeditionEventType.RescueSignal
                ? rescueIdleText
                : unknownDeviceIdleText;
        }
    }

    private void Reset()
    {
        eventCollider = GetComponent<Collider2D>();
        visualRoot = gameObject;
        radarTarget = GetComponent<RadarTarget>();

        if (eventCollider != null)
        {
            eventCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        eventCollider = GetComponent<Collider2D>();

        if (eventCollider != null)
        {
            eventCollider.isTrigger = true;
        }

        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (autoConfigureRadarTarget)
        {
            ConfigureRadarTarget();
        }
    }

    private void Start()
    {
        ResolvePlayer();
        expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
    }

    private void Update()
    {
        if (state != ExpeditionEventState.Active)
        {
            return;
        }

        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0)
        {
            CompleteCombatEvent();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (state != ExpeditionEventState.Idle)
        {
            return false;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (interactor != null)
        {
            player = interactor.transform;
        }

        switch (eventType)
        {
            case ExpeditionEventType.RescueSignal:
                StartRescueSignal();
                break;

            case ExpeditionEventType.UnknownDevice:
                TriggerUnknownDevice();
                break;
        }
    }

    private void StartRescueSignal()
    {
        state = ExpeditionEventState.Active;
        ShowMessage("구조 신호 확인. 적을 소탕해라.");

        SpawnEnemyBatch(basicEnemyDefinition, basicEnemyPrefabFallback, rescueBasicEnemyCount);
        SpawnEnemyBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, rescueShotgunEnemyCount);

        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0)
        {
            CompleteCombatEvent();
        }

        Log("구조 신호 이벤트 시작");
    }

    private void TriggerUnknownDevice()
    {
        float roll = Random.value;

        if (roll <= unknownImmediateRewardChance)
        {
            GrantReward(0, unknownImmediateCredits, unknownImmediateScrap, 0);
            ShowMessage("미확인 장치에서 자원을 회수했다.");
            CompleteEvent();
            Log("미확인 장치 즉시 보상");
            return;
        }

        state = ExpeditionEventState.Active;
        ShowMessage("미확인 장치가 적 증원을 호출했다.");

        SpawnEnemyBatch(basicEnemyDefinition, basicEnemyPrefabFallback, unknownCombatBasicEnemyCount);
        SpawnEnemyBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, unknownCombatShotgunEnemyCount);

        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0)
        {
            CompleteCombatEvent();
        }

        Log("미확인 장치 전투 분기 시작");
    }

    private void CompleteCombatEvent()
    {
        if (state != ExpeditionEventState.Active)
        {
            return;
        }

        switch (eventType)
        {
            case ExpeditionEventType.RescueSignal:
                GrantReward(rescueRewardXp, rescueRewardCredits, rescueRewardScrap, 0);
                ShowMessage("구조 신호 클리어. 보상을 획득했다.");
                break;

            case ExpeditionEventType.UnknownDevice:
                GrantReward(unknownCombatRewardXp, unknownCombatRewardCredits, unknownCombatRewardScrap, 0);
                ShowMessage("미확인 장치 증원을 격파했다.");
                break;
        }

        CompleteEvent();
    }

    private void CompleteEvent()
    {
        state = ExpeditionEventState.Completed;

        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = trackedEnemies[i];

            if (enemy != null)
            {
                enemy.Died -= HandleTrackedEnemyDied;
            }
        }

        trackedEnemies.Clear();

        if (eventCollider != null)
        {
            eventCollider.enabled = false;
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        if (hideVisualOnCompleted && visualRoot != null)
        {
            visualRoot.SetActive(false);
        }

        Log("이벤트 완료");
    }

    private void SpawnEnemyBatch(EnemyDefinition definition, GameObject prefabFallback, int count)
    {
        count = Mathf.Max(0, count);

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy(definition, prefabFallback);
        }
    }

    private GameObject SpawnEnemy(EnemyDefinition definition, GameObject prefabFallback)
    {
        GameObject prefab = definition != null && definition.EnemyPrefab != null
            ? definition.EnemyPrefab
            : prefabFallback;

        if (prefab == null)
        {
            Debug.LogWarning("이벤트 적 프리팹이 비어 있습니다.", this);
            return null;
        }

        Vector2 spawnPosition = GetSpawnPosition();
        GameObject enemyObject = Instantiate(prefab, spawnPosition, Quaternion.identity);

        EnemyBaseAI enemyAI = enemyObject.GetComponent<EnemyBaseAI>();
        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

        if (enemyAI != null)
        {
            if (definition != null)
            {
                enemyAI.ApplyDefinition(definition);
            }

            ResolvePlayer();

            if (player != null)
            {
                enemyAI.SetTarget(player);

                if (alertSpawnedEnemies)
                {
                    enemyAI.AlertTo(player.position);
                }
            }
        }
        else if (enemyHealth != null && definition != null)
        {
            enemyHealth.ApplyDefinition(definition);
        }

        ApplyDeepZoneEnemyModifier(enemyHealth);
        SuppressNormalRewardIfNeeded(enemyObject);
        TrackEnemy(enemyHealth);

        return enemyObject;
    }

    private void TrackEnemy(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }

        if (trackedEnemies.Contains(enemyHealth))
        {
            return;
        }

        trackedEnemies.Add(enemyHealth);
        enemyHealth.Died += HandleTrackedEnemyDied;
    }

    private void HandleTrackedEnemyDied(EnemyHealth enemyHealth)
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleTrackedEnemyDied;
        }

        trackedEnemies.Remove(enemyHealth);
        CleanupTrackedEnemies();

        if (state == ExpeditionEventState.Active && trackedEnemies.Count <= 0)
        {
            CompleteCombatEvent();
        }
    }

    private void CleanupTrackedEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = trackedEnemies[i];

            if (enemy == null || enemy.IsDead)
            {
                if (enemy != null)
                {
                    enemy.Died -= HandleTrackedEnemyDied;
                }

                trackedEnemies.RemoveAt(i);
            }
        }
    }

    private Vector2 GetSpawnPosition()
    {
        Vector2 direction = Random.insideUnitCircle;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float radius = Random.Range(
            Mathf.Max(0.1f, spawnRadiusMin),
            Mathf.Max(spawnRadiusMin + 0.1f, spawnRadiusMax)
        );

        return (Vector2)transform.position + direction * radius;
    }

    private void ApplyDeepZoneEnemyModifier(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        if (RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1)
        {
            return;
        }

        float multiplier = Mathf.Max(0.01f, deepZoneEnemyHpMultiplier);
        enemyHealth.SetMaxHp(enemyHealth.MaxHp * multiplier, true);
    }

    private void SuppressNormalRewardIfNeeded(GameObject enemyObject)
    {
        if (!suppressSpawnedEnemyNormalRewards || enemyObject == null)
        {
            return;
        }

        RewardDropper dropper = enemyObject.GetComponent<RewardDropper>();

        if (dropper != null)
        {
            dropper.SetRewardDefinition(null);
        }
    }

    private void GrantReward(int xp, int credits, int scrap, int core)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        if (xp > 0)
        {
            RunManager.Instance.AddCurrency(CurrencyType.Experience, xp);
        }

        if (credits > 0)
        {
            RunManager.Instance.AddCurrency(CurrencyType.Credits, credits);
        }

        if (scrap > 0)
        {
            RunManager.Instance.AddCurrency(CurrencyType.ScrapParts, scrap);
        }

        if (core > 0)
        {
            RunManager.Instance.AddCurrency(CurrencyType.CoreShards, core);
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void ConfigureRadarTarget()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (radarTarget == null)
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
        }

        radarTarget.SetMarkerType(RadarMarkerType.Event);
        radarTarget.SetVisible(state != ExpeditionEventState.Completed);
    }

    private void ShowMessage(string message)
    {
        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowWarning(message);
        }
    }

    private void Log(string message)
    {
        if (!logEventFlow)
        {
            return;
        }

        Debug.Log($"[{name}] {message}", this);
    }
}