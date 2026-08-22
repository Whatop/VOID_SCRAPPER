using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CoreActivationPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform pulseOrigin;
    [SerializeField] private SpriteRenderer[] tintRenderers;
    [SerializeField] private Renderer[] renderersToHide;
    [SerializeField] private Collider2D[] collidersToDisable;

    [Header("Animator")]
    [SerializeField] private string activateTrigger = "Activate";
    [SerializeField] private string collapseTrigger = "Collapse";

    [Header("Animator Startup / Spent State")]
    [Tooltip("플레이 시작 시 Animator 자체를 꺼서 Default State가 Activate여도 자동 재생되지 않게 합니다.")]
    [SerializeField] private bool disableAnimatorUntilActivation = true;
    [Tooltip("Trigger 전환 대신 Activate 상태를 0프레임부터 직접 재생합니다.")]
    [SerializeField] private bool playActivateStateDirectly = true;
    [Tooltip("프리팹이 켜질 때 Idle 상태가 있으면 첫 프레임으로 강제 초기화합니다.")]
    [SerializeField] private bool forceIdleStateOnEnable = true;
    [SerializeField] private string idleStateName = "Idle";
    [Tooltip("활성화 후 코어를 숨기지 않고 방전된 모습으로 유지합니다.")]
    [SerializeField] private bool keepSpentVisualAfterActivation;
    [SerializeField] private string spentStateName = "Spent";
    [Tooltip("Spent 상태가 없으면 Activate 애니메이션의 마지막 프레임을 정지해 유지합니다.")]
    [SerializeField] private bool freezeLastFrameWhenSpentStateMissing = true;
    [Tooltip("활성화가 끝난 뒤 상호작용/물리 콜라이더를 비활성화합니다.")]
    [SerializeField] private bool disableCollidersAfterActivation = true;

    [Header("Animator Timing Sync")]
    [Tooltip("Activate 상태의 길이를 읽어 펄스와 붕괴 시점을 스프라이트 애니메이션에 맞춥니다.")]
    [SerializeField] private bool synchronizeWithActivateAnimation = true;
    [Tooltip("Animator의 Activate 상태 이름입니다. 이름을 못 찾으면 현재 상태 길이 또는 Fallback Duration을 사용합니다.")]
    [SerializeField] private string activateStateName = "Activate";
    [Min(0.05f)]
    [SerializeField] private float fallbackActivateAnimationDuration = 0.75f;
    [Tooltip("Activate 애니메이션 진행도 중 노란 펄스를 시작할 시점입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float pulseStartNormalizedTime = 0.52f;
    [Tooltip("Animator가 정상 재생 중일 때도 코드의 확대/색상 Fallback을 겹쳐서 재생합니다. 보통 꺼두는 것이 자연스럽습니다.")]
    [SerializeField] private bool allowFallbackMotionWithAnimator;

    [Header("Activation Timing")]
    [Min(0f)]
    [SerializeField] private float prePulseDelay = 0.08f;
    [Min(0f)]
    [SerializeField] private float pulseHoldDuration = 0.2f;
    [Min(0.05f)]
    [SerializeField] private float collapseDuration = 0.32f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Yellow Pulse")]
    [SerializeField] private GameObject pulsePrefab;
    [SerializeField] private Material runtimePulseMaterial;
    [SerializeField] private Color pulseColor = new Color(1f, 0.82f, 0.08f, 0.95f);
    [Range(1, 8)]
    [SerializeField] private int pulseCount = 3;
    [Min(0.01f)]
    [SerializeField] private float pulseInterval = 0.14f;
    [Min(0.05f)]
    [SerializeField] private float pulseDuration = 0.72f;
    [Min(0.01f)]
    [SerializeField] private float pulseStartRadius = 0.35f;
    [Min(0.05f)]
    [SerializeField] private float pulseEndRadius = 6.5f;
    [Min(0.001f)]
    [SerializeField] private float pulseStartWidth = 0.14f;
    [Min(0.001f)]
    [SerializeField] private float pulseEndWidth = 0.02f;
    [Range(12, 128)]
    [SerializeField] private int pulseSegments = 64;
    [SerializeField] private string pulseSortingLayerName = "Default";
    [SerializeField] private int pulseSortingOrder = 80;
    [SerializeField] private AnimationCurve pulseExpansionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fallback Core Motion")]
    [SerializeField] private bool useFallbackMotion = true;
    [Min(1f)]
    [SerializeField] private float chargeScale = 1.16f;
    [Min(0.05f)]
    [SerializeField] private float chargeScaleDuration = 0.26f;
    [SerializeField] private Color chargeTint = new Color(1f, 0.88f, 0.25f, 1f);
    [Tooltip("Legacy 옵션입니다. Keep Spent Visual이 꺼져 있을 때만 사용합니다.")]
    [SerializeField] private bool hideCoreVisualAfterActivation;
    [Min(0f)]
    [SerializeField] private float pulseShakeAmplitude = 0.12f;
    [Min(0f)]
    [SerializeField] private float pulseShakeDuration = 0.2f;

    private Vector3 baseVisualScale = Vector3.one;
    private Color[] baseTintColors;
    private bool captured;
    private bool playing;
    private bool finished;
    private float cachedAnimatorSpeed = 1f;
    private bool animatorSpeedCaptured;

    public bool IsPlaying => playing;
    public bool HasFinished => finished;
    public bool KeepsSpentVisual => keepSpentVisualAfterActivation;
    public Transform FocusTarget => pulseOrigin != null ? pulseOrigin : transform;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
        visualRoot = transform;
        pulseOrigin = transform;
        tintRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        renderersToHide = GetComponentsInChildren<Renderer>(true);
        collidersToDisable = GetComponentsInChildren<Collider2D>(true);
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseState();
        CaptureAnimatorSpeed();
        ResetAnimatorToIdle();
    }

    private void OnEnable()
    {
        if (!playing && !finished)
        {
            ResetAnimatorToIdle();
            RestoreVisualState();
        }
    }

    public IEnumerator PlayActivationRoutine()
    {
        if (playing || finished)
        {
            yield break;
        }

        ResolveReferences();
        CaptureBaseState();

        playing = true;
        RestoreAnimatorSpeed();

        bool animatorActivated = StartActivateAnimation();
        float activateAnimationDuration = 0f;

        if (animatorActivated && synchronizeWithActivateAnimation)
        {
            // Trigger가 실제 상태 전환에 반영된 뒤 길이를 읽습니다.
            yield return null;
            activateAnimationDuration = ResolveActivateAnimationDuration();
        }

        Coroutine fallbackRoutine = null;
        bool shouldUseFallbackMotion =
            useFallbackMotion &&
            visualRoot != null &&
            (!animatorActivated || allowFallbackMotionWithAnimator);

        if (shouldUseFallbackMotion)
        {
            fallbackRoutine = StartCoroutine(PlayFallbackChargeRoutine());
        }

        float elapsedBeforePulse = 0f;
        float pulseDelay = Mathf.Max(0f, prePulseDelay);

        if (animatorActivated && synchronizeWithActivateAnimation)
        {
            pulseDelay = Mathf.Max(
                pulseDelay,
                activateAnimationDuration * Mathf.Clamp01(pulseStartNormalizedTime)
            );
        }

        if (pulseDelay > 0f)
        {
            yield return Wait(pulseDelay);
            elapsedBeforePulse += pulseDelay;
        }

        int safePulseCount = Mathf.Max(1, pulseCount);

        for (int i = 0; i < safePulseCount; i++)
        {
            SpawnPulse();

            if (i == 0 && pulseShakeAmplitude > 0f && pulseShakeDuration > 0f)
            {
                GungeonStyleCamera2D.RequestShake(pulseShakeAmplitude, pulseShakeDuration);
            }

            if (i < safePulseCount - 1 && pulseInterval > 0f)
            {
                yield return Wait(pulseInterval);
                elapsedBeforePulse += pulseInterval;
            }
        }

        if (animatorActivated && synchronizeWithActivateAnimation)
        {
            float remainingAnimationTime = activateAnimationDuration - elapsedBeforePulse;

            if (remainingAnimationTime > 0f)
            {
                yield return Wait(remainingAnimationTime);
            }
        }

        if (fallbackRoutine != null)
        {
            yield return fallbackRoutine;
        }

        if (pulseHoldDuration > 0f)
        {
            yield return Wait(pulseHoldDuration);
        }

        if (keepSpentVisualAfterActivation)
        {
            EnterSpentState();

            if (disableCollidersAfterActivation)
            {
                SetCollidersEnabled(false);
            }
        }
        else
        {
            TriggerAnimator(collapseTrigger);

            if (hideCoreVisualAfterActivation)
            {
                yield return CollapseAndHideRoutine();
            }
            else if (disableCollidersAfterActivation)
            {
                SetCollidersEnabled(false);
            }
        }

        finished = true;
        playing = false;
    }

    public void ResetPresentation()
    {
        StopAllCoroutines();
        ResolveReferences();
        CaptureBaseState();

        playing = false;
        finished = false;

        RestoreAnimatorSpeed();
        ResetAnimatorToIdle();
        RestoreVisualState();
    }

    private IEnumerator PlayFallbackChargeRoutine()
    {
        float duration = Mathf.Max(0.05f, chargeScaleDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);

            visualRoot.localScale = Vector3.LerpUnclamped(
                baseVisualScale,
                baseVisualScale * Mathf.Max(1f, chargeScale),
                pulse
            );

            ApplyTint(Color.Lerp(Color.white, chargeTint, pulse));
            yield return null;
        }

        visualRoot.localScale = baseVisualScale;
        RestoreTint();
    }

    private IEnumerator CollapseAndHideRoutine()
    {
        if (visualRoot == null)
        {
            SetCoreVisible(false);
            yield break;
        }

        Vector3 startScale = visualRoot.localScale;
        float duration = Mathf.Max(0.05f, collapseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = normalized * normalized;

            visualRoot.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, eased);
            ApplyTint(Color.Lerp(Color.white, pulseColor, normalized));
            yield return null;
        }

        SetCoreVisible(false);
        visualRoot.localScale = baseVisualScale;
        RestoreTint();
    }

    private void SpawnPulse()
    {
        Vector3 position = FocusTarget != null ? FocusTarget.position : transform.position;

        if (pulsePrefab != null)
        {
            GameObject instance = Instantiate(pulsePrefab, position, Quaternion.identity);
            ExpandingPulseRing2D pulse = instance.GetComponent<ExpandingPulseRing2D>();

            if (pulse == null)
            {
                pulse = instance.AddComponent<ExpandingPulseRing2D>();
            }

            pulse.Initialize(
                pulseColor,
                pulseDuration,
                pulseStartRadius,
                pulseEndRadius,
                pulseStartWidth,
                pulseEndWidth,
                pulseSegments,
                pulseSortingLayerName,
                pulseSortingOrder,
                runtimePulseMaterial,
                pulseExpansionCurve,
                useUnscaledTime
            );

            return;
        }

        ExpandingPulseRing2D.Spawn(
            position,
            pulseColor,
            pulseDuration,
            pulseStartRadius,
            pulseEndRadius,
            pulseStartWidth,
            pulseEndWidth,
            pulseSegments,
            pulseSortingLayerName,
            pulseSortingOrder,
            runtimePulseMaterial,
            pulseExpansionCurve,
            useUnscaledTime
        );
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (pulseOrigin == null)
        {
            pulseOrigin = transform;
        }

        if (tintRenderers == null || tintRenderers.Length == 0)
        {
            tintRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (renderersToHide == null || renderersToHide.Length == 0)
        {
            renderersToHide = GetComponentsInChildren<Renderer>(true);
        }

        if (collidersToDisable == null || collidersToDisable.Length == 0)
        {
            collidersToDisable = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void CaptureBaseState()
    {
        if (captured)
        {
            return;
        }

        if (visualRoot != null)
        {
            baseVisualScale = visualRoot.localScale;
        }

        if (tintRenderers != null)
        {
            baseTintColors = new Color[tintRenderers.Length];

            for (int i = 0; i < tintRenderers.Length; i++)
            {
                baseTintColors[i] = tintRenderers[i] != null
                    ? tintRenderers[i].color
                    : Color.white;
            }
        }

        captured = true;
    }

    private void SetCoreVisible(bool visible)
    {
        SetRenderersEnabled(visible);
        SetCollidersEnabled(visible);
    }

    private void SetRenderersEnabled(bool visible)
    {
        if (renderersToHide == null)
        {
            return;
        }

        for (int i = 0; i < renderersToHide.Length; i++)
        {
            if (renderersToHide[i] != null)
            {
                renderersToHide[i].enabled = visible;
            }
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (collidersToDisable == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            if (collidersToDisable[i] != null)
            {
                collidersToDisable[i].enabled = enabled;
            }
        }
    }

    private void RestoreVisualState()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = baseVisualScale;
        }

        RestoreTint();
        SetRenderersEnabled(true);
        SetCollidersEnabled(true);
    }

    private void CaptureAnimatorSpeed()
    {
        if (animator == null || animatorSpeedCaptured)
        {
            return;
        }

        cachedAnimatorSpeed = Mathf.Approximately(animator.speed, 0f) ? 1f : animator.speed;
        animatorSpeedCaptured = true;
    }

    private void RestoreAnimatorSpeed()
    {
        if (animator == null)
        {
            return;
        }

        CaptureAnimatorSpeed();
        animator.speed = Mathf.Approximately(cachedAnimatorSpeed, 0f) ? 1f : cachedAnimatorSpeed;
    }

    private void ResetAnimatorToIdle()
    {
        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        RestoreAnimatorSpeed();
        ResetAnimatorTriggers();

        if (forceIdleStateOnEnable && HasAnimatorState(animator, idleStateName))
        {
            animator.Play(idleStateName, 0, 0f);
            animator.Update(0f);
        }

        if (disableAnimatorUntilActivation)
        {
            animator.enabled = false;
        }
    }

    private bool StartActivateAnimation()
    {
        if (animator == null)
        {
            return false;
        }

        animator.enabled = true;
        RestoreAnimatorSpeed();
        ResetAnimatorTriggers();

        if (playActivateStateDirectly && HasAnimatorState(animator, activateStateName))
        {
            animator.Play(activateStateName, 0, 0f);
            animator.Update(0f);
            return true;
        }

        return TriggerAnimator(activateTrigger);
    }

    private void EnterSpentState()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = baseVisualScale;
        }

        RestoreTint();
        SetRenderersEnabled(true);

        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        bool playedSpentState = false;

        if (HasAnimatorState(animator, spentStateName))
        {
            animator.Play(spentStateName, 0, 0f);
            animator.Update(0f);
            playedSpentState = true;
        }

        if (!playedSpentState && freezeLastFrameWhenSpentStateMissing && HasAnimatorState(animator, activateStateName))
        {
            animator.Play(activateStateName, 0, 0.999f);
            animator.Update(0f);
            animator.enabled = false;
        }
    }

    private void ResetAnimatorTriggers()
    {
        if (animator == null)
        {
            return;
        }

        if (HasAnimatorParameter(animator, activateTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(activateTrigger);
        }

        if (HasAnimatorParameter(animator, collapseTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(collapseTrigger);
        }
    }

    private static bool HasAnimatorState(Animator target, string stateName)
    {
        if (target == null || target.layerCount <= 0 || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        int fullHash = Animator.StringToHash($"{target.GetLayerName(0)}.{stateName}");

        return target.HasState(0, shortHash) || target.HasState(0, fullHash);
    }

    private void ApplyTint(Color multiplier)
    {
        if (tintRenderers == null || baseTintColors == null)
        {
            return;
        }

        for (int i = 0; i < tintRenderers.Length && i < baseTintColors.Length; i++)
        {
            SpriteRenderer target = tintRenderers[i];

            if (target == null)
            {
                continue;
            }

            Color baseColor = baseTintColors[i];
            target.color = new Color(
                baseColor.r * multiplier.r,
                baseColor.g * multiplier.g,
                baseColor.b * multiplier.b,
                baseColor.a
            );
        }
    }

    private void RestoreTint()
    {
        if (tintRenderers == null || baseTintColors == null)
        {
            return;
        }

        for (int i = 0; i < tintRenderers.Length && i < baseTintColors.Length; i++)
        {
            if (tintRenderers[i] != null)
            {
                tintRenderers[i].color = baseTintColors[i];
            }
        }
    }

    private bool TriggerAnimator(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        if (!HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Trigger))
        {
            return false;
        }

        if (!animator.enabled)
        {
            animator.enabled = true;
        }

        animator.ResetTrigger(parameterName);
        animator.SetTrigger(parameterName);
        return true;
    }

    private float ResolveActivateAnimationDuration()
    {
        float fallbackDuration = Mathf.Max(0.05f, fallbackActivateAnimationDuration);

        if (animator == null || animator.layerCount <= 0)
        {
            return fallbackDuration;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(0);

            if (string.IsNullOrWhiteSpace(activateStateName) || MatchesAnimatorState(nextStateInfo, activateStateName))
            {
                stateInfo = nextStateInfo;
            }
        }

        if (!string.IsNullOrWhiteSpace(activateStateName) && !MatchesAnimatorState(stateInfo, activateStateName))
        {
            return fallbackDuration;
        }

        float animatorSpeed = Mathf.Max(0.01f, Mathf.Abs(animator.speed));
        float resolvedDuration = stateInfo.length / animatorSpeed;

        return resolvedDuration > 0.05f
            ? resolvedDuration
            : fallbackDuration;
    }

    private static bool MatchesAnimatorState(AnimatorStateInfo stateInfo, string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return true;
        }

        int stateHash = Animator.StringToHash(stateName);

        return stateInfo.shortNameHash == stateHash ||
               stateInfo.fullPathHash == stateHash ||
               stateInfo.IsName(stateName);
    }

    private static bool HasAnimatorParameter(
        Animator target,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (target == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = target.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter != null &&
                parameter.type == expectedType &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
