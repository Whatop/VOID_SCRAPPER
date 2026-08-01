using UnityEngine;

[DisallowMultipleComponent]
public class BossPassiveRuntimeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private ComponentShieldPassive componentShield;

    [Header("Phase Afterimage Visual")]
    [SerializeField] private Color phaseAfterimageColor = new Color(0.25f, 0.9f, 1f, 0.55f);

    private RunManager subscribedRunManager;
    private int sectorBarrierLevel;
    private int matterReconstructorLevel;
    private int phaseAfterimageLevel;
    private int lastCargoLoad;
    private float nextPhaseAfterimageTime;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeRuntimeEvents();
        lastCargoLoad = ResolveCurrentCargoLoad();
    }

    private void Update()
    {
        if (subscribedRunManager != RunManager.Instance)
        {
            SubscribeRuntimeEvents();
        }
    }

    private void OnDisable()
    {
        UnsubscribeRuntimeEvents();
    }

    public void ConfigureSectorBarrier(int level)
    {
        sectorBarrierLevel = Mathf.Clamp(level, 0, 3);
        CacheReferences();

        if (sectorBarrierLevel <= 0)
        {
            if (componentShield != null)
            {
                componentShield.enabled = false;
            }

            return;
        }

        if (componentShield == null)
        {
            componentShield = gameObject.AddComponent<ComponentShieldPassive>();
        }

        componentShield.enabled = true;

        float rechargeSeconds = sectorBarrierLevel switch
        {
            1 => 18f,
            2 => 15f,
            _ => 12f
        };

        componentShield.ConfigureRuntime(
            rechargeSeconds,
            2.5f,
            true
        );
    }

    public void ConfigureMatterReconstructor(int level)
    {
        matterReconstructorLevel = Mathf.Clamp(level, 0, 3);
        CacheReferences();
        SubscribeRuntimeEvents();
        lastCargoLoad = ResolveCurrentCargoLoad();

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        int maxStacks = ResolveMatterMaximumArmorStacks();
        RunManager.Instance.CurrentRun.ClampMatterReconstructorState(maxStacks);

        if (matterReconstructorLevel <= 0 || playerArmor == null)
        {
            return;
        }

        int savedStacks = RunManager.Instance.CurrentRun.MatterReconstructorArmorStacks;
        playerArmor.SetMaxArmor(Mathf.Max(playerArmor.MaxArmor, maxStacks), false);

        if (savedStacks > playerArmor.CurrentArmor)
        {
            playerArmor.SetArmor(savedStacks);
        }
    }

    public void ConfigurePhaseAfterimage(int level)
    {
        phaseAfterimageLevel = Mathf.Clamp(level, 0, 3);
        CacheReferences();
        SubscribeRuntimeEvents();
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        int currentCargoLoad = ResolveCurrentCargoLoad();
        int gainedCargo = Mathf.Max(0, currentCargoLoad - lastCargoLoad);
        lastCargoLoad = currentCargoLoad;

        if (matterReconstructorLevel <= 0 || gainedCargo <= 0 ||
            RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        int grantedStacks = RunManager.Instance.CurrentRun.AddMatterReconstructorCargoProgress(
            gainedCargo,
            ResolveMatterCargoThreshold(),
            ResolveMatterMaximumArmorStacks()
        );

        if (grantedStacks <= 0 || playerArmor == null)
        {
            return;
        }

        int maxStacks = ResolveMatterMaximumArmorStacks();
        playerArmor.SetMaxArmor(Mathf.Max(playerArmor.MaxArmor, maxStacks), false);
        playerArmor.AddArmor(grantedStacks);

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null)
        {
            hud.ShowWarning($"물질 재구성로 · 임시 Armor +{grantedStacks}");
        }
    }

    private void HandleDashStarted(Vector2 dashDirection)
    {
        if (phaseAfterimageLevel <= 0 || Time.time < nextPhaseAfterimageTime)
        {
            return;
        }

        nextPhaseAfterimageTime = Time.time + ResolvePhaseCooldown();

        GameObject decoyObject = new GameObject("PhaseAfterimageDecoy");
        decoyObject.transform.position = transform.position;
        decoyObject.transform.rotation = transform.rotation;
        decoyObject.transform.localScale = transform.lossyScale;

        PhaseAfterimageDecoy decoy = decoyObject.AddComponent<PhaseAfterimageDecoy>();
        decoy.Initialize(
            transform,
            FindBestSourceRenderer(),
            phaseAfterimageColor,
            ResolvePhaseDuration(),
            ResolvePhaseTauntRadius()
        );
    }

    private SpriteRenderer FindBestSourceRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        float bestArea = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];

            if (candidate == null || candidate.sprite == null)
            {
                continue;
            }

            Vector2 size = candidate.sprite.bounds.size;
            float area = size.x * size.y;

            if (area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

    private int ResolveMatterCargoThreshold()
    {
        return matterReconstructorLevel switch
        {
            1 => 12,
            2 => 10,
            3 => 8,
            _ => int.MaxValue
        };
    }

    private int ResolveMatterMaximumArmorStacks()
    {
        return matterReconstructorLevel switch
        {
            1 => 3,
            2 => 4,
            3 => 5,
            _ => 0
        };
    }

    private float ResolvePhaseDuration()
    {
        return phaseAfterimageLevel switch
        {
            1 => 1.5f,
            2 => 2f,
            3 => 2.5f,
            _ => 0f
        };
    }

    private float ResolvePhaseCooldown()
    {
        return phaseAfterimageLevel switch
        {
            1 => 8f,
            2 => 7f,
            3 => 6f,
            _ => 999f
        };
    }

    private float ResolvePhaseTauntRadius()
    {
        return phaseAfterimageLevel switch
        {
            1 => 7f,
            2 => 8f,
            3 => 9f,
            _ => 0f
        };
    }

    private int ResolveCurrentCargoLoad()
    {
        return RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.CurrentCargoLoad
            : 0;
    }

    private void CacheReferences()
    {
        if (playerArmor == null)
        {
            playerArmor = GetComponent<PlayerArmor>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (componentShield == null)
        {
            componentShield = GetComponent<ComponentShieldPassive>();
        }
    }

    private void SubscribeRuntimeEvents()
    {
        if (subscribedRunManager != RunManager.Instance)
        {
            if (subscribedRunManager != null)
            {
                subscribedRunManager.WalletChanged -= HandleWalletChanged;
            }

            subscribedRunManager = RunManager.Instance;

            if (subscribedRunManager != null)
            {
                subscribedRunManager.WalletChanged += HandleWalletChanged;
            }
        }

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
            playerDash.DashStarted += HandleDashStarted;
        }
    }

    private void UnsubscribeRuntimeEvents()
    {
        if (subscribedRunManager != null)
        {
            subscribedRunManager.WalletChanged -= HandleWalletChanged;
            subscribedRunManager = null;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
        }
    }
}
