using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CoreBossIntroSequence : MonoBehaviour
{
    [Header("Warning")]
    [SerializeField] private WarningMessageUI warningMessageUI;
    [SerializeField] private string activationWarningMessage = "고 에너지 방출 감지!";
    [SerializeField] private float warningDuration = 1.4f;
    [SerializeField] private float delayAfterWarning = 0.25f;

    [Header("Motion Title - Core Activation")]
    [SerializeField] private bool useMotionTitleForCoreActivation = true;
    [SerializeField] private EventTitleDirector coreActivationTitleDirector;
    [SerializeField] private EventTitleType coreActivationTitleType = EventTitleType.CoreReaction;
    [SerializeField] private string coreActivationTitle = "코어 활성화";
    [SerializeField] private string coreActivationSubtitle = "구획 관리자 신호 감지";
    [SerializeField] private float coreActivationTitleWait = 1.2f;
    [SerializeField] private bool fallbackToWarningMessageIfTitleMissing = true;

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerInput = true;
    [SerializeField] private bool makePlayerInvincibleDuringIntro = true;
    [SerializeField] private float playerInvincibleExtraTime = 0.5f;
    [SerializeField] private MonoBehaviour[] extraPlayerComponentsToDisable;

    [Header("HUD")]
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private bool hideStatusAndResourceUIDuringIntro = true;

    [Header("Camera")]
    [SerializeField] private CameraZoomController2D cameraZoomController;
    [SerializeField] private float wideZoomMultiplier = 3.5f;
    [SerializeField] private float zoomOutWait = 0.8f;
    [SerializeField] private float zoomInWait = 0.75f;
    [SerializeField] private bool resetCameraZoomOnBattleStart = true;
    [SerializeField] private bool returnCameraZoomAfterIntro = true;
    [SerializeField] private bool createCameraZoomControllerIfMissing = true;

    [Header("Arena")]
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(14f, 14f);

    [Range(0.2f, 1f)]
    [SerializeField] private float verticalSpaceScale = 0.7f;

    [Tooltip("코어보다 위쪽을 보스전 중심으로 쓰기 위한 오프셋")]
    [SerializeField] private Vector2 arenaCenterOffset = new Vector2(0f, 2.5f);

    [Tooltip("Boss Spawn Point가 코어와 겹칠 때 추가로 위로 올리는 오프셋")]
    [SerializeField] private Vector2 bossBattlePositionOffset = new Vector2(0f, 2.5f);

    [Header("Laser Manager Ships")]
    [SerializeField] private GameObject laserManagerShipPrefab;
    [SerializeField] private bool createRuntimePlaceholderIfMissing = true;
    [SerializeField] private bool keepManagerShipsUntilBossDeath = true;
    [SerializeField] private float managerShipBaseRotationZ = 45f;
    [SerializeField] private float managerShipStartExtraDistance = 7f;
    [SerializeField] private float managerShipMoveDuration = 0.9f;
    [SerializeField] private AnimationCurve managerShipMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Runtime Placeholder")]
    [SerializeField] private Color runtimePlaceholderColor = new Color(0.15f, 0.8f, 1f, 1f);
    [SerializeField] private float runtimePlaceholderSize = 0.45f;
    [SerializeField] private float runtimePlaceholderLineWidth = 0.05f;

    [Header("Laser Wall")]
    [SerializeField] private BossArenaLaserWall laserWallPrefab;
    [SerializeField] private float wallThickness = 0.45f;
    [SerializeField] private bool createSolidLaserWalls = true;
    [SerializeField] private bool cleanupArenaObjectsOnBossDeath = true;
    [SerializeField] private float wallDamage = 4f;
    [SerializeField] private float wallDamageInterval = 0.5f;
    [SerializeField] private Material laserLineMaterial;
    [SerializeField] private Color laserLineColor = Color.white;
    [SerializeField] private string laserWallLayerName = "Default";
    [SerializeField] private string laserSortingLayerName = "Default";
    [SerializeField] private int laserSortingOrder = 30;
    [SerializeField] private float delayBeforeWallActivation = 0.2f;
    [SerializeField] private float delayAfterWallActivation = 0.55f;

    [Header("Boss Arrival")]
    [SerializeField] private Vector2 bossArrivalDirection = Vector2.up;
    [SerializeField] private float bossArrivalDistance = 11f;
    [SerializeField] private float bossArrivalDuration = 1.15f;
    [SerializeField] private AnimationCurve bossMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool faceBossToPlayerWhenArrived = true;
    [SerializeField] private float bossRotationOffset = -90f;

    [Header("Debug")]
    [SerializeField] private bool logSequence;

    private readonly List<LaserGuardianDrone> spawnedManagerShips = new List<LaserGuardianDrone>(4);
    private readonly List<GameObject> spawnedWallObjects = new List<GameObject>(4);
    private readonly List<MonoBehaviour> disabledBossComponents = new List<MonoBehaviour>();
    private readonly List<ComponentEnabledState> extraDisabledPlayerComponents = new List<ComponentEnabledState>();

    private PlayerLockState playerLockState;
    private EnemyHealth trackedBossHealth;
    private GameObject spawnedBoss;
    private bool isPlaying;

    public GameObject SpawnedBoss => spawnedBoss;
    public bool IsPlaying => isPlaying;

    private struct ComponentEnabledState
    {
        public MonoBehaviour component;
        public bool wasEnabled;
    }

    private struct PlayerLockState
    {
        public bool hasState;
        public Rigidbody2D rb;

        public PlayerController2D controller;
        public bool controllerControlWasEnabled;
        public bool controllerMovementWasLocked;

        public PlayerDash dash;
        public bool dashWasEnabled;

        public PlayerWeaponController weaponController;
        public bool weaponWasEnabled;
        public WeaponTreeType weaponTreeBeforeLock;

        public PlayerInteractor interactor;
        public bool interactorWasEnabled;

        public MonoBehaviour radarScanner;
        public bool radarWasEnabled;

        public EmergencyReturnController emergencyReturnController;
        public bool emergencyReturnWasEnabled;
    }

    private void Reset()
    {
        cameraZoomController = FindFirstObjectByType<CameraZoomController2D>();
        warningMessageUI = FindFirstObjectByType<WarningMessageUI>();
        expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
    }

    private void OnDisable()
    {
        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        if (hideStatusAndResourceUIDuringIntro && expeditionHUD != null)
        {
            expeditionHUD.SetCinematicMode(false);
        }

        if (returnCameraZoomAfterIntro || resetCameraZoomOnBattleStart)
        {
            ResetCameraZoom();
        }

        if (isPlaying)
        {
            RestorePlayer();
            EnableBossForBattle();
            isPlaying = false;
        }
    }

    public IEnumerator PlayIntroRoutine(
        GameObject interactor,
        GameObject bossPrefab,
        Vector3 bossBattlePosition,
        Vector3 arenaCenter,
        Action<GameObject> bossCreatedCallback,
        Action battleStartCallback)
    {
        if (isPlaying)
        {
            yield break;
        }

        isPlaying = true;
        spawnedBoss = null;

        ResolveReferences();

        Vector3 effectiveArenaCenter = arenaCenter + (Vector3)arenaCenterOffset;
        Vector3 effectiveBossBattlePosition = bossBattlePosition + (Vector3)bossBattlePositionOffset;

        if (logSequence)
        {
            Debug.Log("코어 보스 인트로 시작", this);
        }

        if (hideStatusAndResourceUIDuringIntro && expeditionHUD != null)
        {
            expeditionHUD.SetCinematicMode(true);
        }

        if (lockPlayerInput)
        {
            LockPlayer(interactor);
        }

        ApplyIntroInvincibility(interactor);
        yield return PlayCoreActivationNoticeRoutine();

        SetCameraWide();
        yield return Wait(zoomOutWait);

        yield return SpawnAndMoveManagerShipsRoutine(effectiveArenaCenter);

        spawnedBoss = SpawnBossForIntro(bossPrefab, effectiveBossBattlePosition);
        bossCreatedCallback?.Invoke(spawnedBoss);

        DisableBossForIntro(spawnedBoss);

        yield return MoveBossArrivalRoutine(spawnedBoss, effectiveBossBattlePosition, interactor);

        if (delayBeforeWallActivation > 0f)
        {
            yield return new WaitForSeconds(delayBeforeWallActivation);
        }

        ActivateLaserWallsFromManagerShips();
        TrackBossDeathForCleanup(spawnedBoss);

        if (delayAfterWallActivation > 0f)
        {
            yield return new WaitForSeconds(delayAfterWallActivation);
        }

        if (resetCameraZoomOnBattleStart || returnCameraZoomAfterIntro)
        {
            ResetCameraZoom();
            yield return Wait(zoomInWait);
        }

        EnableBossForBattle();
        battleStartCallback?.Invoke();

        if (lockPlayerInput)
        {
            RestorePlayer();
        }

        if (hideStatusAndResourceUIDuringIntro && expeditionHUD != null)
        {
            expeditionHUD.SetCinematicMode(false);
        }

        if (!keepManagerShipsUntilBossDeath)
        {
            DestroySpawnedManagerShips();
        }

        if (logSequence)
        {
            Debug.Log("코어 보스 인트로 종료. 보스전 시작.", this);
        }

        isPlaying = false;
    }

    private void ResolveReferences()
    {
        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        }

        if (cameraZoomController == null)
        {
            cameraZoomController = FindFirstObjectByType<CameraZoomController2D>(FindObjectsInactive.Include);
        }

        if (cameraZoomController == null && createCameraZoomControllerIfMissing)
        {
            CinemachineCamera cmCamera = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);

            if (cmCamera != null)
            {
                cameraZoomController = cmCamera.GetComponent<CameraZoomController2D>();

                if (cameraZoomController == null)
                {
                    cameraZoomController = cmCamera.gameObject.AddComponent<CameraZoomController2D>();
                }
            }
        }

        if (cameraZoomController == null && createCameraZoomControllerIfMissing && Camera.main != null)
        {
            cameraZoomController = Camera.main.GetComponent<CameraZoomController2D>();

            if (cameraZoomController == null)
            {
                cameraZoomController = Camera.main.gameObject.AddComponent<CameraZoomController2D>();
            }
        }

        if (warningMessageUI == null)
        {
            warningMessageUI = FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        }
    }

    private Vector2 GetEffectiveHalfExtents()
    {
        return new Vector2(
            Mathf.Max(0.1f, arenaHalfExtents.x),
            Mathf.Max(0.1f, arenaHalfExtents.y * verticalSpaceScale)
        );
    }

    private IEnumerator SpawnAndMoveManagerShipsRoutine(Vector3 arenaCenter)
    {
        DestroySpawnedManagerShips();
        spawnedManagerShips.Clear();

        Vector2 half = GetEffectiveHalfExtents();

        Vector2[] cornerSigns =
        {
            new Vector2(-1f, -1f),
            new Vector2(-1f,  1f),
            new Vector2( 1f,  1f),
            new Vector2( 1f, -1f),
        };

        float[] rotationZ =
        {
            managerShipBaseRotationZ + 0f,
            managerShipBaseRotationZ - 90f,
            managerShipBaseRotationZ + 180f,
            managerShipBaseRotationZ + 90f
        };

        string[] names =
        {
            "LaserManagerShip_01_BottomLeft",
            "LaserManagerShip_02_TopLeft",
            "LaserManagerShip_03_TopRight",
            "LaserManagerShip_04_BottomRight"
        };

        Vector3[] startPositions = new Vector3[4];
        Vector3[] endPositions = new Vector3[4];

        for (int i = 0; i < 4; i++)
        {
            Vector2 sign = cornerSigns[i];
            Vector3 cornerOffset = new Vector3(sign.x * half.x, sign.y * half.y, 0f);
            Vector3 outward = cornerOffset.sqrMagnitude > 0.001f ? cornerOffset.normalized : Vector3.up;

            endPositions[i] = arenaCenter + cornerOffset;
            startPositions[i] = endPositions[i] + outward * Mathf.Max(0f, managerShipStartExtraDistance);

            LaserGuardianDrone managerShip = CreateManagerShip(
                names[i],
                i + 1,
                startPositions[i],
                rotationZ[i],
                Color.white
            );

            if (managerShip != null)
            {
                spawnedManagerShips.Add(managerShip);
            }
        }

        float duration = Mathf.Max(0.01f, managerShipMoveDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = managerShipMoveCurve != null ? managerShipMoveCurve.Evaluate(t) : t;

            for (int i = 0; i < spawnedManagerShips.Count; i++)
            {
                LaserGuardianDrone ship = spawnedManagerShips[i];

                if (ship == null)
                {
                    continue;
                }

                ship.transform.position = Vector3.LerpUnclamped(startPositions[i], endPositions[i], eased);
                ship.ApplySlotRotation(rotationZ[i]);
            }

            yield return null;
        }

        for (int i = 0; i < spawnedManagerShips.Count; i++)
        {
            LaserGuardianDrone ship = spawnedManagerShips[i];

            if (ship == null)
            {
                continue;
            }

            ship.transform.position = endPositions[i];
            ship.ApplySlotRotation(rotationZ[i]);
        }
    }

    private LaserGuardianDrone CreateManagerShip(
        string objectName,
        int runtimeIndex,
        Vector3 position,
        float rotationZ,
        Color tint)
    {
        GameObject shipObject;

        if (laserManagerShipPrefab != null)
        {
            shipObject = Instantiate(
                laserManagerShipPrefab,
                position,
                Quaternion.Euler(0f, 0f, rotationZ)
            );
        }
        else
        {
            if (!createRuntimePlaceholderIfMissing)
            {
                return null;
            }

            shipObject = CreateRuntimePlaceholderShip(objectName, position, rotationZ);
        }

        shipObject.name = objectName;

        LaserGuardianDrone drone = shipObject.GetComponent<LaserGuardianDrone>();

        if (drone == null)
        {
            drone = shipObject.AddComponent<LaserGuardianDrone>();
        }

        drone.SetRuntimeIndex(runtimeIndex);
        drone.ApplySlotRotation(rotationZ);
        drone.SetTint(tint);

        return drone;
    }

    private GameObject CreateRuntimePlaceholderShip(string objectName, Vector3 position, float rotationZ)
    {
        GameObject shipObject = new GameObject(objectName);
        shipObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationZ));

        shipObject.AddComponent<LaserGuardianDrone>();

        LineRenderer lineRenderer = shipObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 5;

        float size = Mathf.Max(0.05f, runtimePlaceholderSize);

        lineRenderer.SetPosition(0, new Vector3(0f, size, 0f));
        lineRenderer.SetPosition(1, new Vector3(size, 0f, 0f));
        lineRenderer.SetPosition(2, new Vector3(0f, -size, 0f));
        lineRenderer.SetPosition(3, new Vector3(-size, 0f, 0f));
        lineRenderer.SetPosition(4, new Vector3(0f, size, 0f));

        lineRenderer.startWidth = Mathf.Max(0.001f, runtimePlaceholderLineWidth);
        lineRenderer.endWidth = Mathf.Max(0.001f, runtimePlaceholderLineWidth);
        lineRenderer.startColor = runtimePlaceholderColor;
        lineRenderer.endColor = runtimePlaceholderColor;
        lineRenderer.sortingLayerName = laserSortingLayerName;
        lineRenderer.sortingOrder = laserSortingOrder + 1;

        if (laserLineMaterial != null)
        {
            lineRenderer.material = laserLineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }

        return shipObject;
    }

    private void ActivateLaserWallsFromManagerShips()
    {
        DestroySpawnedWalls();

        if (spawnedManagerShips.Count < 4)
        {
            return;
        }

        CreateLaserWallBetween("BossArenaWall_Left", spawnedManagerShips[0], spawnedManagerShips[1]);
        CreateLaserWallBetween("BossArenaWall_Top", spawnedManagerShips[1], spawnedManagerShips[2]);
        CreateLaserWallBetween("BossArenaWall_Right", spawnedManagerShips[2], spawnedManagerShips[3]);
        CreateLaserWallBetween("BossArenaWall_Bottom", spawnedManagerShips[3], spawnedManagerShips[0]);
    }

    private void CreateLaserWallBetween(string objectName, LaserGuardianDrone startDrone, LaserGuardianDrone endDrone)
    {
        if (startDrone == null || endDrone == null)
        {
            return;
        }

        Vector2 start = startDrone.transform.position;
        Vector2 end = endDrone.transform.position;
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;

        BossArenaLaserWall wall;

        if (laserWallPrefab != null)
        {
            wall = Instantiate(laserWallPrefab);
            wall.name = objectName;
        }
        else
        {
            GameObject wallObject = new GameObject(objectName);
            wall = wallObject.AddComponent<BossArenaLaserWall>();
        }

        wall.Initialize(
            center,
            direction,
            length,
            wallThickness,
            createSolidLaserWalls,
            wallDamage,
            wallDamageInterval,
            laserLineMaterial,
            laserLineColor,
            laserSortingLayerName,
            laserSortingOrder,
            laserWallLayerName
        );

        spawnedWallObjects.Add(wall.gameObject);
    }

    private GameObject SpawnBossForIntro(GameObject bossPrefab, Vector3 bossBattlePosition)
    {
        if (bossPrefab == null)
        {
            return null;
        }

        Vector2 arrivalDirection = bossArrivalDirection;

        if (arrivalDirection.sqrMagnitude <= 0.001f)
        {
            arrivalDirection = Vector2.up;
        }

        arrivalDirection.Normalize();

        Vector3 startPosition = bossBattlePosition + (Vector3)(arrivalDirection * Mathf.Max(0.1f, bossArrivalDistance));
        GameObject bossObject = Instantiate(bossPrefab, startPosition, Quaternion.identity);
        bossObject.name = bossPrefab.name;

        Rigidbody2D bossRb = bossObject.GetComponent<Rigidbody2D>();

        if (bossRb != null)
        {
            bossRb.linearVelocity = Vector2.zero;
            bossRb.angularVelocity = 0f;
        }

        return bossObject;
    }

    private IEnumerator MoveBossArrivalRoutine(
        GameObject bossObject,
        Vector3 bossBattlePosition,
        GameObject interactor)
    {
        if (bossObject == null)
        {
            yield break;
        }

        Transform bossTransform = bossObject.transform;
        Vector3 startPosition = bossTransform.position;
        Vector3 endPosition = bossBattlePosition;

        float duration = Mathf.Max(0.01f, bossArrivalDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = bossMoveCurve != null ? bossMoveCurve.Evaluate(t) : t;

            bossTransform.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);

            if (faceBossToPlayerWhenArrived && interactor != null)
            {
                FaceTransformToTarget(bossTransform, interactor.transform.position, bossRotationOffset);
            }

            yield return null;
        }

        bossTransform.position = endPosition;

        if (faceBossToPlayerWhenArrived && interactor != null)
        {
            FaceTransformToTarget(bossTransform, interactor.transform.position, bossRotationOffset);
        }

        Rigidbody2D bossRb = bossObject.GetComponent<Rigidbody2D>();

        if (bossRb != null)
        {
            bossRb.linearVelocity = Vector2.zero;
            bossRb.angularVelocity = 0f;
        }
    }

    private void DisableBossForIntro(GameObject bossObject)
    {
        disabledBossComponents.Clear();

        if (bossObject == null)
        {
            return;
        }

        MonoBehaviour[] components = bossObject.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];

            if (component == null || !component.enabled)
            {
                continue;
            }

            if (!ShouldDisableBossComponentForIntro(component))
            {
                continue;
            }

            component.enabled = false;
            disabledBossComponents.Add(component);
        }

        Rigidbody2D rb = bossObject.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private bool ShouldDisableBossComponentForIntro(MonoBehaviour component)
    {
        string typeName = component.GetType().Name;

        return typeName == "EnemyBaseAI" ||
               typeName == "EnemyAttackController" ||
               typeName == "BossPatternController";
    }

    private void EnableBossForBattle()
    {
        for (int i = 0; i < disabledBossComponents.Count; i++)
        {
            MonoBehaviour component = disabledBossComponents[i];

            if (component != null)
            {
                component.enabled = true;
            }
        }

        disabledBossComponents.Clear();

        if (spawnedBoss != null)
        {
            Rigidbody2D rb = spawnedBoss.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void TrackBossDeathForCleanup(GameObject bossObject)
    {
        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        if (!cleanupArenaObjectsOnBossDeath || bossObject == null)
        {
            return;
        }

        trackedBossHealth = bossObject.GetComponent<EnemyHealth>();

        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died += HandleTrackedBossDied;
        }
    }

    private void HandleTrackedBossDied(EnemyHealth health)
    {
        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        DestroySpawnedWalls();
        DestroySpawnedManagerShips();
    }

    private void DestroySpawnedWalls()
    {
        for (int i = spawnedWallObjects.Count - 1; i >= 0; i--)
        {
            GameObject wallObject = spawnedWallObjects[i];

            if (wallObject == null)
            {
                continue;
            }

            BossArenaLaserWall wall = wallObject.GetComponent<BossArenaLaserWall>();

            if (wall != null)
            {
                wall.Deactivate();
            }
            else
            {
                Destroy(wallObject);
            }
        }

        spawnedWallObjects.Clear();
    }

    private void DestroySpawnedManagerShips()
    {
        for (int i = spawnedManagerShips.Count - 1; i >= 0; i--)
        {
            LaserGuardianDrone managerShip = spawnedManagerShips[i];

            if (managerShip != null)
            {
                Destroy(managerShip.gameObject);
            }
        }

        spawnedManagerShips.Clear();
    }

    private void ShowWarning()
    {
        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(activationWarningMessage, warningDuration);
            return;
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowWarning(activationWarningMessage);
        }
    }

    private IEnumerator PlayCoreActivationNoticeRoutine()
    {
        bool titlePlayed = false;

        EventTitleDirector director = coreActivationTitleDirector != null
            ? coreActivationTitleDirector
            : EventTitleDirector.Instance;

        if (useMotionTitleForCoreActivation && director != null)
        {
            director.Show(
                coreActivationTitleType,
                coreActivationTitle,
                coreActivationSubtitle
            );

            titlePlayed = true;
        }
        else if (fallbackToWarningMessageIfTitleMissing)
        {
            ShowWarning();
        }

        float waitTime = titlePlayed
            ? coreActivationTitleWait
            : delayAfterWarning;

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SetCameraWide()
    {
        if (cameraZoomController == null)
        {
            ResolveReferences();
        }

        if (cameraZoomController != null)
        {
            cameraZoomController.SetZoomMultiplier(wideZoomMultiplier, true);
        }
        else
        {
            Debug.LogWarning(
                "CameraZoomController2D를 찾지 못했습니다. CinemachineCamera 또는 Main Camera에 CameraZoomController2D를 붙이세요.",
                this
            );
        }
    }

    private void ResetCameraZoom()
    {
        if (cameraZoomController != null)
        {
            cameraZoomController.ResetZoom(true);
        }
    }

    private void LockPlayer(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        playerLockState = new PlayerLockState
        {
            hasState = true
        };

        playerLockState.rb = interactor.GetComponent<Rigidbody2D>();

        if (playerLockState.rb != null)
        {
            playerLockState.rb.linearVelocity = Vector2.zero;
            playerLockState.rb.angularVelocity = 0f;
        }

        playerLockState.controller = interactor.GetComponent<PlayerController2D>();

        if (playerLockState.controller != null)
        {
            playerLockState.controllerControlWasEnabled = playerLockState.controller.ControlEnabled;
            playerLockState.controllerMovementWasLocked = playerLockState.controller.MovementLocked;

            playerLockState.controller.SetControlEnabled(false);
            playerLockState.controller.SetMovementLocked(true);
        }

        playerLockState.dash = interactor.GetComponent<PlayerDash>();

        if (playerLockState.dash != null)
        {
            playerLockState.dashWasEnabled = playerLockState.dash.enabled;
            playerLockState.dash.enabled = false;
        }

        playerLockState.weaponController = interactor.GetComponent<PlayerWeaponController>();

        if (playerLockState.weaponController != null)
        {
            playerLockState.weaponWasEnabled = playerLockState.weaponController.enabled;
            playerLockState.weaponTreeBeforeLock = playerLockState.weaponController.CurrentWeaponTree;
            playerLockState.weaponController.enabled = false;
        }

        playerLockState.interactor = interactor.GetComponent<PlayerInteractor>();

        if (playerLockState.interactor != null)
        {
            playerLockState.interactorWasEnabled = playerLockState.interactor.enabled;
            playerLockState.interactor.enabled = false;
        }

        playerLockState.radarScanner = interactor.GetComponent("PlayerRadarScanner") as MonoBehaviour;

        if (playerLockState.radarScanner != null)
        {
            playerLockState.radarWasEnabled = playerLockState.radarScanner.enabled;
            playerLockState.radarScanner.enabled = false;
        }

        playerLockState.emergencyReturnController = interactor.GetComponent<EmergencyReturnController>();

        if (playerLockState.emergencyReturnController != null)
        {
            playerLockState.emergencyReturnWasEnabled = playerLockState.emergencyReturnController.enabled;
            playerLockState.emergencyReturnController.enabled = false;
        }

        extraDisabledPlayerComponents.Clear();

        if (extraPlayerComponentsToDisable == null)
        {
            return;
        }

        for (int i = 0; i < extraPlayerComponentsToDisable.Length; i++)
        {
            MonoBehaviour component = extraPlayerComponentsToDisable[i];

            if (component == null || component == this)
            {
                continue;
            }

            extraDisabledPlayerComponents.Add(new ComponentEnabledState
            {
                component = component,
                wasEnabled = component.enabled
            });

            component.enabled = false;
        }
    }

    private void RestorePlayer()
    {
        if (!playerLockState.hasState)
        {
            return;
        }

        if (playerLockState.rb != null)
        {
            playerLockState.rb.linearVelocity = Vector2.zero;
            playerLockState.rb.angularVelocity = 0f;
        }

        if (playerLockState.controller != null)
        {
            playerLockState.controller.SetControlEnabled(playerLockState.controllerControlWasEnabled);
            playerLockState.controller.SetMovementLocked(playerLockState.controllerMovementWasLocked);
        }

        if (playerLockState.dash != null)
        {
            playerLockState.dash.enabled = playerLockState.dashWasEnabled;
        }

        if (playerLockState.weaponController != null)
        {
            playerLockState.weaponController.enabled = playerLockState.weaponWasEnabled;

            if (playerLockState.weaponWasEnabled)
            {
                playerLockState.weaponController.EquipWeapon(playerLockState.weaponTreeBeforeLock);
            }
        }

        if (playerLockState.interactor != null)
        {
            playerLockState.interactor.enabled = playerLockState.interactorWasEnabled;
        }

        if (playerLockState.radarScanner != null)
        {
            playerLockState.radarScanner.enabled = playerLockState.radarWasEnabled;
        }

        if (playerLockState.emergencyReturnController != null)
        {
            playerLockState.emergencyReturnController.enabled = playerLockState.emergencyReturnWasEnabled;
        }

        for (int i = 0; i < extraDisabledPlayerComponents.Count; i++)
        {
            ComponentEnabledState state = extraDisabledPlayerComponents[i];

            if (state.component != null)
            {
                state.component.enabled = state.wasEnabled;
            }
        }

        extraDisabledPlayerComponents.Clear();
        playerLockState = new PlayerLockState();
    }

    private void ApplyIntroInvincibility(GameObject interactor)
    {
        if (!makePlayerInvincibleDuringIntro || interactor == null)
        {
            return;
        }

        PlayerHealth playerHealth = interactor.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        float duration =
            warningDuration +
            delayAfterWarning +
            coreActivationTitleWait +
            zoomOutWait +
            managerShipMoveDuration +
            bossArrivalDuration +
            delayBeforeWallActivation +
            delayAfterWallActivation +
            zoomInWait +
            playerInvincibleExtraTime;

        playerHealth.AddInvincibleTime(duration);
    }

    private void FaceTransformToTarget(Transform target, Vector3 targetPosition, float rotationOffset)
    {
        if (target == null)
        {
            return;
        }

        Vector2 direction = targetPosition - target.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        target.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    private IEnumerator Wait(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + (Vector3)arenaCenterOffset;
        Vector2 half = GetEffectiveHalfExtents();

        Vector3 bottomLeft = center + new Vector3(-half.x, -half.y, 0f);
        Vector3 topLeft = center + new Vector3(-half.x, half.y, 0f);
        Vector3 topRight = center + new Vector3(half.x, half.y, 0f);
        Vector3 bottomRight = center + new Vector3(half.x, -half.y, 0f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(bottomLeft, 0.35f);
        Gizmos.DrawWireSphere(topLeft, 0.35f);
        Gizmos.DrawWireSphere(topRight, 0.35f);
        Gizmos.DrawWireSphere(bottomRight, 0.35f);
    }
}