using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueLifecycleStabilityTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private readonly LocalizationEditModeLifecycle localizationLifecycle =
        new LocalizationEditModeLifecycle();
    private VoidScrapperLocalizationService previousLocalizationService;

    [SetUp]
    public void SetUp()
    {
        previousLocalizationService =
            LocalizationServiceTestIsolation.DetachActiveInstance();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            localizationLifecycle.ShutdownAll();
        }
        finally
        {
            try
            {
                for (int i = createdObjects.Count - 1; i >= 0; i--)
                {
                    if (createdObjects[i] != null)
                    {
                        Object.DestroyImmediate(createdObjects[i]);
                    }
                }
            }
            finally
            {
                createdObjects.Clear();
                LocalizationServiceTestIsolation.RestoreActiveInstance(
                    previousLocalizationService);
                previousLocalizationService = null;
            }
        }
    }

    [Test]
    public void BootstrapOwnerResolution_IsIdempotent()
    {
        Assert.That(
            GameBootstrap.ShouldPruneIncomingDialogueManagerForEditorAndTests(
                false,
                true),
            Is.False);
        Assert.That(
            GameBootstrap.ShouldPruneIncomingDialogueManagerForEditorAndTests(
                true,
                false),
            Is.False);
        Assert.That(
            GameBootstrap.ShouldPruneIncomingDialogueManagerForEditorAndTests(
                true,
                true),
            Is.True);
    }

    [Test]
    public void DefensiveLocalizationGuard_RemovesDuplicateRoot()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);
        Assert.That(catalog, Is.Not.Null);

        VoidScrapperLocalizationService canonical =
            VoidScrapperLocalizationService.Instance;
        if (canonical == null)
        {
            GameObject canonicalRoot = CreateInactiveGameObject("CanonicalDialogueManager");
            canonical = canonicalRoot.AddComponent<VoidScrapperLocalizationService>();
            canonical.ConfigureForEditorAndTests(catalog);
            canonicalRoot.SetActive(true);
            Assert.That(localizationLifecycle.InitializeService(canonical), Is.True);
        }

        Assert.That(VoidScrapperLocalizationService.Instance, Is.SameAs(canonical));
        Assert.That(canonical.gameObject.activeSelf, Is.True);

        GameObject duplicateRoot = CreateInactiveGameObject("DuplicateDialogueManager");
        VoidScrapperLocalizationService duplicate =
            duplicateRoot.AddComponent<VoidScrapperLocalizationService>();
        duplicate.ConfigureForEditorAndTests(catalog);

        LogAssert.Expect(
            LogType.Error,
            new Regex("second localization service.*duplicate Dialogue Manager root"));
        duplicateRoot.SetActive(true);
        Assert.That(localizationLifecycle.InitializeService(duplicate), Is.False);

        Assert.That(VoidScrapperLocalizationService.Instance, Is.SameAs(canonical));
        Assert.That(canonical.gameObject.activeSelf, Is.True);
        Assert.That(duplicateRoot == null || !duplicateRoot.activeSelf, Is.True);
    }

    [Test]
    public void ActiveConversation_BlocksAuthoritativeLaunchGate()
    {
        bool allowed = SettlementExpeditionLaunchGuard.TryPassDialogueGate(
            true,
            out SettlementExpeditionLaunchFailure failure);

        Assert.That(allowed, Is.False);
        Assert.That(failure, Is.EqualTo(SettlementExpeditionLaunchFailure.DialogueActive));
    }

    [Test]
    public void InactiveConversation_AllowsExistingLaunchRequirementsToContinue()
    {
        bool allowed = SettlementExpeditionLaunchGuard.TryPassDialogueGate(
            false,
            out SettlementExpeditionLaunchFailure failure);

        Assert.That(allowed, Is.True);
        Assert.That(failure, Is.EqualTo(SettlementExpeditionLaunchFailure.None));
    }

    [Test]
    public void SettlementInputState_FollowsConversationEventsWithoutPolling()
    {
        GameObject inputRoot = CreateInactiveGameObject("SettlementInputTest");
        CanvasGroup canvasGroup = inputRoot.AddComponent<CanvasGroup>();
        SettlementUIController controller =
            inputRoot.AddComponent<SettlementUIController>();
        controller.ConfigureDialogueModalForEditorAndTests(canvasGroup);
        inputRoot.SetActive(true);

        controller.SetDialogueModalActiveForEditorAndTests(true);
        Assert.That(canvasGroup.interactable, Is.False);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);

        controller.SetDialogueModalActiveForEditorAndTests(false);
        Assert.That(canvasGroup.interactable, Is.True);
        Assert.That(canvasGroup.blocksRaycasts, Is.True);
    }

    [Test]
    public void SettlementDialogueBlockMessage_HasKoreanSource()
    {
        LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
            LocalizationContentImporter.DefaultCatalogAssetPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(
            catalog.TryGetEntry(
                SettlementExpeditionLaunchGuard.DialogueActiveTextKey,
                out LocalizationEntry entry),
            Is.True);
        Assert.That(entry.Korean, Is.Not.Null.And.Not.Empty);
    }

    private GameObject CreateInactiveGameObject(string objectName)
    {
        GameObject gameObject = new GameObject(objectName);
        gameObject.SetActive(false);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}

/// <summary>
/// EditMode does not run ordinary MonoBehaviour lifecycle messages when a test
/// toggles an object active. This fixture invokes the same private callback
/// order used by Play Mode and tracks every invocation so cleanup cannot mix
/// manual and automatic lifecycle ownership.
/// </summary>
internal sealed class LocalizationEditModeLifecycle
{
    private const BindingFlags LifecycleFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly MethodInfo ServiceAwake = ResolveLifecycleMethod(
        typeof(VoidScrapperLocalizationService),
        "Awake");
    private static readonly MethodInfo ServiceOnEnable = ResolveLifecycleMethod(
        typeof(VoidScrapperLocalizationService),
        "OnEnable");
    private static readonly MethodInfo ServiceOnDisable = ResolveLifecycleMethod(
        typeof(VoidScrapperLocalizationService),
        "OnDisable");
    private static readonly MethodInfo ServiceOnDestroy = ResolveLifecycleMethod(
        typeof(VoidScrapperLocalizationService),
        "OnDestroy");
    private static readonly MethodInfo PresenterOnEnable = ResolveLifecycleMethod(
        typeof(LocalizedTextPresenter),
        "OnEnable");
    private static readonly MethodInfo PresenterOnDisable = ResolveLifecycleMethod(
        typeof(LocalizedTextPresenter),
        "OnDisable");

    private readonly List<VoidScrapperLocalizationService> initializedServices =
        new List<VoidScrapperLocalizationService>();
    private readonly List<LocalizedTextPresenter> initializedPresenters =
        new List<LocalizedTextPresenter>();

    public bool InitializeService(VoidScrapperLocalizationService service)
    {
        RequireEditMode();

        if (service == null)
        {
            throw new System.ArgumentNullException(nameof(service));
        }

        if (!service.gameObject.activeInHierarchy)
        {
            throw new System.InvalidOperationException(
                "The service root must be active before its lifecycle is initialized.");
        }

        if (initializedServices.Contains(service) ||
            service.didAwake ||
            object.ReferenceEquals(VoidScrapperLocalizationService.Instance, service))
        {
            throw new System.InvalidOperationException(
                "The localization service lifecycle was already initialized.");
        }

        Invoke(ServiceAwake, service);

        // Duplicate Awake destroys its root synchronously in EditMode. It must
        // never receive OnEnable or enter this fixture's teardown ownership.
        if (service == null || !service.gameObject.activeInHierarchy)
        {
            return false;
        }

        initializedServices.Add(service);
        Invoke(ServiceOnEnable, service);
        return true;
    }

    public void InitializePresenter(LocalizedTextPresenter presenter)
    {
        RequireEditMode();

        if (presenter == null)
        {
            throw new System.ArgumentNullException(nameof(presenter));
        }

        if (!presenter.gameObject.activeInHierarchy || !presenter.enabled)
        {
            throw new System.InvalidOperationException(
                "The presenter must be active and enabled before initialization.");
        }

        if (initializedPresenters.Contains(presenter))
        {
            throw new System.InvalidOperationException(
                "The localized presenter lifecycle was already initialized.");
        }

        initializedPresenters.Add(presenter);
        Invoke(PresenterOnEnable, presenter);
    }

    public void DisablePresenter(LocalizedTextPresenter presenter)
    {
        RequireEditMode();

        if (presenter == null || !initializedPresenters.Remove(presenter))
        {
            return;
        }

        Invoke(PresenterOnDisable, presenter);
    }

    public void ShutdownAll()
    {
        RequireEditMode();

        for (int i = initializedPresenters.Count - 1; i >= 0; i--)
        {
            LocalizedTextPresenter presenter = initializedPresenters[i];
            if (presenter != null)
            {
                Invoke(PresenterOnDisable, presenter);
            }
        }

        initializedPresenters.Clear();

        for (int i = initializedServices.Count - 1; i >= 0; i--)
        {
            VoidScrapperLocalizationService service = initializedServices[i];
            if (service != null)
            {
                Invoke(ServiceOnDisable, service);
                Invoke(ServiceOnDestroy, service);
            }
        }

        initializedServices.Clear();
    }

    private static MethodInfo ResolveLifecycleMethod(
        System.Type componentType,
        string methodName)
    {
        MethodInfo method = componentType.GetMethod(methodName, LifecycleFlags);
        if (method == null)
        {
            throw new System.MissingMethodException(componentType.Name, methodName);
        }

        return method;
    }

    private static void Invoke(MethodInfo method, MonoBehaviour target)
    {
        method.Invoke(target, null);
    }

    private static void RequireEditMode()
    {
        if (Application.isPlaying)
        {
            throw new System.InvalidOperationException(
                "The manual lifecycle fixture must only run in EditMode tests.");
        }
    }
}

internal static class LocalizationServiceTestIsolation
{
    private static readonly FieldInfo ActiveInstanceField =
        typeof(VoidScrapperLocalizationService).GetField(
            "activeInstance",
            BindingFlags.Static | BindingFlags.NonPublic);

    public static VoidScrapperLocalizationService DetachActiveInstance()
    {
        if (ActiveInstanceField == null)
        {
            throw new System.MissingFieldException(
                nameof(VoidScrapperLocalizationService),
                "activeInstance");
        }

        VoidScrapperLocalizationService previous =
            ActiveInstanceField.GetValue(null) as VoidScrapperLocalizationService;
        ActiveInstanceField.SetValue(null, null);
        return previous;
    }

    public static void RestoreActiveInstance(
        VoidScrapperLocalizationService previous)
    {
        if (ActiveInstanceField == null)
        {
            return;
        }

        ActiveInstanceField.SetValue(null, previous != null ? previous : null);
    }
}
