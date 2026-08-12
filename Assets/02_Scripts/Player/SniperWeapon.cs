using UnityEngine;

public class SniperWeapon : PlayerWeaponBase
{
    private static Material sharedRuntimeChargeLineMaterial;

    [Header("Sniper Fallback Values")]
    [SerializeField] private float fallbackMinDamage = 4f;
    [SerializeField] private float fallbackMaxDamage = 10f;
    [SerializeField] private float fallbackSpeed = 22f;
    [SerializeField] private float fallbackRange = 16f;
    [SerializeField] private int fallbackPierceCount = 3;

    [Header("Charge")]
    [SerializeField] private float fallbackMaxChargeTime = 1.2f;
    [Tooltip("이 시간보다 짧게 눌렀다 떼면 발사하지 않고 조용히 취소합니다.")]
    [Min(0f)]
    [SerializeField] private float minimumChargeTime = 0.15f;
    [Tooltip("짧은 클릭마다 시작음이 반복되지 않도록 차징음 재생을 지연합니다.")]
    [Min(0f)]
    [SerializeField] private float chargeAudioStartDelay = 0.08f;
    [Tooltip("빠른 연속 클릭으로 차징 시작/취소가 반복되는 것을 막는 입력 잠금 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float rapidClickLockout = 0.22f;
    [SerializeField] private bool suppressCancelSoundBeforeMinimumCharge = true;
    [SerializeField] private float nextChargeDelay = 0.25f;
    [SerializeField] private bool cancelChargeOnMove = true;
    [Tooltip("켜면 이동 중에도 차징을 유지합니다. 기존 cancelChargeOnMove보다 우선합니다.")]
    [SerializeField] private bool allowChargeWhileMoving = true;
    [Tooltip("이동 중 차징 속도 배율입니다. 0.6이면 차징 시간이 약 1 / 0.6 = 1.67배 길어집니다.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float movingChargeSpeedMultiplier = 0.6f;

    [Header("Stationary Snipe Camera Assist")]
    [Tooltip("멈춘 상태에서 차징할 때 마우스 방향 카메라 오프셋을 얼마나 더 멀리 보낼지 결정합니다.")]
    [Range(1f, 3f)]
    [SerializeField] private float stationaryAimOffsetMultiplier = 1.65f;
    [Tooltip("멈춘 상태에서 차징할 때 마우스 원거리 감도를 얼마나 늘릴지 결정합니다.")]
    [Range(1f, 3f)]
    [SerializeField] private float stationaryMouseDistanceMultiplier = 1.5f;
    [Tooltip("차징량에 따라 조준 카메라 보너스를 곱하는 커브입니다.")]
    [SerializeField] private AnimationCurve stationaryAimAssistByCharge = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera Zoom")]
    [SerializeField] private CameraZoomController2D cameraZoomController;
    [SerializeField] private bool autoResolveCameraZoomController = true;
    [SerializeField] private float fallbackChargeZoomBonus = 0.2f;
    [SerializeField] private GungeonStyleCamera2D gungeonStyleCamera;
    [SerializeField] private bool autoResolveGungeonStyleCamera = true;

    [Header("Charge Audio")]
    [SerializeField] private string chargeLoopChannel = "sniper_charge";
    [Tooltip("sniper_charge_loop 이벤트가 없으면 시작음을 임시 루프로 사용합니다.")]
    [SerializeField] private bool fallbackToStartSoundAsLoop = true;
    [SerializeField] private float chargeLoopStartPitchMultiplier = 0.78f;
    [SerializeField] private float chargeLoopEndPitchMultiplier = 1.34f;
    [SerializeField] private float chargeLoopStartVolumeMultiplier = 0.55f;
    [SerializeField] private float chargeLoopEndVolumeMultiplier = 1f;
    [SerializeField] private AnimationCurve chargeAudioCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Charge Aim Line")]
    [SerializeField] private bool showChargeAimLine = true;
    [SerializeField] private bool createRuntimeChargeLineIfMissing = true;
    [SerializeField] private LineRenderer chargeAimLine;
    [SerializeField] private Material chargeLineMaterial;
    [SerializeField] private Color chargeLineStartColor = new Color(0.2f, 0.85f, 1f, 0.25f);
    [SerializeField] private Color chargeLineReadyColor = new Color(1f, 0.9f, 0.2f, 0.95f);
    [SerializeField] private float chargeLineMinWidth = 0.025f;
    [SerializeField] private float chargeLineMaxWidth = 0.085f;
    [SerializeField] private string chargeLineSortingLayerName = "Default";
    [SerializeField] private int chargeLineSortingOrder = 70;

    [Header("Debug")]
    [SerializeField] private bool logProjectileFailure = true;

    private bool isCharging;
    private bool chargeAudioStarted;
    private float chargeTimer;
    private float nextChargeAllowedTime;

    public override bool IsCharging => isCharging;

    public override float ChargeRatio
    {
        get
        {
            float maxChargeTime = GetMaxChargeTime();

            if (maxChargeTime <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(chargeTimer / maxChargeTime);
        }
    }

    private void Awake()
    {
        ResolvePresentationReferences();
        SetChargeLineVisible(false);
    }

    private void OnDisable()
    {
        if (isCharging)
        {
            CancelCharge();
        }
        else
        {
            StopChargeLoop();
            SetChargeLineVisible(false);

            if (gungeonStyleCamera != null)
            {
                gungeonStyleCamera.ResetAimOffsetAssist();
            }
        }
    }

    public override void SetRuntimeReferences(
        PlayerWeaponController owner,
        Transform firePointReference,
        PlayerController2D controller,
        PlayerCombatState state,
        PlayerWeaponModifiers modifiers)
    {
        base.SetRuntimeReferences(owner, firePointReference, controller, state, modifiers);
        ResolvePresentationReferences();
    }

    public override void OnEquip()
    {
        ResolvePresentationReferences();
        ResetChargeState();
    }

    public override void OnUnequip()
    {
        CancelCharge();
    }

    public override void ForceCancel()
    {
        CancelCharge();
    }

    public override void TickWeapon(WeaponFireInput input, float deltaTime)
    {
        if (input.PressedThisFrame)
        {
            TryBeginCharge();
        }

        if (!isCharging)
        {
            return;
        }

        bool isMoving = playerController != null && playerController.IsMoving;

        if (!allowChargeWhileMoving && cancelChargeOnMove && isMoving)
        {
            CancelCharge();
            return;
        }

        float effectiveDeltaTime = Mathf.Max(0f, deltaTime);

        if (isMoving)
        {
            effectiveDeltaTime *= Mathf.Clamp(movingChargeSpeedMultiplier, 0.1f, 1f);
        }

        chargeTimer += effectiveDeltaTime;

        if (!chargeAudioStarted && chargeTimer >= Mathf.Max(0f, chargeAudioStartDelay))
        {
            StartChargeAudio();
        }

        UpdateCameraZoom();
        UpdateAimAssist();
        UpdateChargePresentation();
        NotifyChargeChanged(ChargeRatio);

        // ReleasedThisFrame가 UI 포인터 전환 등의 이유로 유실되어도
        // Held가 false가 되는 순간 반드시 발사 처리를 한다.
        if (input.ReleasedThisFrame || !input.Held)
        {
            FireChargedShot();
        }
    }

    private void TryBeginCharge()
    {
        if (Time.time < nextChargeAllowedTime || isCharging)
        {
            return;
        }

        ResolvePresentationReferences();

        isCharging = true;
        chargeTimer = 0f;

        UpdateCameraZoom();
        UpdateAimAssist();
        SetChargeLineVisible(showChargeAimLine);
        UpdateChargePresentation();

        chargeAudioStarted = false;

        if (chargeAudioStartDelay <= 0f)
        {
            StartChargeAudio();
        }

        NotifyChargeStarted();
        NotifyChargeChanged(ChargeRatio);
    }

    private void FireChargedShot()
    {
        if (!isCharging)
        {
            return;
        }

        if (chargeTimer < Mathf.Max(0f, minimumChargeTime))
        {
            bool playCancel = !suppressCancelSoundBeforeMinimumCharge && chargeAudioStarted;
            CancelCharge(!playCancel);
            nextChargeAllowedTime = Time.time + Mathf.Max(rapidClickLockout, nextChargeDelay);
            return;
        }

        float finalChargeRatio = ChargeRatio;
        Vector2 direction = GetAimDirection();

        float damage = Mathf.Lerp(
            GetMinChargeDamage(),
            GetMaxChargeDamage(),
            finalChargeRatio
        );

        float baseSpeed = GetProjectileSpeed(fallbackSpeed);
        float baseRange = GetProjectileRange(fallbackRange);
        int basePierce = GetProjectilePierceCount(fallbackPierceCount);

        if (weaponModifiers != null)
        {
            damage *= weaponModifiers.ChargeDamageMultiplier;
        }

        bool fired = SpawnProjectile(
            direction,
            damage,
            baseSpeed,
            baseRange,
            basePierce
        );

        StopChargeLoop();

        if (fired)
        {
            SpawnMuzzleEffect(direction);
            AudioManager.PlayAt(SoundEventIds.SniperFire, transform.position);

            RegisterAttack();
            NotifyFired(finalChargeRatio);
            NotifyChargeReleased(finalChargeRatio);
        }
        else
        {
            if (logProjectileFailure)
            {
                Debug.LogError(
                    $"{name}: 스나이퍼 탄환 생성에 실패했습니다. ProjectileDefinition, Projectile Prefab, Bullet 컴포넌트를 확인하세요.",
                    this
                );
            }

            AudioManager.PlayAt(SoundEventIds.SniperChargeCancel, transform.position, 0.7f);
            NotifyChargeCanceled();
        }

        ResetChargeState();
        nextChargeAllowedTime = Time.time + Mathf.Max(0f, nextChargeDelay);
    }

    private void CancelCharge(bool silent = false)
    {
        if (!isCharging)
        {
            StopChargeLoop();
            chargeAudioStarted = false;
            SetChargeLineVisible(false);
            return;
        }

        bool shouldPlayCancelSound = !silent && chargeAudioStarted;
        StopChargeLoop();

        if (shouldPlayCancelSound)
        {
            AudioManager.PlayAt(SoundEventIds.SniperChargeCancel, transform.position, 0.7f);
        }

        NotifyChargeCanceled();
        ResetChargeState();
    }

    private void ResetChargeState()
    {
        StopChargeLoop();
        chargeAudioStarted = false;
        isCharging = false;
        chargeTimer = 0f;
        SetChargeLineVisible(false);

        if (cameraZoomController != null)
        {
            cameraZoomController.ResetZoom();
        }

        if (gungeonStyleCamera != null)
        {
            gungeonStyleCamera.ResetAimOffsetAssist();
        }
    }

    private void UpdateChargePresentation()
    {
        float ratio = ChargeRatio;

        if (chargeAudioStarted)
        {
            UpdateChargeLoopModulation(ratio);
        }

        UpdateChargeAimLine(ratio);
    }

    private void StartChargeAudio()
    {
        if (chargeAudioStarted || !isCharging)
        {
            return;
        }

        chargeAudioStarted = true;

        bool loopStarted = AudioManager.PlayLoop(
            SoundEventIds.SniperChargeLoop,
            chargeLoopChannel,
            1f
        );

        bool usedStartSoundAsLoop = false;

        if (!loopStarted && fallbackToStartSoundAsLoop)
        {
            usedStartSoundAsLoop = AudioManager.PlayLoop(
                SoundEventIds.SniperChargeStart,
                chargeLoopChannel,
                0.75f
            );
        }

        if (!usedStartSoundAsLoop)
        {
            AudioManager.PlayAt(SoundEventIds.SniperChargeStart, transform.position, 0.8f);
        }

        UpdateChargeLoopModulation(ChargeRatio);
    }

    private void UpdateChargeLoopModulation(float ratio)
    {
        float normalized = Mathf.Clamp01(ratio);
        float eased = chargeAudioCurve != null && chargeAudioCurve.length > 0
            ? Mathf.Clamp01(chargeAudioCurve.Evaluate(normalized))
            : normalized;

        float pitchMultiplier = Mathf.Lerp(
            chargeLoopStartPitchMultiplier,
            chargeLoopEndPitchMultiplier,
            eased
        );
        float volumeMultiplier = Mathf.Lerp(
            chargeLoopStartVolumeMultiplier,
            chargeLoopEndVolumeMultiplier,
            eased
        );

        AudioManager.SetLoopModulation(
            chargeLoopChannel,
            pitchMultiplier,
            volumeMultiplier
        );
    }

    private void StopChargeLoop()
    {
        if (!string.IsNullOrWhiteSpace(chargeLoopChannel))
        {
            AudioManager.StopLoop(chargeLoopChannel);
        }
    }

    private void UpdateCameraZoom()
    {
        ResolveCameraZoomController();

        if (cameraZoomController == null)
        {
            return;
        }

        float zoomBonus = fallbackChargeZoomBonus;

        if (weaponDefinition != null)
        {
            zoomBonus = weaponDefinition.ChargeCameraZoomBonus;
        }

        float targetMultiplier = 1f + (zoomBonus * ChargeRatio);
        cameraZoomController.SetZoomMultiplier(targetMultiplier);
    }

    private void UpdateChargeAimLine(float ratio)
    {
        if (!showChargeAimLine)
        {
            SetChargeLineVisible(false);
            return;
        }

        EnsureChargeAimLine();

        if (chargeAimLine == null)
        {
            return;
        }

        Transform origin = firePoint != null ? firePoint : transform;
        Vector2 direction = GetAimDirection();
        float range = GetProjectileRange(fallbackRange);

        if (weaponModifiers != null)
        {
            range *= weaponModifiers.RangeMultiplier;
        }

        float normalized = Mathf.Clamp01(ratio);
        float readyPulse = normalized >= 0.999f
            ? 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.12f
            : 1f;

        Color color = Color.Lerp(chargeLineStartColor, chargeLineReadyColor, normalized);
        float width = Mathf.Lerp(chargeLineMinWidth, chargeLineMaxWidth, normalized) * readyPulse;

        chargeAimLine.positionCount = 2;
        chargeAimLine.SetPosition(0, origin.position);
        chargeAimLine.SetPosition(1, origin.position + (Vector3)(direction * Mathf.Max(0.1f, range)));
        chargeAimLine.startWidth = width;
        chargeAimLine.endWidth = width * 0.45f;
        chargeAimLine.startColor = color;

        Color endColor = color;
        endColor.a *= 0.35f;
        chargeAimLine.endColor = endColor;
    }

    private void SetChargeLineVisible(bool visible)
    {
        if (visible)
        {
            EnsureChargeAimLine();
        }

        if (chargeAimLine != null)
        {
            chargeAimLine.enabled = visible;
        }
    }

    private void EnsureChargeAimLine()
    {
        if (chargeAimLine != null || !createRuntimeChargeLineIfMissing)
        {
            return;
        }

        GameObject lineObject = new GameObject("Runtime_SniperChargeAimLine");
        lineObject.transform.SetParent(transform, false);

        chargeAimLine = lineObject.AddComponent<LineRenderer>();
        chargeAimLine.useWorldSpace = true;
        chargeAimLine.loop = false;
        chargeAimLine.positionCount = 2;
        chargeAimLine.numCapVertices = 2;
        chargeAimLine.numCornerVertices = 2;
        chargeAimLine.textureMode = LineTextureMode.Stretch;
        chargeAimLine.alignment = LineAlignment.TransformZ;
        chargeAimLine.sortingLayerName = string.IsNullOrWhiteSpace(chargeLineSortingLayerName)
            ? "Default"
            : chargeLineSortingLayerName;
        chargeAimLine.sortingOrder = chargeLineSortingOrder;
        chargeAimLine.sharedMaterial = chargeLineMaterial != null
            ? chargeLineMaterial
            : GetRuntimeChargeLineMaterial();
        chargeAimLine.enabled = false;
    }

    private void ResolvePresentationReferences()
    {
        ResolveCameraZoomController();
        ResolveGungeonStyleCamera();

        if (showChargeAimLine)
        {
            EnsureChargeAimLine();
        }
    }

    private void ResolveCameraZoomController()
    {
        if (cameraZoomController != null || !autoResolveCameraZoomController)
        {
            return;
        }

        GungeonStyleCamera2D preferredCamera = GungeonStyleCamera2D.Instance;

        if (preferredCamera != null)
        {
            cameraZoomController = preferredCamera.GetComponent<CameraZoomController2D>();
        }

        if (cameraZoomController == null)
        {
            cameraZoomController = FindFirstObjectByType<CameraZoomController2D>(FindObjectsInactive.Include);
        }
    }

    private void ResolveGungeonStyleCamera()
    {
        if (gungeonStyleCamera != null || !autoResolveGungeonStyleCamera)
        {
            return;
        }

        gungeonStyleCamera = GungeonStyleCamera2D.Instance;

        if (gungeonStyleCamera == null)
        {
            gungeonStyleCamera = FindFirstObjectByType<GungeonStyleCamera2D>(FindObjectsInactive.Include);
        }
    }

    private void UpdateAimAssist()
    {
        ResolveGungeonStyleCamera();

        if (gungeonStyleCamera == null)
        {
            return;
        }

        bool isMoving = playerController != null && playerController.IsMoving;

        if (!isCharging || isMoving)
        {
            gungeonStyleCamera.ResetAimOffsetAssist();
            return;
        }

        float ratio = ChargeRatio;
        float curveValue = stationaryAimAssistByCharge != null && stationaryAimAssistByCharge.length > 0
            ? Mathf.Clamp01(stationaryAimAssistByCharge.Evaluate(ratio))
            : ratio;

        float aimOffsetMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, stationaryAimOffsetMultiplier), curveValue);
        float mouseDistanceMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, stationaryMouseDistanceMultiplier), curveValue);

        gungeonStyleCamera.SetAimOffsetAssist(aimOffsetMultiplier, mouseDistanceMultiplier);
    }

    private static Material GetRuntimeChargeLineMaterial()
    {
        if (sharedRuntimeChargeLineMaterial != null)
        {
            return sharedRuntimeChargeLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        sharedRuntimeChargeLineMaterial = new Material(shader)
        {
            name = "Runtime_SniperChargeAimLine_Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        return sharedRuntimeChargeLineMaterial;
    }

    private float GetMaxChargeTime()
    {
        float chargeTime = weaponDefinition != null
            ? weaponDefinition.MaxChargeTime
            : fallbackMaxChargeTime;

        if (weaponModifiers != null)
        {
            chargeTime *= weaponModifiers.ChargeTimeMultiplier;
        }

        return Mathf.Max(0.05f, chargeTime);
    }

    private float GetMinChargeDamage()
    {
        return weaponDefinition != null
            ? weaponDefinition.MinChargeDamage
            : fallbackMinDamage;
    }

    private float GetMaxChargeDamage()
    {
        return weaponDefinition != null
            ? weaponDefinition.MaxChargeDamage
            : fallbackMaxDamage;
    }
}
