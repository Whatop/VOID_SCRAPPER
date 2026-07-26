using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController2D))]
public class PlayerMovementAudioController : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] private string movePuffEventId = SoundEventIds.ShipMovePuff;
    [SerializeField] private float interval = 0.50f;
    [SerializeField] private float minimumInputMagnitude = 0.1f;
    [SerializeField] private float volumeScale = 0.7f;

    [Header("References")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerHealth playerHealth;

    private float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapInstaller()
    {
        if (FindFirstObjectByType<PlayerMovementAudioRuntimeInstaller>() != null)
        {
            return;
        }

        GameObject root = new GameObject("PlayerMovementAudioInstaller");
        DontDestroyOnLoad(root);
        root.AddComponent<PlayerMovementAudioRuntimeInstaller>();
    }

    private void Reset()
    {
        playerController = GetComponent<PlayerController2D>();
        playerDash = GetComponent<PlayerDash>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            timer = 0f;
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            timer = 0f;
            return;
        }

        if (playerController == null || !playerController.ControlEnabled || playerController.MovementLocked)
        {
            timer = 0f;
            return;
        }

        if (playerDash != null && playerDash.IsDashing)
        {
            timer = 0f;
            return;
        }

        bool moving = playerController.MoveInput.sqrMagnitude >= minimumInputMagnitude * minimumInputMagnitude;

        if (!moving)
        {
            timer = 0f;
            return;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        AudioManager.Play(movePuffEventId, volumeScale);
        timer = Mathf.Max(0.05f, interval);
    }

    private void CacheReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }
    }
}

public class PlayerMovementAudioRuntimeInstaller : MonoBehaviour
{
    [SerializeField] private float scanInterval = 1f;

    private void Start()
    {
        StartCoroutine(InstallRoutine());
    }

    private IEnumerator InstallRoutine()
    {
        while (true)
        {
            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];

                if (player != null && player.GetComponent<PlayerMovementAudioController>() == null)
                {
                    player.gameObject.AddComponent<PlayerMovementAudioController>();
                }
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, scanInterval));
        }
    }
}
