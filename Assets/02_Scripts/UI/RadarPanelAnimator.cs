using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RadarPanelAnimator : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("전체 HUD Canvas가 아니라 RadarPanelRoot만 넣어야 합니다.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("RadarPanelRoot에 붙은 CanvasGroup을 넣어야 합니다. 전체 Canvas의 CanvasGroup을 넣으면 HUD 전체가 꺼집니다.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Frame Animation")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Sprite[] openFrames;
    [SerializeField] private Sprite[] closeFrames;
    [SerializeField] private float frameRate = 12f;

    [Header("Functional Radar")]
    [Tooltip("실제 마커/레이더 이미지 루트. 열림 애니메이션이 끝난 뒤 켜집니다.")]
    [SerializeField] private GameObject functionalRadarRoot;

    [Header("Alpha")]
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("Startup")]
    [Tooltip("켜두면 시작 시 레이더만 닫힌 상태로 초기화됩니다. HUD 전체는 건드리지 않습니다.")]
    [SerializeField] private bool closeOnStart = true;

    [Tooltip("비추천. 켜면 닫을 때 panelRoot를 SetActive(false) 합니다. 실수로 전체 Canvas를 넣으면 HUD 전체가 꺼집니다.")]
    [SerializeField] private bool deactivatePanelRootWhenClosed = false;

    private Coroutine routine;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Reset()
    {
        panelRoot = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        if (frameRate <= 0f)
        {
            frameRate = 12f;
        }

        // 중요:
        // 레이더 루트는 꺼버리지 말고 항상 Active 상태로 둔다.
        // 그래야 자기 자신이나 자식 스크립트가 비활성화되지 않는다.
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (closeOnStart)
        {
            CloseImmediate();
        }
        else
        {
            OpenImmediate();
        }
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        StopCurrentRoutine();
        routine = StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        if (!isOpen)
        {
            CloseImmediate();
            return;
        }

        StopCurrentRoutine();
        routine = StartCoroutine(CloseRoutine());
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void OpenImmediate()
    {
        StopCurrentRoutine();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetCanvasGroup(1f, true);

        if (frameImage != null && openFrames != null && openFrames.Length > 0)
        {
            frameImage.sprite = openFrames[openFrames.Length - 1];
            frameImage.enabled = true;
        }

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(true);
        }

        isOpen = true;
    }

    public void CloseImmediate()
    {
        StopCurrentRoutine();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        if (frameImage != null)
        {
            if (closeFrames != null && closeFrames.Length > 0)
            {
                frameImage.sprite = closeFrames[closeFrames.Length - 1];
                frameImage.enabled = true;
            }
            else if (openFrames != null && openFrames.Length > 0)
            {
                frameImage.sprite = openFrames[0];
                frameImage.enabled = true;
            }
        }

        SetCanvasGroup(0f, false);

        if (deactivatePanelRootWhenClosed && panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isOpen = false;
    }

    private IEnumerator OpenRoutine()
    {
        isOpen = true;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        yield return FadeCanvasGroup(0f, 1f, fadeDuration);

        yield return PlayFrames(openFrames);

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(true);
        }

        SetCanvasGroup(1f, true);
        routine = null;
    }

    private IEnumerator CloseRoutine()
    {
        isOpen = false;

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        yield return PlayFrames(closeFrames);

        yield return FadeCanvasGroup(1f, 0f, fadeDuration);

        SetCanvasGroup(0f, false);

        if (deactivatePanelRootWhenClosed && panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        routine = null;
    }

    private IEnumerator PlayFrames(Sprite[] frames)
    {
        if (frameImage == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        frameImage.enabled = true;

        float delay = 1f / Mathf.Max(1f, frameRate);

        for (int i = 0; i < frames.Length; i++)
        {
            frameImage.sprite = frames[i];
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        duration = Mathf.Max(0.001f, duration);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float alpha = Mathf.Lerp(from, to, t);

            SetCanvasGroup(alpha, alpha > 0.01f);

            yield return null;
        }

        SetCanvasGroup(to, to > 0.01f);
    }

    private void SetCanvasGroup(float alpha, bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);

        // 레이더는 HUD 위에 올라오는 정보창이라 열렸을 때만 입력을 막도록 둔다.
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void StopCurrentRoutine()
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }
}