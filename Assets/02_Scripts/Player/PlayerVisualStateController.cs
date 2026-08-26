using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerVisualStateController : MonoBehaviour
{
    [Header("Existing Player References")]
    [SerializeField] private PlayerShipVisualController shipVisualController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SpriteRenderer baseSpriteRenderer;

    [Header("Base Sprite State")]
    [SerializeField] private Sprite normalMachineGunSprite;
    [SerializeField] private Sprite cursedBaseSprite;

    [Header("Layered Visuals")]
    [SerializeField] private SpriteRenderer weaponAccentRenderer;
    [SerializeField] private GameObject curseVisualRoot;
    [SerializeField] private SpriteRenderer curseEdgeRenderer;
    [SerializeField] private SpriteRenderer curseAfterimageRenderer;
    [SerializeField] private ParticleSystem curseFragmentParticles;

    [Header("Curse State")]
    [SerializeField] private TraitDefinition curseTraitDefinition;
    [SerializeField] private Color curseEdgeColor = new Color(0.65f, 0.2f, 1f, 0.38f);
    [SerializeField] private Color curseAfterimageColor = new Color(0.75f, 0.25f, 1f, 0.55f);

    [Header("Weapon Accent Colors")]
    [SerializeField] private Color machineGunColor = new Color(0.25f, 1f, 0.35f, 1f);
    [SerializeField] private Color shotgunColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color sniperColor = new Color(0.2f, 0.72f, 1f, 1f);

    [Header("Weapon Fire Feedback")]
    [Tooltip("Presentation-only root. When empty, the base sprite parent is used. Gameplay, collider, and fire-point roots are never moved.")]
    [SerializeField] private Transform recoilVisualRoot;
    [Min(0f)]
    [SerializeField] private float machineGunRecoilDistance = 0.018f;
    [Min(0f)]
    [SerializeField] private float shotgunRecoilDistance = 0.11f;
    [Min(0f)]
    [SerializeField] private float sniperRecoilDistance = 0.1f;
    [Min(0f)]
    [SerializeField] private float machineGunCameraAmplitude = 0.018f;
    [Min(0f)]
    [SerializeField] private float shotgunCameraAmplitude = 0.07f;
    [Min(0f)]
    [SerializeField] private float sniperCameraAmplitude = 0.06f;
    [Min(0f)]
    [SerializeField] private float machineGunCameraMinimumInterval = 0.18f;
    [Range(1f, 1.5f)]
    [SerializeField] private float overpressurePresentationMultiplier = 1.12f;

    [Header("Glitch Pulse")]
    [Min(0.01f)]
    [SerializeField] private float glitchOffset = 0.04f;
    [Min(0.01f)]
    [SerializeField] private float ambientGlitchDuration = 0.12f;
    [Min(0.1f)]
    [SerializeField] private float ambientGlitchIntervalMin = 2.5f;
    [Min(0.1f)]
    [SerializeField] private float ambientGlitchIntervalMax = 5f;

    private PermanentProgress subscribedProgress;
    private GameBootstrap subscribedBootstrap;
    private Tween ambientGlitchDelay;
    private Sequence glitchSequence;
    private Sequence recoilSequence;
    private ParticleSystem muzzlePulseParticles;
    private Material muzzlePulseMaterial;
    private Vector3 afterimageRestLocalPosition;
    private Vector3 recoilRestLocalPosition;
    private Quaternion recoilRestLocalRotation;
    private float nextMachineGunCameraTime;
    private bool recoilRestStateCached;
    private Sprite currentShipVisualSprite;
    private bool isCursed;
    private WeaponTreeType currentWeaponTree = WeaponTreeType.MachineGun;

    public bool IsCursed => isCursed;
    public WeaponTreeType CurrentWeaponTree => currentWeaponTree;
    public Color CurrentWeaponAccentColor => ResolveWeaponAccentColor(currentWeaponTree);

    public event Action<bool> CurseStateChanged;
    public event Action<WeaponTreeType, Color> WeaponAccentChanged;

    private void Awake()
    {
        CacheReferences();

        if (curseAfterimageRenderer != null)
        {
            afterimageRestLocalPosition = curseAfterimageRenderer.transform.localPosition;
            curseAfterimageRenderer.enabled = false;
        }

        CacheRecoilRoot();
    }

    private void OnEnable()
    {
        CacheReferences();
        Subscribe();
        RefreshVisualState();
    }

    private void Start()
    {
        SubscribeToBootstrap();
        SubscribeToProgress();
        RefreshVisualState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopGlitchTweens();
        ResetFireFeedback();
        SetCurseVisualsActive(false);
    }

    private void OnDestroy()
    {
        if (muzzlePulseMaterial != null)
        {
            Destroy(muzzlePulseMaterial);
            muzzlePulseMaterial = null;
        }
    }

    public void RefreshVisualState()
    {
        RefreshWeaponAccent();
        RefreshCurseState();
        ApplyBaseSpriteState();
        SynchronizeLayerSprites();
    }

    public void PlayHitGlitch()
    {
        if (!isCursed || !isActiveAndEnabled)
        {
            return;
        }

        PlayGlitchPulse(ambientGlitchDuration * 1.5f, glitchOffset * 1.6f, false);
    }

    public void PlayCurseAcquiredGlitch()
    {
        if (!isCursed || !isActiveAndEnabled)
        {
            return;
        }

        PlayGlitchPulse(ambientGlitchDuration * 3f, glitchOffset * 2.5f, true);
    }

    public void PlayWeaponFireFeedback(
        WeaponTreeType weaponTreeType,
        Vector2 muzzleWorldPosition,
        Vector2 shotDirection,
        float powerRatio = 1f,
        float aimChokeStrength = 0f,
        bool amplifyPresentation = false)
    {
        if (!isActiveAndEnabled || shotDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        CacheRecoilRoot();

        Vector2 direction = shotDirection.normalized;
        float power = Mathf.Clamp01(powerRatio);
        float choke = Mathf.Clamp01(aimChokeStrength);
        float amplification = amplifyPresentation
            ? Mathf.Max(1f, overpressurePresentationMultiplier)
            : 1f;

        ResolveFireFeedbackProfile(
            weaponTreeType,
            power,
            out float recoilDistance,
            out float rotationDegrees,
            out float kickDuration,
            out float returnDuration,
            out float cameraAmplitude,
            out float cameraDuration,
            out float muzzleSize
        );

        recoilDistance *= amplification;
        rotationDegrees *= amplification;
        cameraAmplitude *= amplification;
        muzzleSize *= amplification * Mathf.Lerp(1f, 1.12f, choke);

        PlayVisualRecoil(direction, recoilDistance, rotationDegrees, kickDuration, returnDuration);
        PlayMuzzlePulse(muzzleWorldPosition, direction, ResolveWeaponAccentColor(weaponTreeType), muzzleSize);
        PlayCameraResponse(weaponTreeType, cameraAmplitude, cameraDuration);
    }

    public void PlayAuxiliaryLaunchPulse(
        Vector2 worldPosition,
        Vector2 direction,
        Color color,
        float size)
    {
        if (!isActiveAndEnabled || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        PlayMuzzlePulse(
            worldPosition,
            direction.normalized,
            color,
            Mathf.Max(0.02f, size)
        );
    }

    private void CacheReferences()
    {
        if (shipVisualController == null)
        {
            shipVisualController = GetComponent<PlayerShipVisualController>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (baseSpriteRenderer == null && shipVisualController != null)
        {
            baseSpriteRenderer = shipVisualController.TargetSpriteRenderer;
        }

        CacheRecoilRoot();
    }

    private void Subscribe()
    {
        SubscribeToBootstrap();

        if (shipVisualController != null)
        {
            shipVisualController.VisualChanged -= HandleShipVisualChanged;
            shipVisualController.VisualChanged += HandleShipVisualChanged;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
            weaponController.WeaponEquipped += HandleWeaponEquipped;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
            playerHealth.Damaged += HandlePlayerDamaged;
            playerHealth.Died -= HandlePlayerDied;
            playerHealth.Died += HandlePlayerDied;
        }

        SubscribeToProgress();
    }

    private void SubscribeToProgress()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        if (subscribedProgress == progress)
        {
            return;
        }

        if (subscribedProgress != null)
        {
            subscribedProgress.Changed -= HandleProgressChanged;
        }

        subscribedProgress = progress;

        if (subscribedProgress != null)
        {
            subscribedProgress.Changed += HandleProgressChanged;
        }
    }

    private void Unsubscribe()
    {
        if (shipVisualController != null)
        {
            shipVisualController.VisualChanged -= HandleShipVisualChanged;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }

        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
            playerHealth.Died -= HandlePlayerDied;
        }

        if (subscribedProgress != null)
        {
            subscribedProgress.Changed -= HandleProgressChanged;
            subscribedProgress = null;
        }

        if (subscribedBootstrap != null)
        {
            subscribedBootstrap.ProgressLoaded -= HandleProgressLoaded;
            subscribedBootstrap = null;
        }

        shipVisualController?.SetFinalBaseSpriteOverride(null, true);
    }

    private void HandleShipVisualChanged(WeaponTreeType weaponTreeType, Sprite sprite)
    {
        currentWeaponTree = weaponTreeType;
        currentShipVisualSprite = sprite;
        ApplyWeaponAccent();
        ApplyBaseSpriteState();
        SynchronizeLayerSprites();
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTreeType, PlayerWeaponBase weapon)
    {
        ResetFireFeedback();
        currentWeaponTree = weaponTreeType;
        currentShipVisualSprite = shipVisualController != null
            ? shipVisualController.GetSprite(currentWeaponTree)
            : currentShipVisualSprite;
        ApplyWeaponAccent();
        ApplyBaseSpriteState();
        SynchronizeLayerSprites();
    }

    private void HandleProgressChanged()
    {
        RefreshCurseState();
        ApplyBaseSpriteState();
        SynchronizeLayerSprites();
    }

    private void HandleProgressLoaded()
    {
        SubscribeToProgress();
        RefreshVisualState();
    }

    private void HandlePlayerDamaged(float currentHp, float maxHp)
    {
        PlayHitGlitch();
    }

    private void HandlePlayerDied()
    {
        ResetFireFeedback();
    }

    private void RefreshWeaponAccent()
    {
        if (weaponController != null && weaponController.CurrentWeapon != null)
        {
            currentWeaponTree = weaponController.CurrentWeaponTree;
        }
        else if (shipVisualController != null)
        {
            currentWeaponTree = shipVisualController.CurrentWeaponTree;
        }

        if (shipVisualController != null)
        {
            Sprite shipSprite = shipVisualController.GetSprite(currentWeaponTree);

            if (shipSprite != null)
            {
                currentShipVisualSprite = shipSprite;
            }
        }

        ApplyWeaponAccent();
    }

    private void ApplyWeaponAccent()
    {
        Color color = ResolveWeaponAccentColor(currentWeaponTree);

        if (weaponAccentRenderer != null)
        {
            weaponAccentRenderer.color = color;
        }

        WeaponAccentChanged?.Invoke(currentWeaponTree, color);
    }

    private Color ResolveWeaponAccentColor(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => shotgunColor,
            WeaponTreeType.Sniper => sniperColor,
            _ => machineGunColor
        };
    }

    private void RefreshCurseState()
    {
        SubscribeToProgress();

        bool nextState = subscribedProgress != null &&
                         curseTraitDefinition != null &&
                         curseTraitDefinition.IsPersistentStoryTrait &&
                         curseTraitDefinition.IsNegativeStatus &&
                         subscribedProgress.HasPersistentStoryTrait(curseTraitDefinition);

        if (isCursed == nextState)
        {
            SetCurseVisualsActive(nextState);
            return;
        }

        isCursed = nextState;
        ResetFireFeedback();
        SetCurseVisualsActive(isCursed);
        CurseStateChanged?.Invoke(isCursed);
    }

    private void SetCurseVisualsActive(bool active)
    {
        if (curseVisualRoot != null)
        {
            curseVisualRoot.SetActive(active);
        }

        if (curseEdgeRenderer != null)
        {
            curseEdgeRenderer.enabled = active;
            curseEdgeRenderer.color = curseEdgeColor;
        }

        if (curseAfterimageRenderer != null)
        {
            curseAfterimageRenderer.enabled = false;
            curseAfterimageRenderer.transform.localPosition = afterimageRestLocalPosition;
            curseAfterimageRenderer.color = curseAfterimageColor;
        }

        if (curseFragmentParticles != null)
        {
            if (active)
            {
                curseFragmentParticles.Play();
            }
            else
            {
                curseFragmentParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (active && isActiveAndEnabled)
        {
            ScheduleAmbientGlitch();
        }
        else
        {
            StopGlitchTweens();
        }
    }

    private void ApplyBaseSpriteState()
    {
        if (baseSpriteRenderer == null)
        {
            return;
        }

        Sprite targetSprite = isCursed && cursedBaseSprite != null
            ? cursedBaseSprite
            : ResolveUncursedBaseSprite();

        shipVisualController?.SetFinalBaseSpriteOverride(
            isCursed ? cursedBaseSprite : null,
            false
        );

        if (targetSprite != null && baseSpriteRenderer.sprite != targetSprite)
        {
            baseSpriteRenderer.sprite = targetSprite;
        }
    }

    private void SubscribeToBootstrap()
    {
        GameBootstrap bootstrap = GameBootstrap.Instance;

        if (subscribedBootstrap == bootstrap)
        {
            return;
        }

        if (subscribedBootstrap != null)
        {
            subscribedBootstrap.ProgressLoaded -= HandleProgressLoaded;
        }

        subscribedBootstrap = bootstrap;

        if (subscribedBootstrap != null)
        {
            subscribedBootstrap.ProgressLoaded += HandleProgressLoaded;
        }
    }

    private Sprite ResolveUncursedBaseSprite()
    {
        if (currentWeaponTree == WeaponTreeType.MachineGun && normalMachineGunSprite != null)
        {
            return normalMachineGunSprite;
        }

        if (currentShipVisualSprite != null)
        {
            return currentShipVisualSprite;
        }

        if (shipVisualController != null)
        {
            Sprite shipSprite = shipVisualController.GetSprite(currentWeaponTree);

            if (shipSprite != null)
            {
                currentShipVisualSprite = shipSprite;
                return shipSprite;
            }
        }

        return baseSpriteRenderer != null ? baseSpriteRenderer.sprite : null;
    }

    private void SynchronizeLayerSprites()
    {
        Sprite activeSilhouette = isCursed && cursedBaseSprite != null
            ? cursedBaseSprite
            : baseSpriteRenderer != null
                ? baseSpriteRenderer.sprite
                : ResolveUncursedBaseSprite();

        // The cursed player remains a compact pixel body. Weapon identity is
        // expressed by tint/projectiles/VFX instead of restoring a full ship.
        Sprite weaponSprite = isCursed && cursedBaseSprite != null
            ? cursedBaseSprite
            : ResolveUncursedBaseSprite();

        if (weaponAccentRenderer != null && weaponSprite != null)
        {
            weaponAccentRenderer.sprite = weaponSprite;
        }

        if (curseEdgeRenderer != null && activeSilhouette != null)
        {
            curseEdgeRenderer.sprite = activeSilhouette;
        }

        if (curseAfterimageRenderer != null && activeSilhouette != null)
        {
            curseAfterimageRenderer.sprite = activeSilhouette;
        }
    }

    private void ScheduleAmbientGlitch()
    {
        ambientGlitchDelay?.Kill();
        ambientGlitchDelay = null;

        if (!isCursed || !isActiveAndEnabled)
        {
            return;
        }

        float minimum = Mathf.Min(ambientGlitchIntervalMin, ambientGlitchIntervalMax);
        float maximum = Mathf.Max(ambientGlitchIntervalMin, ambientGlitchIntervalMax);
        float delay = UnityEngine.Random.Range(minimum, maximum);

        ambientGlitchDelay = DOVirtual.DelayedCall(delay, PlayAmbientGlitch, false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void PlayAmbientGlitch()
    {
        PlayGlitchPulse(ambientGlitchDuration, glitchOffset, false);
    }

    private void PlayGlitchPulse(float duration, float offsetMagnitude, bool useIndependentUpdate)
    {
        ambientGlitchDelay?.Kill();
        ambientGlitchDelay = null;
        glitchSequence?.Kill();
        glitchSequence = null;

        if (curseAfterimageRenderer == null || curseEdgeRenderer == null)
        {
            ScheduleAmbientGlitch();
            return;
        }

        SynchronizeLayerSprites();

        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0.01f, offsetMagnitude);
        float safeDuration = Mathf.Max(0.03f, duration);

        curseAfterimageRenderer.transform.localPosition = afterimageRestLocalPosition + offset;
        curseAfterimageRenderer.color = curseAfterimageColor;
        curseAfterimageRenderer.enabled = true;

        Color edgePulseColor = curseEdgeColor;
        edgePulseColor.a = Mathf.Clamp01(curseEdgeColor.a * 1.6f);

        glitchSequence = DOTween.Sequence()
            .SetUpdate(useIndependentUpdate)
            .Append(curseAfterimageRenderer.transform.DOLocalMove(afterimageRestLocalPosition, safeDuration).SetEase(Ease.OutQuad))
            .Join(curseAfterimageRenderer.DOFade(0f, safeDuration))
            .Join(curseEdgeRenderer.DOColor(edgePulseColor, safeDuration * 0.5f).SetLoops(2, LoopType.Yoyo))
            .OnComplete(HandleGlitchComplete)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void HandleGlitchComplete()
    {
        glitchSequence = null;

        if (curseAfterimageRenderer != null)
        {
            curseAfterimageRenderer.enabled = false;
            curseAfterimageRenderer.transform.localPosition = afterimageRestLocalPosition;
            curseAfterimageRenderer.color = curseAfterimageColor;
        }

        if (curseEdgeRenderer != null)
        {
            curseEdgeRenderer.color = curseEdgeColor;
        }

        ScheduleAmbientGlitch();
    }

    private void StopGlitchTweens()
    {
        ambientGlitchDelay?.Kill();
        ambientGlitchDelay = null;
        glitchSequence?.Kill();
        glitchSequence = null;
    }

    private void CacheRecoilRoot()
    {
        if (recoilVisualRoot == null && baseSpriteRenderer != null)
        {
            Transform candidate = baseSpriteRenderer.transform.parent;
            if (candidate != transform &&
                candidate != null &&
                candidate.GetComponent<Rigidbody2D>() == null &&
                candidate.GetComponent<Collider2D>() == null)
            {
                recoilVisualRoot = candidate;
            }
        }

        if (recoilVisualRoot == null ||
            recoilVisualRoot == transform ||
            recoilVisualRoot.GetComponent<Rigidbody2D>() != null ||
            recoilVisualRoot.GetComponent<Collider2D>() != null ||
            recoilRestStateCached)
        {
            return;
        }

        recoilRestLocalPosition = recoilVisualRoot.localPosition;
        recoilRestLocalRotation = recoilVisualRoot.localRotation;
        recoilRestStateCached = true;
    }

    private void PlayVisualRecoil(
        Vector2 worldDirection,
        float recoilDistance,
        float rotationDegrees,
        float kickDuration,
        float returnDuration)
    {
        if (recoilVisualRoot == null || !recoilRestStateCached || recoilDistance <= 0f)
        {
            return;
        }

        recoilSequence?.Kill();
        recoilVisualRoot.localPosition = recoilRestLocalPosition;
        recoilVisualRoot.localRotation = recoilRestLocalRotation;

        Vector3 localDirection = transform.InverseTransformDirection(-worldDirection);
        localDirection.z = 0f;
        localDirection = localDirection.sqrMagnitude > 0.0001f
            ? localDirection.normalized
            : Vector3.down;
        Vector3 kickPosition = recoilRestLocalPosition + localDirection * recoilDistance;
        float signedRotation = rotationDegrees * (worldDirection.x >= 0f ? -1f : 1f);
        Quaternion kickRotation = recoilRestLocalRotation * Quaternion.Euler(0f, 0f, signedRotation);

        recoilSequence = DOTween.Sequence()
            .Append(recoilVisualRoot.DOLocalMove(kickPosition, Mathf.Max(0.01f, kickDuration)).SetEase(Ease.OutQuad))
            .Join(recoilVisualRoot.DOLocalRotateQuaternion(kickRotation, Mathf.Max(0.01f, kickDuration)).SetEase(Ease.OutQuad))
            .Append(recoilVisualRoot.DOLocalMove(recoilRestLocalPosition, Mathf.Max(0.01f, returnDuration)).SetEase(Ease.OutCubic))
            .Join(recoilVisualRoot.DOLocalRotateQuaternion(recoilRestLocalRotation, Mathf.Max(0.01f, returnDuration)).SetEase(Ease.OutCubic))
            .OnComplete(() => recoilSequence = null)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void PlayCameraResponse(WeaponTreeType weaponTreeType, float amplitude, float duration)
    {
        if (weaponTreeType == WeaponTreeType.MachineGun)
        {
            if (Time.time < nextMachineGunCameraTime)
            {
                return;
            }

            nextMachineGunCameraTime = Time.time + Mathf.Max(0.01f, machineGunCameraMinimumInterval);
        }

        GungeonStyleCamera2D.RequestShake(amplitude, duration);
    }

    private void PlayMuzzlePulse(Vector2 worldPosition, Vector2 direction, Color color, float size)
    {
        EnsureMuzzlePulseParticles();
        if (muzzlePulseParticles == null)
        {
            return;
        }

        muzzlePulseParticles.transform.position = worldPosition;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = worldPosition,
            velocity = direction * 0.55f,
            startColor = color,
            startLifetime = 0.05f,
            startSize = Mathf.Max(0.02f, size),
            applyShapeToPosition = false
        };
        muzzlePulseParticles.Emit(emit, 1);

        Color coreColor = Color.Lerp(color, Color.white, 0.55f);
        emit.velocity = direction * 0.2f;
        emit.startColor = coreColor;
        emit.startLifetime = 0.035f;
        emit.startSize = Mathf.Max(0.02f, size * 0.55f);
        muzzlePulseParticles.Emit(emit, 1);
    }

    private void EnsureMuzzlePulseParticles()
    {
        if (muzzlePulseParticles != null)
        {
            return;
        }

        GameObject pulseObject = new GameObject("WeaponMuzzlePulse_Runtime");
        pulseObject.transform.SetParent(transform, false);
        muzzlePulseParticles = pulseObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = muzzlePulseParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;
        main.startSpeed = 0f;
        main.startLifetime = 0.06f;
        main.startSize = 0.2f;

        ParticleSystem.EmissionModule emission = muzzlePulseParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = muzzlePulseParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = pulseObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortingLayerID = baseSpriteRenderer != null
            ? baseSpriteRenderer.sortingLayerID
            : 0;
        particleRenderer.sortingOrder = baseSpriteRenderer != null
            ? baseSpriteRenderer.sortingOrder + 8
            : 8;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            muzzlePulseMaterial = new Material(shader)
            {
                name = "Runtime_WeaponMuzzlePulse_Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            particleRenderer.sharedMaterial = muzzlePulseMaterial;
        }
    }

    private void ResetFireFeedback()
    {
        recoilSequence?.Kill();
        recoilSequence = null;

        if (recoilVisualRoot != null && recoilRestStateCached)
        {
            recoilVisualRoot.localPosition = recoilRestLocalPosition;
            recoilVisualRoot.localRotation = recoilRestLocalRotation;
        }

        if (muzzlePulseParticles != null)
        {
            muzzlePulseParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        nextMachineGunCameraTime = 0f;
    }

    private void ResolveFireFeedbackProfile(
        WeaponTreeType weaponTreeType,
        float powerRatio,
        out float recoilDistance,
        out float rotationDegrees,
        out float kickDuration,
        out float returnDuration,
        out float cameraAmplitude,
        out float cameraDuration,
        out float muzzleSize)
    {
        switch (weaponTreeType)
        {
            case WeaponTreeType.Shotgun:
                recoilDistance = shotgunRecoilDistance;
                rotationDegrees = 2f;
                kickDuration = 0.035f;
                returnDuration = 0.085f;
                cameraAmplitude = shotgunCameraAmplitude;
                cameraDuration = 0.1f;
                muzzleSize = 0.22f;
                break;

            case WeaponTreeType.Sniper:
                recoilDistance = Mathf.Lerp(sniperRecoilDistance * 0.7f, sniperRecoilDistance, powerRatio);
                rotationDegrees = Mathf.Lerp(0.9f, 1.3f, powerRatio);
                kickDuration = 0.04f;
                returnDuration = 0.1f;
                cameraAmplitude = Mathf.Lerp(sniperCameraAmplitude * 0.67f, sniperCameraAmplitude, powerRatio);
                cameraDuration = Mathf.Lerp(0.09f, 0.12f, powerRatio);
                muzzleSize = Mathf.Lerp(0.15f, 0.21f, powerRatio);
                break;

            default:
                recoilDistance = machineGunRecoilDistance;
                rotationDegrees = 0.25f;
                kickDuration = 0.018f;
                returnDuration = 0.045f;
                cameraAmplitude = machineGunCameraAmplitude;
                cameraDuration = 0.04f;
                muzzleSize = 0.08f;
                break;
        }
    }
}
