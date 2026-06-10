using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EmergencyReturnExitSequence : MonoBehaviour
{
    [Header("Visual Move")]
    [Tooltip("실제로 화면에서 날아갈 기체 비주얼 루트. Player 루트가 아니라 VisualRoot를 넣어라.")]
    [SerializeField] private Transform visualRoot;

    [SerializeField] private Vector2 exitDirectionWorld = Vector2.right;
    [SerializeField] private float exitDistance = 12f;
    [SerializeField] private float exitDuration = 0.75f;
    [SerializeField] private AnimationCurve exitMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visual Scale Optional")]
    [SerializeField] private bool scaleDuringExit = true;
    [SerializeField] private float endScaleMultiplier = 0.75f;

    [Header("Visual State")]
    [SerializeField] private bool hideVisualAfterMove = true;

    [Header("Fade And Result")]
    [SerializeField] private bool useScreenFade = true;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float blackHoldBeforeResult = 0.05f;
    [SerializeField] private float waitResultPanelBeforeFadeOut = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("References")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerWeaponController playerWeaponController;
    [SerializeField] private PlayerInteractor playerInteractor;

    [Header("Disable During Exit")]
    [SerializeField] private Collider2D[] collidersToDisableDuringExit;
    [SerializeField] private MonoBehaviour[] extraComponentsToDisableDuringExit;

    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Reset()
    {
        visualRoot = transform.Find("VisualRoot");

        playerRigidbody = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController2D>();
        playerDash = GetComponent<PlayerDash>();
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerInteractor = GetComponent<PlayerInteractor>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    public void PlayEmergencyReturn()
    {
        if (isPlaying)
        {
            return;
        }

        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        isPlaying = true;

        CacheReferences();
        DisablePlayerControlAndCollision();

        yield return MoveVisualOut();

        if (useScreenFade && screenFader != null)
        {
            yield return screenFader.FadeIn(fadeInDuration);
        }

        if (blackHoldBeforeResult > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldBeforeResult);
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CompleteRun(RunEndReason.EmergencyReturn);
        }

        if (waitResultPanelBeforeFadeOut > 0f)
        {
            yield return new WaitForSecondsRealtime(waitResultPanelBeforeFadeOut);
        }
        else
        {
            yield return null;
        }

        if (useScreenFade && screenFader != null)
        {
            yield return screenFader.FadeOut(fadeOutDuration);
        }

        isPlaying = false;
    }

    private IEnumerator MoveVisualOut()
    {
        Transform targetVisual = visualRoot != null ? visualRoot : transform;

        Vector3 startPosition = targetVisual.position;

        Vector3 direction = exitDirectionWorld;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        Vector3 endPosition = startPosition + direction * Mathf.Max(0.1f, exitDistance);

        Vector3 startScale = targetVisual.localScale;
        Vector3 endScale = startScale * Mathf.Max(0.01f, endScaleMultiplier);

        float timer = 0f;
        float duration = Mathf.Max(0.01f, exitDuration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float eased = exitMoveCurve != null ? exitMoveCurve.Evaluate(t) : t;

            targetVisual.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);

            if (scaleDuringExit)
            {
                targetVisual.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            }

            yield return null;
        }

        targetVisual.position = endPosition;

        if (scaleDuringExit)
        {
            targetVisual.localScale = endScale;
        }

        if (hideVisualAfterMove && targetVisual != transform)
        {
            targetVisual.gameObject.SetActive(false);
        }
    }

    private void DisablePlayerControlAndCollision()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        if (playerController != null)
        {
            playerController.SetControlEnabled(false);
            playerController.SetMovementLocked(true);
        }

        if (playerDash != null)
        {
            playerDash.enabled = false;
        }

        if (playerWeaponController != null)
        {
            playerWeaponController.enabled = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = false;
        }

        if (collidersToDisableDuringExit != null)
        {
            for (int i = 0; i < collidersToDisableDuringExit.Length; i++)
            {
                Collider2D targetCollider = collidersToDisableDuringExit[i];

                if (targetCollider != null)
                {
                    targetCollider.enabled = false;
                }
            }
        }

        if (extraComponentsToDisableDuringExit != null)
        {
            for (int i = 0; i < extraComponentsToDisableDuringExit.Length; i++)
            {
                MonoBehaviour component = extraComponentsToDisableDuringExit[i];

                if (component != null && component != this)
                {
                    component.enabled = false;
                }
            }
        }
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
        {
            Transform foundVisualRoot = transform.Find("VisualRoot");

            if (foundVisualRoot != null)
            {
                visualRoot = foundVisualRoot;
            }
        }

        if (screenFader == null)
        {
            screenFader = ScreenFader.Instance;
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (playerWeaponController == null)
        {
            playerWeaponController = GetComponent<PlayerWeaponController>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = GetComponent<PlayerInteractor>();
        }

        if (collidersToDisableDuringExit == null || collidersToDisableDuringExit.Length == 0)
        {
            collidersToDisableDuringExit = GetComponentsInChildren<Collider2D>();
        }
    }
}