using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossCinematicLetterboxUI : MonoBehaviour
{
    [Header("Runtime UI")]
    [Range(0.02f, 0.2f)]
    [SerializeField] private float defaultHeightRatio = 0.085f;
    [SerializeField] private int sortingOrder = 9990;
    [SerializeField] private Color barColor = Color.black;

    private Canvas canvas;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private Coroutine animationRoutine;
    private float currentRatio;

    public float CurrentRatio => currentRatio;

    public static BossCinematicLetterboxUI GetOrCreate()
    {
        BossCinematicLetterboxUI existing = FindFirstObjectByType<BossCinematicLetterboxUI>();

        if (existing != null)
        {
            existing.EnsureRuntimeUi();
            return existing;
        }

        GameObject root = new GameObject("Boss Cinematic Letterbox", typeof(RectTransform));
        BossCinematicLetterboxUI created = root.AddComponent<BossCinematicLetterboxUI>();
        created.EnsureRuntimeUi();
        return created;
    }

    private void Awake()
    {
        EnsureRuntimeUi();
        ApplyRatio(0f);
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    public IEnumerator ShowRoutine(float heightRatio, float duration)
    {
        yield return AnimateRoutine(Mathf.Clamp(heightRatio, 0f, 0.25f), duration);
    }

    public IEnumerator ShowRoutine(float duration)
    {
        yield return ShowRoutine(defaultHeightRatio, duration);
    }

    public IEnumerator HideRoutine(float duration)
    {
        yield return AnimateRoutine(0f, duration);
    }

    public void ShowImmediate(float heightRatio)
    {
        StopAnimation();
        ApplyRatio(Mathf.Clamp(heightRatio, 0f, 0.25f));
    }

    public void HideImmediate()
    {
        StopAnimation();
        ApplyRatio(0f);
    }

    private IEnumerator AnimateRoutine(float targetRatio, float duration)
    {
        StopAnimation();
        EnsureRuntimeUi();

        float startRatio = currentRatio;
        float safeDuration = Mathf.Max(0f, duration);

        if (safeDuration <= 0.0001f)
        {
            ApplyRatio(targetRatio);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t * t * (3f - 2f * t);

            ApplyRatio(Mathf.LerpUnclamped(startRatio, targetRatio, eased));
            yield return null;
        }

        ApplyRatio(targetRatio);
        animationRoutine = null;
    }

    private void StopAnimation()
    {
        if (animationRoutine == null)
        {
            return;
        }

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private void EnsureRuntimeUi()
    {
        if (canvas != null && topBar != null && bottomBar != null)
        {
            return;
        }

        canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();

        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();

        if (raycaster != null)
        {
            raycaster.enabled = false;
        }

        topBar = CreateBar("Top Letterbox");
        bottomBar = CreateBar("Bottom Letterbox");
        ApplyRatio(currentRatio);
    }

    private RectTransform CreateBar(string objectName)
    {
        Transform existing = transform.Find(objectName);

        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;

            if (existingRect != null)
            {
                return existingRect;
            }
        }

        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(transform, false);

        Image image = barObject.GetComponent<Image>();
        image.color = barColor;
        image.raycastTarget = false;

        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void ApplyRatio(float ratio)
    {
        currentRatio = Mathf.Clamp(ratio, 0f, 0.25f);

        if (topBar != null)
        {
            topBar.anchorMin = new Vector2(0f, 1f - currentRatio);
            topBar.anchorMax = Vector2.one;
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
        }

        if (bottomBar != null)
        {
            bottomBar.anchorMin = Vector2.zero;
            bottomBar.anchorMax = new Vector2(1f, currentRatio);
            bottomBar.offsetMin = Vector2.zero;
            bottomBar.offsetMax = Vector2.zero;
        }
    }
}
