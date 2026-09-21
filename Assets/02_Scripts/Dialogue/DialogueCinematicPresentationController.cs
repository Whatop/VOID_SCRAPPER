using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[AddComponentMenu("VOID SCRAPPER/Dialogue/Dialogue Cinematic Presentation Controller")]
[DisallowMultipleComponent]
[RequireComponent(typeof(StandardDialogueUI))]
public sealed class DialogueCinematicPresentationController : MonoBehaviour
{
    private const string PresentationRootName = "VOID SCRAPPER Presentation Surface";
    private const string ContinueIndicator = "▼";

    [Header("Unscaled Transitions")]
    [SerializeField, Min(0f)] private float overlayTransitionDuration =
        DialoguePresentationPolicy.OverlayTransitionDuration;
    [SerializeField, Min(0f)] private float panelTransitionDuration =
        DialoguePresentationPolicy.PanelTransitionDuration;
    [SerializeField, Min(0.01f)] private float sourcePanelAnimationDuration = 0.5f;

    [Header("Explicit Remote Communication Intro")]
    [SerializeField, Range(0.15f, 0.4f)] private float incomingCommunicationDuration = 0.24f;
    private Sequence incomingCommunicationTween;
    private string incomingConversationId;
    public bool IsIncomingCommunicationIntroActive => incomingCommunicationTween != null && incomingCommunicationTween.IsActive();

    [Header("Reference Layout")]
    [SerializeField, Min(1f)] private float compactPanelHeight = 60f;
    [SerializeField, Min(1f)] private float cinematicPanelHeight =
        DialoguePresentationPolicy.CommunicationPanelHeight;
    [SerializeField, Min(1f)] private float portraitAreaWidth =
        DialoguePresentationPolicy.PortraitAreaWidth;
    [SerializeField, Min(1f)] private float cinematicTopBarHeight =
        DialoguePresentationPolicy.CinematicTopBarHeight;

    [Header("Presentation Colors")]
    [SerializeField] private Color operatorAccent =
        new Color(0.22f, 0.76f, 0.95f, 1f);
    [SerializeField] private Color curseAccent =
        new Color(0.68f, 0.28f, 0.96f, 1f);
    [SerializeField] private Color settlementAccent =
        new Color(0.96f, 0.72f, 0.38f, 1f);
    [SerializeField] private Color neutralAccent =
        new Color(0.84f, 0.86f, 0.88f, 1f);
    [SerializeField] private Color panelBackground =
        new Color(0.025f, 0.055f, 0.085f, 0.97f);

    private readonly List<SubtitlePanelAppearance> panelAppearances =
        new List<SubtitlePanelAppearance>(2);
    private readonly DialoguePresentationLifecycleState lifecycleState =
        new DialoguePresentationLifecycleState();

    private StandardDialogueUI standardDialogueUi;
    private DialogueSystemController dialogueController;
    private CanvasGroup overlayCanvasGroup;
    private GameObject overlayRoot;
    private Image topBarImage;
    private RectTransform topBarRect;
    private Image accentLineImage;
    private Coroutine overlayRoutine;
    private Coroutine continuePulseRoutine;
    private Coroutine curseGlitchRoutine;
    private ExpeditionHUD ownedExpeditionHud;
    private GameObject selectionBeforeConversation;
    private bool ownsHudCinematicMode;
    private bool subscribed;

    public DialoguePresentationMode CurrentMode { get; private set; }
    public DialogueActorTheme CurrentActorTheme { get; private set; }
    public bool IsPresentationActive => lifecycleState.IsActive;
    public bool OwnsHudCinematicMode => ownsHudCinematicMode;
    public bool TakesCameraOwnership => false;
    public bool UsesUnscaledTransitions => true;
    public float OverlayTransitionDuration => overlayTransitionDuration;
    public float PanelTransitionDuration => panelTransitionDuration;
    public float CompactPanelHeight => compactPanelHeight;
    public float CinematicPanelHeight => cinematicPanelHeight;
    public float PortraitAreaWidth => portraitAreaWidth;
    public float CinematicTopBarHeight => cinematicTopBarHeight;
    public float CurrentDimAlpha => overlayCanvasGroup != null
        ? overlayCanvasGroup.alpha
        : 0f;
    public bool IsTopBarVisible => topBarImage != null && topBarImage.gameObject.activeSelf;

    private sealed class SubtitlePanelAppearance
    {
        public StandardUISubtitlePanel Panel;
        public RectTransform Rect;
        public Image Background;
        public Image SpeakerPlate;
        public TMP_Text SpeakerName;
        public Image ContinueBackground;
        public TMP_Text ContinueLabel;
        public LayoutElement PortraitLayout;
        public LayoutElement SpeakerPlateLayout;
        public ContentSizeFitter Fitter;
        public HorizontalLayoutGroup Layout;
        public Vector3 IntroBaselineScale;
        public bool HasIntroBaseline;
    }

    private void Awake()
    {
        standardDialogueUi = GetComponent<StandardDialogueUI>();
        EnsurePresentationSurface();
        CacheSubtitlePanels();
        ConfigurePixelCrushersPanelTiming();
        ConfigurePanelLayout(DialoguePresentationMode.CompactGuidance);
        ApplyActorTheme(DialogueActorTheme.Neutral);
        ResetPresentationImmediate();
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();

        if (DialogueManager.isConversationActive && !lifecycleState.IsActive)
        {
            BeginPresentation(DialogueManager.lastConversationStarted);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        Unsubscribe();
        EndPresentation(true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        Unsubscribe();
        EndPresentation(true);
    }

    private void TrySubscribe()
    {
        DialogueSystemController nextController = DialogueManager.hasInstance
            ? DialogueManager.instance
            : null;
        if (nextController == null ||
            subscribed && ReferenceEquals(dialogueController, nextController))
        {
            return;
        }

        Unsubscribe();
        dialogueController = nextController;
        dialogueController.conversationStarted += HandleConversationStarted;
        dialogueController.conversationEnded += HandleConversationEnded;
        dialogueController.conversationLinePrepared += HandleConversationLinePrepared;
        dialogueController.stoppingAllConversations += HandleStoppingAllConversations;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || dialogueController == null)
        {
            subscribed = false;
            dialogueController = null;
            return;
        }

        dialogueController.conversationStarted -= HandleConversationStarted;
        dialogueController.conversationEnded -= HandleConversationEnded;
        dialogueController.conversationLinePrepared -= HandleConversationLinePrepared;
        dialogueController.stoppingAllConversations -= HandleStoppingAllConversations;
        subscribed = false;
        dialogueController = null;
    }

    private void HandleConversationStarted(Transform actor)
    {
        string conversationId = dialogueController != null
            ? dialogueController.lastConversationStarted
            : DialogueManager.lastConversationStarted;
        BeginPresentation(conversationId);
    }

    private void HandleConversationEnded(Transform actor)
    {
        EndPresentation(false);
    }

    private void HandleConversationLinePrepared(Subtitle subtitle)
    {
        string actorId = subtitle != null && subtitle.speakerInfo != null
            ? subtitle.speakerInfo.nameInDatabase
            : string.Empty;
        DialogueActorTheme theme = DialoguePresentationPolicy.ResolveActorTheme(
            lifecycleState.ActiveConversationId,
            actorId);
        ApplyActorTheme(theme);
    }

    private void HandleStoppingAllConversations()
    {
        EndPresentation(true);
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        EndPresentation(true);
    }

    private void BeginPresentation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        if (lifecycleState.IsActive && string.Equals(
                lifecycleState.ActiveConversationId,
                conversationId,
                StringComparison.Ordinal))
        {
            return;
        }

        if (lifecycleState.IsActive)
        {
            EndPresentation(true);
        }

        if (!lifecycleState.TryBegin(conversationId))
        {
            return;
        }

        DialoguePresentationProfile profile = DialoguePresentationPolicy.Resolve(
            conversationId,
            SettlementExpeditionLaunchGuard.IsMandatoryFirstSettlementStoryPending);
        CurrentMode = profile.Mode;
        selectionBeforeConversation = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        ConfigurePanelLayout(profile.Mode);
        ApplyActorTheme(DialoguePresentationPolicy.ResolveActorTheme(
            conversationId,
            string.Empty));
        AcquireHudPresentation(profile.Mode);
        BeginOverlayTransition(profile.DimAlpha, profile.ShowsTopBar, false);
        StartContinueIndicatorPulse();

        if (CurrentActorTheme == DialogueActorTheme.Curse)
        {
            StartCurseEntranceGlitch();
        }
    }

    private void EndPresentation(bool immediate)
    {
        CancelIncomingCommunicationIntro();
        if (!lifecycleState.IsActive &&
            overlayCanvasGroup != null &&
            overlayCanvasGroup.alpha <= 0f &&
            !ownsHudCinematicMode)
        {
            return;
        }

        lifecycleState.TryEnd();
        StopContinueIndicatorPulse();
        StopCurseEntranceGlitch();
        ReleaseHudPresentation(immediate);
        ApplyActorTheme(DialogueActorTheme.Neutral);
        BeginOverlayTransition(0f, false, immediate);

        if (immediate)
        {
            RestorePreviousSelection();
        }
    }

    // Called only by the same accepted, explicitly remote start authorities as the audio cue.
    // This is deliberately not attached to the global conversationStarted event.
    public static void BeginIncomingCommunication()
    {
        Component ui = DialogueManager.dialogueUI as Component;
        if (ui != null)
            ui.GetComponent<DialogueCinematicPresentationController>()?.PlayIncomingCommunicationIntro(
                DialogueManager.lastConversationStarted);
    }

    public void PlayIncomingCommunicationIntro(string conversationId)
    {
        if (!isActiveAndEnabled || !DialogueManager.isConversationActive ||
            !string.Equals(conversationId, DialogueManager.lastConversationStarted, StringComparison.Ordinal) ||
            string.Equals(incomingConversationId, conversationId, StringComparison.Ordinal)) return;

        BeginPresentation(conversationId);
        incomingConversationId = conversationId;
        incomingCommunicationTween = DOTween.Sequence().SetUpdate(true)
            .AppendInterval(incomingCommunicationDuration);
        foreach (SubtitlePanelAppearance appearance in panelAppearances)
        {
            if (appearance.Rect == null) continue;
            appearance.IntroBaselineScale = appearance.Rect.localScale;
            appearance.HasIntroBaseline = true;
            appearance.Rect.localScale = Vector3.Scale(appearance.IntroBaselineScale, new Vector3(0.94f, 0.72f, 1f));
            incomingCommunicationTween.Join(appearance.Rect.DOScale(appearance.IntroBaselineScale,
                incomingCommunicationDuration).SetEase(Ease.OutCubic));
        }
        incomingCommunicationTween.OnComplete(() =>
        {
            incomingCommunicationTween = null;
            RestoreIncomingPanelScales();
        });
    }

    private void CancelIncomingCommunicationIntro()
    {
        incomingCommunicationTween?.Kill(false);
        incomingCommunicationTween = null;
        incomingConversationId = null;
        RestoreIncomingPanelScales();
    }

    private void RestoreIncomingPanelScales()
    {
        foreach (SubtitlePanelAppearance appearance in panelAppearances)
        {
            if (!appearance.HasIntroBaseline) continue;
            if (appearance.Rect != null) appearance.Rect.localScale = appearance.IntroBaselineScale;
            appearance.HasIntroBaseline = false;
        }
    }

    private void AcquireHudPresentation(DialoguePresentationMode mode)
    {
        if (mode != DialoguePresentationMode.CinematicCommunication)
        {
            return;
        }

        ownedExpeditionHud = FindFirstObjectByType<ExpeditionHUD>(
            FindObjectsInactive.Include);
        if (ownedExpeditionHud == null)
        {
            return;
        }

        ownedExpeditionHud.SetCinematicMode(this, true);
        ownsHudCinematicMode = true;
    }

    private void ReleaseHudPresentation(bool immediate)
    {
        if (!ownsHudCinematicMode)
        {
            ownedExpeditionHud = null;
            return;
        }

        if (ownedExpeditionHud != null)
        {
            ownedExpeditionHud.ReleaseCinematicMode(this);

            if (immediate)
            {
                ownedExpeditionHud.CompleteCinematicVisibilityTransition();
            }
        }

        ownsHudCinematicMode = false;
        ownedExpeditionHud = null;
    }

    private void EnsurePresentationSurface()
    {
        Transform existing = transform.Find(PresentationRootName);
        if (existing != null)
        {
            overlayRoot = existing.gameObject;
            overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
            Transform topBar = existing.Find("Cinematic Top Bar");
            Transform accentLine = existing.Find("Communication Accent Line");
            topBarImage = topBar != null ? topBar.GetComponent<Image>() : null;
            topBarRect = topBarImage != null ? topBarImage.rectTransform : null;
            accentLineImage = accentLine != null
                ? accentLine.GetComponent<Image>()
                : null;
            SuppressLegacyFullScreenDim();
            return;
        }

        overlayRoot = CreateImageObject(PresentationRootName, transform, Color.black);
        overlayRoot.transform.SetAsFirstSibling();
        overlayCanvasGroup = overlayRoot.AddComponent<CanvasGroup>();
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;

        topBarImage = CreateImageObject(
            "Cinematic Top Bar",
            overlayRoot.transform,
            Color.black).GetComponent<Image>();
        topBarRect = topBarImage.rectTransform;
        topBarRect.anchorMin = new Vector2(0f, 1f);
        topBarRect.anchorMax = Vector2.one;
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.anchoredPosition = Vector2.zero;
        topBarRect.sizeDelta = new Vector2(0f, cinematicTopBarHeight);

        accentLineImage = CreateImageObject(
            "Communication Accent Line",
            overlayRoot.transform,
            neutralAccent).GetComponent<Image>();
        RectTransform accentRect = accentLineImage.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 0f);
        accentRect.pivot = new Vector2(0.5f, 0f);
        accentRect.anchoredPosition = new Vector2(0f, cinematicPanelHeight + 8f);
        accentRect.sizeDelta = new Vector2(-16f, 2f);

        SuppressLegacyFullScreenDim();
    }

    private void SuppressLegacyFullScreenDim()
    {
        Image mainPanelImage = standardDialogueUi != null &&
                               standardDialogueUi.conversationUIElements.mainPanel != null
            ? standardDialogueUi.conversationUIElements.mainPanel.GetComponent<Image>()
            : null;
        if (mainPanelImage != null)
        {
            Color color = mainPanelImage.color;
            color.a = 0f;
            mainPanelImage.color = color;
        }
    }

    private void ConfigurePixelCrushersPanelTiming()
    {
        if (standardDialogueUi == null)
        {
            return;
        }

        float safeDuration = Mathf.Max(0.01f, panelTransitionDuration);
        float animatorSpeed = Mathf.Max(0.01f, sourcePanelAnimationDuration) /
                              safeDuration;
        PixelCrushers.UIPanel mainPanel =
            standardDialogueUi.conversationUIElements.mainPanel;
        ConfigureAnimator(mainPanel != null ? mainPanel.GetComponent<Animator>() : null, animatorSpeed);

        StandardUISubtitlePanel[] subtitlePanels =
            standardDialogueUi.conversationUIElements.subtitlePanels;
        if (subtitlePanels != null)
        {
            for (int index = 0; index < subtitlePanels.Length; index++)
            {
                ConfigureAnimator(
                    subtitlePanels[index] != null
                        ? subtitlePanels[index].GetComponent<Animator>()
                        : null,
                    animatorSpeed);
            }
        }

        StandardUIMenuPanel[] menuPanels =
            standardDialogueUi.conversationUIElements.menuPanels;
        if (menuPanels == null)
        {
            return;
        }

        for (int index = 0; index < menuPanels.Length; index++)
        {
            ConfigureAnimator(
                menuPanels[index] != null
                    ? menuPanels[index].GetComponent<Animator>()
                    : null,
                animatorSpeed);
        }
    }

    private static void ConfigureAnimator(Animator animator, float speed)
    {
        if (animator == null)
        {
            return;
        }

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = speed;
    }

    private static GameObject CreateImageObject(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject created = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        created.layer = parent.gameObject.layer;
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = created.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return created;
    }

    private void CacheSubtitlePanels()
    {
        panelAppearances.Clear();
        if (standardDialogueUi == null ||
            standardDialogueUi.conversationUIElements.subtitlePanels == null)
        {
            return;
        }

        StandardUISubtitlePanel[] panels =
            standardDialogueUi.conversationUIElements.subtitlePanels;
        for (int index = 0; index < panels.Length; index++)
        {
            StandardUISubtitlePanel panel = panels[index];
            if (panel == null)
            {
                continue;
            }

            TMP_Text speakerName = panel.portraitName != null
                ? panel.portraitName.textMeshProUGUI
                : null;
            Transform speakerPlateTransform = speakerName != null
                ? speakerName.transform.parent
                : null;
            Button continueButton = panel.continueButton;
            panelAppearances.Add(new SubtitlePanelAppearance
            {
                Panel = panel,
                Rect = panel.panel != null ? panel.panel : panel.transform as RectTransform,
                Background = panel.GetComponent<Image>(),
                SpeakerPlate = speakerPlateTransform != null
                    ? speakerPlateTransform.GetComponent<Image>()
                    : null,
                SpeakerName = speakerName,
                ContinueBackground = continueButton != null
                    ? continueButton.GetComponent<Image>()
                    : null,
                ContinueLabel = continueButton != null
                    ? continueButton.GetComponentInChildren<TMP_Text>(true)
                    : null,
                PortraitLayout = panel.portraitImage != null
                    ? panel.portraitImage.GetComponent<LayoutElement>()
                    : null,
                SpeakerPlateLayout = speakerPlateTransform != null
                    ? speakerPlateTransform.GetComponent<LayoutElement>()
                    : null,
                Fitter = panel.GetComponent<ContentSizeFitter>(),
                Layout = panel.GetComponent<HorizontalLayoutGroup>()
            });
        }
    }

    private void ConfigurePanelLayout(DialoguePresentationMode mode)
    {
        float height = mode == DialoguePresentationMode.CompactGuidance
            ? compactPanelHeight
            : cinematicPanelHeight;

        for (int index = 0; index < panelAppearances.Count; index++)
        {
            SubtitlePanelAppearance appearance = panelAppearances[index];
            if (appearance.Rect != null)
            {
                appearance.Rect.anchorMin = new Vector2(0f, 0f);
                appearance.Rect.anchorMax = new Vector2(1f, 0f);
                appearance.Rect.pivot = new Vector2(0.5f, 0f);
                appearance.Rect.anchoredPosition = new Vector2(0f, 8f);
                appearance.Rect.sizeDelta = new Vector2(-16f, height);
            }

            if (appearance.Fitter != null)
            {
                appearance.Fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                appearance.Fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (appearance.Layout != null)
            {
                appearance.Layout.padding.left = 8;
                appearance.Layout.padding.right = 8;
                appearance.Layout.padding.top = 20;
                appearance.Layout.padding.bottom = 8;
                appearance.Layout.spacing = 4f;
                appearance.Layout.childControlWidth = true;
                appearance.Layout.childControlHeight = true;
                appearance.Layout.childForceExpandWidth = false;
                appearance.Layout.childForceExpandHeight = false;
            }

            if (appearance.PortraitLayout != null)
            {
                appearance.PortraitLayout.minWidth = portraitAreaWidth;
                appearance.PortraitLayout.preferredWidth = portraitAreaWidth;
                appearance.PortraitLayout.minHeight = portraitAreaWidth;
                appearance.PortraitLayout.preferredHeight = portraitAreaWidth;
            }

            if (appearance.SpeakerPlateLayout != null)
            {
                appearance.SpeakerPlateLayout.ignoreLayout = true;
                RectTransform speakerRect =
                    appearance.SpeakerPlateLayout.transform as RectTransform;
                if (speakerRect != null)
                {
                    speakerRect.anchorMin = new Vector2(0f, 1f);
                    speakerRect.anchorMax = new Vector2(0f, 1f);
                    speakerRect.pivot = new Vector2(0f, 1f);
                    speakerRect.anchoredPosition = new Vector2(
                        portraitAreaWidth + 14f,
                        -3f);
                    speakerRect.sizeDelta = new Vector2(112f, 15f);
                }
            }

            if (appearance.ContinueLabel != null)
            {
                appearance.ContinueLabel.text = ContinueIndicator;
                appearance.ContinueLabel.textWrappingMode = TextWrappingModes.NoWrap;
                appearance.ContinueLabel.fontSize = 9f;
            }

            LayoutElement continueLayout = appearance.Panel.continueButton != null
                ? appearance.Panel.continueButton.GetComponent<LayoutElement>()
                : null;
            if (continueLayout != null)
            {
                continueLayout.minWidth = 18f;
                continueLayout.preferredWidth = 18f;
                continueLayout.minHeight = 16f;
                continueLayout.preferredHeight = 16f;
            }
        }

        if (topBarRect != null)
        {
            topBarRect.sizeDelta = new Vector2(0f, cinematicTopBarHeight);
        }
    }

    private void ApplyActorTheme(DialogueActorTheme theme)
    {
        CurrentActorTheme = theme;
        Color accent = GetAccent(theme);
        Color themedBackground = Color.Lerp(panelBackground, accent, 0.08f);

        for (int index = 0; index < panelAppearances.Count; index++)
        {
            SubtitlePanelAppearance appearance = panelAppearances[index];
            if (appearance.Background != null)
            {
                appearance.Background.color = themedBackground;
            }

            if (appearance.SpeakerPlate != null)
            {
                appearance.SpeakerPlate.color = Color.Lerp(panelBackground, accent, 0.18f);
            }

            if (appearance.SpeakerName != null)
            {
                appearance.SpeakerName.color = accent;
            }

            if (appearance.ContinueBackground != null)
            {
                appearance.ContinueBackground.color =
                    Color.Lerp(panelBackground, accent, 0.16f);
            }

            if (appearance.ContinueLabel != null)
            {
                appearance.ContinueLabel.color = accent;
            }
        }

        if (accentLineImage != null)
        {
            accentLineImage.color = new Color(accent.r, accent.g, accent.b, 0.88f);
        }
    }

    private Color GetAccent(DialogueActorTheme theme)
    {
        return theme switch
        {
            DialogueActorTheme.Operator => operatorAccent,
            DialogueActorTheme.Curse => curseAccent,
            DialogueActorTheme.Settlement => settlementAccent,
            _ => neutralAccent
        };
    }

    private void BeginOverlayTransition(
        float targetAlpha,
        bool showTopBar,
        bool immediate)
    {
        if (overlayCanvasGroup == null || overlayRoot == null)
        {
            RestorePreviousSelection();
            return;
        }

        StopOverlayTransition();
        float safeTarget = Mathf.Clamp01(targetAlpha);
        bool shouldShowSurface = safeTarget > 0.0001f;
        overlayRoot.SetActive(true);
        topBarImage?.gameObject.SetActive(showTopBar);
        accentLineImage?.gameObject.SetActive(shouldShowSurface);

        if (immediate || overlayTransitionDuration <= 0.0001f)
        {
            overlayCanvasGroup.alpha = safeTarget;
            if (!shouldShowSurface)
            {
                overlayRoot.SetActive(false);
                RestorePreviousSelection();
            }

            return;
        }

        overlayRoutine = StartCoroutine(FadeOverlayRoutine(
            overlayCanvasGroup.alpha,
            safeTarget,
            shouldShowSurface));
    }

    private IEnumerator FadeOverlayRoutine(
        float startAlpha,
        float targetAlpha,
        bool keepSurfaceActive)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, overlayTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, ratio);
            yield return null;
        }

        overlayCanvasGroup.alpha = targetAlpha;
        overlayRoutine = null;
        if (!keepSurfaceActive)
        {
            overlayRoot.SetActive(false);
            RestorePreviousSelection();
        }
    }

    private void StopOverlayTransition()
    {
        if (overlayRoutine == null)
        {
            return;
        }

        StopCoroutine(overlayRoutine);
        overlayRoutine = null;
    }

    private void StartContinueIndicatorPulse()
    {
        StopContinueIndicatorPulse();
        continuePulseRoutine = StartCoroutine(ContinueIndicatorPulseRoutine());
    }

    private IEnumerator ContinueIndicatorPulseRoutine()
    {
        float elapsed = 0f;

        while (lifecycleState.IsActive)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0.45f, 1f, (Mathf.Sin(elapsed * 6f) + 1f) * 0.5f);

            for (int index = 0; index < panelAppearances.Count; index++)
            {
                TMP_Text label = panelAppearances[index].ContinueLabel;
                if (label == null)
                {
                    continue;
                }

                Color color = label.color;
                color.a = alpha;
                label.color = color;
            }

            yield return null;
        }

        continuePulseRoutine = null;
    }

    private void StopContinueIndicatorPulse()
    {
        if (continuePulseRoutine != null)
        {
            StopCoroutine(continuePulseRoutine);
            continuePulseRoutine = null;
        }

        for (int index = 0; index < panelAppearances.Count; index++)
        {
            TMP_Text label = panelAppearances[index].ContinueLabel;
            if (label == null)
            {
                continue;
            }

            Color color = label.color;
            color.a = 1f;
            label.color = color;
        }
    }

    private void StartCurseEntranceGlitch()
    {
        StopCurseEntranceGlitch();
        if (accentLineImage != null)
        {
            curseGlitchRoutine = StartCoroutine(CurseEntranceGlitchRoutine());
        }
    }

    private IEnumerator CurseEntranceGlitchRoutine()
    {
        const float duration = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration && lifecycleState.IsActive)
        {
            elapsed += Time.unscaledDeltaTime;
            Color color = accentLineImage.color;
            color.a = Mathf.Repeat(elapsed, 0.04f) < 0.02f ? 0.22f : 0.95f;
            accentLineImage.color = color;
            yield return null;
        }

        if (accentLineImage != null)
        {
            Color color = accentLineImage.color;
            color.a = 0.88f;
            accentLineImage.color = color;
        }

        curseGlitchRoutine = null;
    }

    private void StopCurseEntranceGlitch()
    {
        if (curseGlitchRoutine != null)
        {
            StopCoroutine(curseGlitchRoutine);
            curseGlitchRoutine = null;
        }
    }

    private void RestorePreviousSelection()
    {
        GameObject previous = selectionBeforeConversation;
        selectionBeforeConversation = null;
        if (previous == null ||
            !previous.activeInHierarchy ||
            EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(previous);
    }

    private void ResetPresentationImmediate()
    {
        StopOverlayTransition();
        StopContinueIndicatorPulse();
        StopCurseEntranceGlitch();
        lifecycleState.TryEnd();
        CurrentMode = DialoguePresentationMode.CompactGuidance;
        CurrentActorTheme = DialogueActorTheme.Neutral;
        selectionBeforeConversation = null;

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }
}
