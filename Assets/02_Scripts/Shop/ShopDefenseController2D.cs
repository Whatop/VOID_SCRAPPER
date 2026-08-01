using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점의 포탑 전력 그룹과 근접 보안 드론 호출을 관리한다.
/// 상점 자체는 직접 사격하지 않고 이 컨트롤러를 통해서만 방어한다.
/// </summary>
[DisallowMultipleComponent]
public class ShopDefenseController2D : MonoBehaviour
{
    [Serializable]
    private sealed class PowerGroup
    {
        [Tooltip("이 장치를 파괴하거나 비활성화하면 이 그룹 포탑이 모두 꺼집니다.")]
        public FieldBaseSecurityNode PowerNode;
        public Transform[] TurretSpawnPoints;
        public Transform[] PowerLinkStartPoints;
        public Transform[] PowerLinkEndPoints;

        [NonSerialized] private List<BaseTurretController> runtimeTurrets;
        [NonSerialized] private List<FieldBasePowerLink2D> runtimeLinks;

        public List<BaseTurretController> RuntimeTurrets
        {
            get
            {
                runtimeTurrets ??= new List<BaseTurretController>();
                return runtimeTurrets;
            }
        }

        public List<FieldBasePowerLink2D> RuntimeLinks
        {
            get
            {
                runtimeLinks ??= new List<FieldBasePowerLink2D>();
                return runtimeLinks;
            }
        }
    }

    [Header("Owner")]
    [SerializeField] private ShopStructure shopOwner;

    [Header("Power Groups - 2 Recommended")]
    [SerializeField] private PowerGroup[] powerGroups =
    {
        new PowerGroup(),
        new PowerGroup()
    };

    [Header("Turret Spawning")]
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private Transform runtimeTurretRoot;
    [SerializeField] private bool useSpawnPointRotation = true;
    [SerializeField] private bool skipOccupiedSpawnPoints = true;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Turret Count by Region")]
    [Min(0)]
    [SerializeField] private int region1TurretCount = 4;
    [Min(0)]
    [SerializeField] private int region2TurretCount = 5;
    [Min(0)]
    [SerializeField] private int region3TurretCount = 6;
    [Min(0)]
    [SerializeField] private int finalNetworkTurretCount = 6;

    [Header("Power Links")]
    [SerializeField] private bool createPowerLinks = true;
    [SerializeField] private GameObject powerLinkPrefab;
    [SerializeField] private bool autoCreatePowerLinkWhenPrefabMissing = true;
    [SerializeField] private Transform runtimePowerLinkRoot;

    [Header("Security Drone")]
    [SerializeField] private GameObject[] securityDronePrefabs;
    [SerializeField] private EnemyDefinition securityDroneDefinition;
    [Min(0)]
    [SerializeField] private int region1InitialDroneCount = 2;
    [Min(0)]
    [SerializeField] private int additionalInitialDronePerRegion = 1;
    [Min(0)]
    [SerializeField] private int extraDroneCapacity = 2;
    [Min(0.1f)]
    [SerializeField] private float droneSummonInterval = 8f;
    [Min(0.1f)]
    [SerializeField] private float droneArrivalRadius = 3.25f;
    [SerializeField] private float droneArrivalAngleStep = 72f;
    [SerializeField] private EnemyArrivalSpawnSettings droneArrivalSettings = new EnemyArrivalSpawnSettings();

    [Header("Summon Pulse")]
    [SerializeField] private Color summonPulseColor = new Color(0.3f, 0.95f, 1f, 0.9f);
    [Min(0.1f)]
    [SerializeField] private float summonPulseRadius = 5f;
    [Min(0f)]
    [SerializeField] private float summonPulseMinPush = 0.6f;
    [Min(0f)]
    [SerializeField] private float summonPulseMaxPush = 2.4f;
    [Min(0.02f)]
    [SerializeField] private float summonPushDuration = 0.22f;

    [Header("Debug")]
    [SerializeField] private bool logDefenseSetup;

    private readonly List<GameObject> activeDrones = new List<GameObject>();

    private Transform combatTarget;
    private bool combatActive;
    private bool initialDronesSummoned;
    private float summonTimer;
    private float summonAngleCursor;
    private bool spawned;

    public bool CombatActive => combatActive;

    private void Reset()
    {
        shopOwner = GetComponent<ShopStructure>();
    }

    private void Awake()
    {
        if (shopOwner == null)
        {
            shopOwner = GetComponent<ShopStructure>();
        }

        EnsureRuntimeRoots();
    }

    private void OnEnable()
    {
        SubscribePowerNodes(true);

        if (shopOwner != null)
        {
            shopOwner.StateChanged -= HandleShopStateChanged;
            shopOwner.StateChanged += HandleShopStateChanged;
            shopOwner.Died -= HandleShopDied;
            shopOwner.Died += HandleShopDied;
        }

        SubscribeRuntimeTurretDamage(true);
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnConfiguredTurrets();
        }

        RefreshAllPowerGroups();
    }

    private void OnDisable()
    {
        SubscribePowerNodes(false);

        if (shopOwner != null)
        {
            shopOwner.StateChanged -= HandleShopStateChanged;
            shopOwner.Died -= HandleShopDied;
        }

        SetCombatActive(false, null);
        SubscribeRuntimeTurretDamage(false);
    }

    private void Update()
    {
        if (!combatActive || shopOwner == null || shopOwner.IsDead || !shopOwner.IsHostile)
        {
            return;
        }

        CleanupDrones();
        summonTimer -= Time.deltaTime;

        if (summonTimer <= 0f)
        {
            summonTimer = Mathf.Max(0.1f, droneSummonInterval);
            TrySummonSecurityDrones(1);
        }
    }

    public void SetCombatActive(bool active, Transform playerTarget)
    {
        combatTarget = playerTarget;

        if (combatActive == active)
        {
            return;
        }

        combatActive = active;

        if (!combatActive)
        {
            summonTimer = 0f;
            return;
        }

        summonTimer = Mathf.Max(0.1f, droneSummonInterval);

        if (!initialDronesSummoned)
        {
            initialDronesSummoned = true;
            TrySummonSecurityDrones(ResolveInitialDroneCount());
        }
    }

    [ContextMenu("Spawn Configured Turrets")]
    public void SpawnConfiguredTurrets()
    {
        if (spawned)
        {
            return;
        }

        spawned = true;
        EnsureRuntimeRoots();

        int requestedCount = ResolveTurretCount();
        int remaining = requestedCount;
        int spawnedCount = 0;

        if (powerGroups == null || powerGroups.Length == 0)
        {
            if (logDefenseSetup)
            {
                Debug.LogWarning($"[{name}] 상점 Power Groups가 비어 있습니다.", this);
            }

            return;
        }

        for (int groupIndex = 0; groupIndex < powerGroups.Length; groupIndex++)
        {
            PowerGroup group = powerGroups[groupIndex];

            if (group == null)
            {
                continue;
            }

            int groupsLeft = Mathf.Max(1, powerGroups.Length - groupIndex);
            int groupRequest = Mathf.CeilToInt(remaining / (float)groupsLeft);
            int placed = SpawnTurretGroup(group, groupIndex, groupRequest);
            spawnedCount += placed;
            remaining = Mathf.Max(0, remaining - placed);
        }

        if (logDefenseSetup)
        {
            Debug.Log(
                $"[{name}] 상점 포탑 생성 완료 | 요청 {requestedCount} | 생성/재사용 {spawnedCount}",
                this
            );
        }
    }

    private int SpawnTurretGroup(PowerGroup group, int groupIndex, int requestedCount)
    {
        if (group.TurretSpawnPoints == null || requestedCount <= 0)
        {
            return 0;
        }

        int placed = 0;

        for (int localIndex = 0;
             localIndex < group.TurretSpawnPoints.Length && placed < requestedCount;
             localIndex++)
        {
            Transform spawnPoint = group.TurretSpawnPoints[localIndex];

            if (spawnPoint == null)
            {
                continue;
            }

            BaseTurretController turret = null;

            if (skipOccupiedSpawnPoints)
            {
                turret = spawnPoint.GetComponentInChildren<BaseTurretController>(true);
            }

            if (turret == null && turretPrefab != null)
            {
                Quaternion rotation = useSpawnPointRotation
                    ? spawnPoint.rotation
                    : Quaternion.identity;
                GameObject turretObject = Instantiate(
                    turretPrefab,
                    spawnPoint.position,
                    rotation,
                    runtimeTurretRoot
                );

                turretObject.name = $"{turretPrefab.name}_G{groupIndex}_{localIndex:00}";
                turret = turretObject.GetComponentInChildren<BaseTurretController>(true);
            }

            if (turret == null)
            {
                continue;
            }

            if (!group.RuntimeTurrets.Contains(turret))
            {
                group.RuntimeTurrets.Add(turret);
            }

            turret.ConfigureShopDefense(shopOwner);

            if (turret.TurretHealth != null)
            {
                turret.TurretHealth.Damaged -= HandleShopTurretDamaged;
                turret.TurretHealth.Damaged += HandleShopTurretDamaged;
            }

            turret.SetPowered(IsGroupPowered(group));
            CreatePowerLink(group, groupIndex, localIndex, turret);
            placed++;
        }

        return placed;
    }

    private void CreatePowerLink(
        PowerGroup group,
        int groupIndex,
        int localIndex,
        BaseTurretController turret)
    {
        if (!createPowerLinks || turret == null)
        {
            return;
        }

        Transform startPoint = ResolvePoint(group.PowerLinkStartPoints, localIndex);
        Transform endPoint = ResolvePoint(group.PowerLinkEndPoints, localIndex);

        if (startPoint == null)
        {
            startPoint = group.PowerNode != null ? group.PowerNode.transform : transform;
        }

        if (endPoint == null)
        {
            endPoint = turret.PowerLinkAnchor;
        }

        if (startPoint == null || endPoint == null)
        {
            return;
        }

        GameObject linkObject = null;

        if (powerLinkPrefab != null)
        {
            linkObject = Instantiate(
                powerLinkPrefab,
                Vector3.zero,
                Quaternion.identity,
                runtimePowerLinkRoot
            );
        }
        else if (autoCreatePowerLinkWhenPrefabMissing)
        {
            linkObject = new GameObject($"ShopPowerLink_G{groupIndex}_{localIndex:00}");
            linkObject.transform.SetParent(runtimePowerLinkRoot, false);
        }

        if (linkObject == null)
        {
            return;
        }

        FieldBasePowerLink2D link = linkObject.GetComponent<FieldBasePowerLink2D>();

        if (link == null)
        {
            link = linkObject.AddComponent<FieldBasePowerLink2D>();
        }

        link.Bind(startPoint, endPoint, turret);
        link.SetBasePowered(IsGroupPowered(group));
        group.RuntimeLinks.Add(link);
    }

    private void HandleShopTurretDamaged(EnemyHealth _)
    {
        shopOwner?.ForceHostileFromDefenseSabotage("상점 방어 포탑");
    }

    private void HandlePowerNodeDisabled(FieldBaseSecurityNode disabledNode)
    {
        if (disabledNode == null)
        {
            return;
        }

        RefreshAllPowerGroups();
        shopOwner?.ForceHostileFromSecuritySabotage(disabledNode);
    }

    private void RefreshAllPowerGroups()
    {
        if (powerGroups == null)
        {
            return;
        }

        for (int i = 0; i < powerGroups.Length; i++)
        {
            RefreshPowerGroup(powerGroups[i]);
        }
    }

    private void RefreshPowerGroup(PowerGroup group)
    {
        if (group == null)
        {
            return;
        }

        bool powered = IsGroupPowered(group) && shopOwner != null && !shopOwner.IsDead;

        for (int i = group.RuntimeTurrets.Count - 1; i >= 0; i--)
        {
            BaseTurretController turret = group.RuntimeTurrets[i];

            if (turret == null)
            {
                group.RuntimeTurrets.RemoveAt(i);
                continue;
            }

            turret.SetPowered(powered);
        }

        for (int i = group.RuntimeLinks.Count - 1; i >= 0; i--)
        {
            FieldBasePowerLink2D link = group.RuntimeLinks[i];

            if (link == null)
            {
                group.RuntimeLinks.RemoveAt(i);
                continue;
            }

            link.SetBasePowered(powered);
        }
    }

    private bool IsGroupPowered(PowerGroup group)
    {
        return group != null &&
               (group.PowerNode == null || !group.PowerNode.IsDisabled);
    }

    private void TrySummonSecurityDrones(int count)
    {
        if (count <= 0 || !HasSecurityDronePrefab())
        {
            return;
        }

        CleanupDrones();
        int maxActive = ResolveMaxActiveDroneCount();
        int spawnedCount = 0;

        for (int i = 0; i < count && activeDrones.Count < maxActive; i++)
        {
            GameObject prefab = GetRandomDronePrefab();

            if (prefab == null)
            {
                continue;
            }

            float angle = summonAngleCursor;
            summonAngleCursor = Mathf.Repeat(
                summonAngleCursor + Mathf.Max(1f, droneArrivalAngleStep),
                360f
            );

            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
            Vector2 arrivalPosition = (Vector2)transform.position +
                                      direction * Mathf.Max(0.1f, droneArrivalRadius);

            GameObject drone = PoolManager.Instance != null
                ? PoolManager.Instance.Get(prefab, arrivalPosition, Quaternion.identity)
                : Instantiate(prefab, arrivalPosition, Quaternion.identity);

            if (drone == null)
            {
                continue;
            }

            EnemyBaseAI droneAI = drone.GetComponentInChildren<EnemyBaseAI>(true);

            if (droneAI != null)
            {
                if (securityDroneDefinition != null)
                {
                    droneAI.ApplyDefinition(securityDroneDefinition);
                }

                droneAI.SetTarget(combatTarget);

                if (droneAI.GetComponent<EnemyMeleeChargeController2D>() == null)
                {
                    droneAI.gameObject.AddComponent<EnemyMeleeChargeController2D>();
                }
            }

            activeDrones.Add(drone);
            spawnedCount++;

            bool arrivalStarted = EnemyArrivalSpawnUtility.BeginArrival(
                drone,
                arrivalPosition,
                combatTarget,
                true,
                transform.position,
                droneArrivalSettings
            );

            if (!arrivalStarted && droneAI != null)
            {
                droneAI.EngagePlayer();
            }

            AudioManager.PlayAt(SoundEventIds.SecurityDroneSpawn, arrivalPosition, 0.75f);
        }

        if (spawnedCount > 0)
        {
            EmitSummonPulse();
        }
    }

    private void EmitSummonPulse()
    {
        Vector2 center = transform.position;
        EventEnemyArrivalMarker.SpawnImpact(
            center,
            summonPulseColor,
            Mathf.Max(0.1f, summonPulseRadius),
            0.3f
        );

        PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>();

        if (playerController == null)
        {
            return;
        }

        Vector2 toPlayer = (Vector2)playerController.transform.position - center;
        float distance = toPlayer.magnitude;
        float radius = Mathf.Max(0.1f, summonPulseRadius);

        if (distance > radius)
        {
            return;
        }

        float normalized = Mathf.Clamp01(distance / radius);
        float pushDistance = Mathf.Lerp(
            Mathf.Max(0f, summonPulseMaxPush),
            Mathf.Max(0f, summonPulseMinPush),
            normalized
        );

        playerController.ApplyExternalPush(
            toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.up,
            pushDistance,
            summonPushDuration
        );
    }

    private bool HasSecurityDronePrefab()
    {
        if (securityDronePrefabs != null)
        {
            for (int i = 0; i < securityDronePrefabs.Length; i++)
            {
                if (securityDronePrefabs[i] != null)
                {
                    return true;
                }
            }
        }

        return securityDroneDefinition != null &&
               securityDroneDefinition.EnemyPrefab != null;
    }

    private GameObject GetRandomDronePrefab()
    {
        if (securityDronePrefabs != null && securityDronePrefabs.Length > 0)
        {
            for (int i = 0; i < 16; i++)
            {
                GameObject prefab = securityDronePrefabs[
                    UnityEngine.Random.Range(0, securityDronePrefabs.Length)
                ];

                if (prefab != null)
                {
                    return prefab;
                }
            }
        }

        return securityDroneDefinition != null
            ? securityDroneDefinition.EnemyPrefab
            : null;
    }

    private void CleanupDrones()
    {
        for (int i = activeDrones.Count - 1; i >= 0; i--)
        {
            if (activeDrones[i] == null || !activeDrones[i].activeInHierarchy)
            {
                activeDrones.RemoveAt(i);
            }
        }
    }

    private int ResolveTurretCount()
    {
        return ResolveDepth() switch
        {
            ExpeditionDepth.Normal => Mathf.Max(0, region1TurretCount),
            ExpeditionDepth.DeepZone1 => Mathf.Max(0, region2TurretCount),
            ExpeditionDepth.DeepZone2 => Mathf.Max(0, region3TurretCount),
            ExpeditionDepth.FinalNetwork => Mathf.Max(0, finalNetworkTurretCount),
            _ => Mathf.Max(0, region1TurretCount)
        };
    }

    private int ResolveInitialDroneCount()
    {
        int regionIndex = (int)ResolveDepth();
        return Mathf.Max(0, region1InitialDroneCount + additionalInitialDronePerRegion * regionIndex);
    }

    private int ResolveMaxActiveDroneCount()
    {
        return ResolveInitialDroneCount() + Mathf.Max(0, extraDroneCapacity);
    }

    private ExpeditionDepth ResolveDepth()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.ExpeditionDepth;
        }

        return ExpeditionDepth.Normal;
    }

    private void HandleShopStateChanged(ShopStructure _)
    {
        if (shopOwner != null && !shopOwner.IsHostile)
        {
            initialDronesSummoned = false;
            SetCombatActive(false, null);
        }

        RefreshAllPowerGroups();
    }

    private void HandleShopDied(ShopStructure _)
    {
        SetCombatActive(false, null);
        RefreshAllPowerGroups();
    }

    private void SubscribePowerNodes(bool subscribe)
    {
        if (powerGroups == null)
        {
            return;
        }

        for (int i = 0; i < powerGroups.Length; i++)
        {
            FieldBaseSecurityNode node = powerGroups[i]?.PowerNode;

            if (node == null)
            {
                continue;
            }

            node.Disabled -= HandlePowerNodeDisabled;

            if (subscribe)
            {
                node.Disabled += HandlePowerNodeDisabled;
            }
        }
    }

    private void SubscribeRuntimeTurretDamage(bool subscribe)
    {
        if (powerGroups == null)
        {
            return;
        }

        for (int groupIndex = 0; groupIndex < powerGroups.Length; groupIndex++)
        {
            PowerGroup group = powerGroups[groupIndex];

            if (group == null)
            {
                continue;
            }

            for (int turretIndex = group.RuntimeTurrets.Count - 1;
                 turretIndex >= 0;
                 turretIndex--)
            {
                BaseTurretController turret = group.RuntimeTurrets[turretIndex];

                if (turret == null)
                {
                    group.RuntimeTurrets.RemoveAt(turretIndex);
                    continue;
                }

                EnemyHealth health = turret.TurretHealth;

                if (health == null)
                {
                    continue;
                }

                health.Damaged -= HandleShopTurretDamaged;

                if (subscribe)
                {
                    health.Damaged += HandleShopTurretDamaged;
                }
            }
        }
    }

    private void EnsureRuntimeRoots()
    {
        if (runtimeTurretRoot == null)
        {
            Transform found = transform.Find("RuntimeTurrets");

            if (found == null)
            {
                GameObject root = new GameObject("RuntimeTurrets");
                root.transform.SetParent(transform, false);
                found = root.transform;
            }

            runtimeTurretRoot = found;
        }

        if (runtimePowerLinkRoot == null)
        {
            Transform found = transform.Find("RuntimePowerLinks");

            if (found == null)
            {
                GameObject root = new GameObject("RuntimePowerLinks");
                root.transform.SetParent(transform, false);
                found = root.transform;
            }

            runtimePowerLinkRoot = found;
        }
    }

    private static Transform ResolvePoint(Transform[] points, int index)
    {
        if (points == null || index < 0 || index >= points.Length)
        {
            return null;
        }

        return points[index];
    }
}
