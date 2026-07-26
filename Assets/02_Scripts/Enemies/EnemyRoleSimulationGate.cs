using System.Collections.Generic;
using UnityEngine;

public enum EnemyRoleSimulationLevel
{
    Dormant = 0,
    Preview = 1,
    Active = 2
}

[DisallowMultipleComponent]
public class EnemyRoleSimulationGate : MonoBehaviour
{
    private static readonly HashSet<EnemyRoleSimulationGate> RivalActionOwners = new HashSet<EnemyRoleSimulationGate>();
    private static readonly HashSet<EnemyRoleSimulationGate> ScavengerActionOwners = new HashSet<EnemyRoleSimulationGate>();
    private static readonly List<EnemyRoleSimulationGate> OwnerCleanupBuffer = new List<EnemyRoleSimulationGate>();

    [Header("Role")]
    [SerializeField] private EnemyRoleType roleType = EnemyRoleType.Patrol;
    [SerializeField] private EnemyRoleSimulationLevel currentLevel = EnemyRoleSimulationLevel.Dormant;

    [Header("Player Intervention Window")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float startGraceSeconds = 12f;
    [SerializeField] private float startMovementUnlockDistance = 8f;
    [SerializeField] private float previewDistance = 26f;
    [SerializeField] private float activeDistance = 18f;
    [SerializeField] private float activeHoldSeconds = 8f;
    [SerializeField] private float radarPreviewDuration = 10f;

    [Header("Concurrent Irreversible Actions")]
    [SerializeField] private int maxConcurrentRivalActions = 1;
    [SerializeField] private int maxConcurrentScavengerActions = 1;

    [Header("References")]
    [SerializeField] private RadarTarget radarTarget;

    [Header("Debug")]
    [SerializeField] private bool logTransitions;

    private Transform player;
    private float playerSearchTimer;
    private float enabledTime;
    private float forcedActiveUntil;
    private float activeHoldUntil;
    private Vector2 playerStartPosition;
    private bool hasPlayerStartPosition;
    private bool ownsActionSlot;

    public EnemyRoleType RoleType => roleType;
    public EnemyRoleSimulationLevel CurrentLevel => currentLevel;
    public bool OwnsActionSlot => ownsActionSlot;
    public bool IsIrreversibleProgressAllowed => RefreshState() == EnemyRoleSimulationLevel.Active;
    public bool IsStartGraceComplete => EvaluateStartGraceComplete();
    public float PlayerDistance => player != null
        ? Vector2.Distance(transform.position, player.position)
        : float.PositiveInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        RivalActionOwners.Clear();
        ScavengerActionOwners.Clear();
        OwnerCleanupBuffer.Clear();
    }

    private void Reset()
    {
        radarTarget = GetComponent<RadarTarget>();
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }
    }

    private void OnEnable()
    {
        enabledTime = Time.time;
        forcedActiveUntil = 0f;
        activeHoldUntil = 0f;
        playerSearchTimer = 0f;
        player = null;
        hasPlayerStartPosition = false;
        ownsActionSlot = false;
        currentLevel = IsGatedRole(roleType)
            ? EnemyRoleSimulationLevel.Dormant
            : EnemyRoleSimulationLevel.Active;
    }

    private void OnDisable()
    {
        ReleaseIrreversibleSlot();
    }

    public void SetRoleType(EnemyRoleType newRoleType)
    {
        if (roleType != newRoleType)
        {
            ReleaseIrreversibleSlot();
        }

        roleType = newRoleType;
        currentLevel = IsGatedRole(roleType)
            ? EnemyRoleSimulationLevel.Dormant
            : EnemyRoleSimulationLevel.Active;
    }

    public void Configure(
        EnemyRoleType newRoleType,
        float newStartGraceSeconds,
        float newStartMovementUnlockDistance,
        float newPreviewDistance,
        float newActiveDistance,
        float newActiveHoldSeconds,
        float newRadarPreviewDuration,
        int newMaxConcurrentRivalActions,
        int newMaxConcurrentScavengerActions)
    {
        SetRoleType(newRoleType);
        startGraceSeconds = Mathf.Max(0f, newStartGraceSeconds);
        startMovementUnlockDistance = Mathf.Max(0f, newStartMovementUnlockDistance);
        previewDistance = Mathf.Max(0.1f, newPreviewDistance);
        activeDistance = Mathf.Clamp(newActiveDistance, 0.1f, previewDistance);
        activeHoldSeconds = Mathf.Max(0f, newActiveHoldSeconds);
        radarPreviewDuration = Mathf.Max(0f, newRadarPreviewDuration);
        maxConcurrentRivalActions = Mathf.Max(1, newMaxConcurrentRivalActions);
        maxConcurrentScavengerActions = Mathf.Max(1, newMaxConcurrentScavengerActions);
    }

    public EnemyRoleSimulationLevel RefreshState()
    {
        ResolvePlayer();

        EnemyRoleSimulationLevel nextLevel = EvaluateLevel();

        if (nextLevel != currentLevel)
        {
            EnemyRoleSimulationLevel previous = currentLevel;
            currentLevel = nextLevel;

            if (logTransitions)
            {
                Debug.Log($"[{name}/{roleType}] Role simulation: {previous} -> {currentLevel}", this);
            }
        }

        if (currentLevel != EnemyRoleSimulationLevel.Active)
        {
            ReleaseIrreversibleSlot();
        }

        return currentLevel;
    }

    public void ForceActive(float duration = -1f)
    {
        float finalDuration = duration >= 0f ? duration : activeHoldSeconds;
        forcedActiveUntil = Mathf.Max(forcedActiveUntil, Time.time + Mathf.Max(0.1f, finalDuration));
        activeHoldUntil = Mathf.Max(activeHoldUntil, forcedActiveUntil);
        RefreshState();
    }

    public bool TryAcquireIrreversibleSlot()
    {
        if (!IsGatedRole(roleType))
        {
            return true;
        }

        if (RefreshState() != EnemyRoleSimulationLevel.Active)
        {
            return false;
        }

        HashSet<EnemyRoleSimulationGate> owners = GetOwnerSet(roleType);

        if (owners == null)
        {
            return true;
        }

        CleanupOwners(owners, roleType);

        if (owners.Contains(this))
        {
            ownsActionSlot = true;
            return true;
        }

        int limit = roleType == EnemyRoleType.RivalHarvester
            ? Mathf.Max(1, maxConcurrentRivalActions)
            : Mathf.Max(1, maxConcurrentScavengerActions);

        if (owners.Count >= limit)
        {
            ownsActionSlot = false;
            return false;
        }

        owners.Add(this);
        ownsActionSlot = true;
        return true;
    }

    public void ReleaseIrreversibleSlot()
    {
        RivalActionOwners.Remove(this);
        ScavengerActionOwners.Remove(this);
        ownsActionSlot = false;
    }

    public bool ShouldNotifyPlayer(float nearbyDistance)
    {
        ResolvePlayer();

        if (player != null && Vector2.Distance(transform.position, player.position) <= Mathf.Max(0f, nearbyDistance))
        {
            return true;
        }

        return radarTarget != null && radarTarget.WasScannedRecently(radarPreviewDuration);
    }

    private EnemyRoleSimulationLevel EvaluateLevel()
    {
        if (!IsGatedRole(roleType))
        {
            return EnemyRoleSimulationLevel.Active;
        }

        if (Time.time <= forcedActiveUntil)
        {
            return EnemyRoleSimulationLevel.Active;
        }

        if (!EvaluateStartGraceComplete())
        {
            return EnemyRoleSimulationLevel.Dormant;
        }

        if (player == null)
        {
            return EnemyRoleSimulationLevel.Dormant;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= activeDistance)
        {
            activeHoldUntil = Time.time + activeHoldSeconds;
            return EnemyRoleSimulationLevel.Active;
        }

        if (Time.time <= activeHoldUntil)
        {
            return EnemyRoleSimulationLevel.Active;
        }

        if (distance <= previewDistance ||
            (radarTarget != null && radarTarget.WasScannedRecently(radarPreviewDuration)))
        {
            return EnemyRoleSimulationLevel.Preview;
        }

        return EnemyRoleSimulationLevel.Dormant;
    }

    private bool EvaluateStartGraceComplete()
    {
        if (!IsGatedRole(roleType))
        {
            return true;
        }

        if (Time.time - enabledTime >= startGraceSeconds)
        {
            return true;
        }

        ResolvePlayer();

        if (player == null || !hasPlayerStartPosition)
        {
            return false;
        }

        return Vector2.Distance(player.position, playerStartPosition) >= startMovementUnlockDistance;
    }

    private void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
        {
            CapturePlayerStartPositionIfNeeded();
            return;
        }

        playerSearchTimer -= Time.deltaTime;

        if (playerSearchTimer > 0f)
        {
            return;
        }

        playerSearchTimer = 0.25f;

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        CapturePlayerStartPositionIfNeeded();
    }

    private void CapturePlayerStartPositionIfNeeded()
    {
        if (player == null || hasPlayerStartPosition)
        {
            return;
        }

        playerStartPosition = player.position;
        hasPlayerStartPosition = true;
    }

    private static bool IsGatedRole(EnemyRoleType type)
    {
        return type == EnemyRoleType.RivalHarvester || type == EnemyRoleType.Scavenger;
    }

    private static HashSet<EnemyRoleSimulationGate> GetOwnerSet(EnemyRoleType type)
    {
        return type switch
        {
            EnemyRoleType.RivalHarvester => RivalActionOwners,
            EnemyRoleType.Scavenger => ScavengerActionOwners,
            _ => null
        };
    }

    private static void CleanupOwners(HashSet<EnemyRoleSimulationGate> owners, EnemyRoleType expectedRole)
    {
        OwnerCleanupBuffer.Clear();

        foreach (EnemyRoleSimulationGate owner in owners)
        {
            if (owner == null ||
                !owner.isActiveAndEnabled ||
                owner.roleType != expectedRole ||
                owner.currentLevel != EnemyRoleSimulationLevel.Active)
            {
                OwnerCleanupBuffer.Add(owner);
            }
        }

        for (int i = 0; i < OwnerCleanupBuffer.Count; i++)
        {
            EnemyRoleSimulationGate owner = OwnerCleanupBuffer[i];

            if (owner != null)
            {
                owner.ownsActionSlot = false;
            }

            owners.Remove(owner);
        }

        OwnerCleanupBuffer.Clear();
    }
}
