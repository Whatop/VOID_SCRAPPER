using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AutoUIButtonSoundInstaller : MonoBehaviour
{
    [SerializeField] private bool includeInactiveObjects = true;
    [SerializeField] private bool skipSettlementButtons = true;
    [SerializeField] private float scanDelayAfterSceneLoad = 0.1f;
    [SerializeField] private float periodicScanInterval = 1.5f;

    private Coroutine scanRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<AutoUIButtonSoundInstaller>() != null)
        {
            return;
        }

        GameObject root = new GameObject("AutoUIButtonSoundInstaller");
        DontDestroyOnLoad(root);
        root.AddComponent<AutoUIButtonSoundInstaller>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        scanRoutine = StartCoroutine(PeriodicScanRoutine());
        StartCoroutine(ScanAfterDelay(scanDelayAfterSceneLoad));
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ScanAfterDelay(scanDelayAfterSceneLoad));
    }

    private IEnumerator PeriodicScanRoutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, periodicScanInterval));
            InstallToAllButtons();
        }
    }

    private IEnumerator ScanAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        InstallToAllButtons();
    }

    private void InstallToAllButtons()
    {
        FindObjectsInactive inactiveMode = includeInactiveObjects
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        Button[] buttons = FindObjectsByType<Button>(inactiveMode, FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
            {
                continue;
            }

            if (IsTraitTreeButton(button))
            {
                continue;
            }

            if (skipSettlementButtons && IsSettlementButton(button))
            {
                continue;
            }

            if (button.GetComponent<UISoundButton>() == null)
            {
                button.gameObject.AddComponent<UISoundButton>();
            }
        }
    }

    private static bool IsTraitTreeButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        return button.GetComponent<ShipTraitBranchTabButton>() != null ||
               button.GetComponent<ShipTraitNodeButton>() != null;
    }

    private static bool IsSettlementButton(Button button)
    {
        Transform current = button != null ? button.transform : null;

        while (current != null)
        {
            if (current.GetComponent<SettlementUIController>() != null ||
                current.GetComponent<SettlementHUD>() != null ||
                current.GetComponent<SettlementSettingsPanel>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
