using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SettlementFinalSupportController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerArmor playerArmor;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private EnemyHealth finalBossHealth;

    [Header("Sequence")]
    [SerializeField] private float supportStepInterval = 0.75f;
    [SerializeField] private bool skipUnavailableBuildingSupport = true;

    [Header("Hangar Support")]
    [SerializeField] private float hangarBaseHeal = 3f;
    [SerializeField] private float hangarHealPerLevel = 2f;
    [SerializeField] private UnityEvent hangarSupportTriggered;

    [Header("Engine Workshop Support")]
    [SerializeField] private float engineBuffDuration = 8f;
    [SerializeField] private float engineMoveSpeedPercentPerLevel = 6f;
    [SerializeField] private float engineDashCooldownReductionPerLevel = 0.03f;
    [SerializeField] private UnityEvent engineSupportTriggered;

    [Header("Weapon Lab Support")]
    [SerializeField] private float weaponLabBaseDamage = 8f;
    [SerializeField] private float weaponLabDamagePerLevel = 6f;
    [SerializeField] private UnityEvent weaponLabSupportTriggered;

    [Header("Recovery Processor Support")]
    [SerializeField] private float recoveryArmorPerLevel = 1f;
    [SerializeField] private UnityEvent recoverySupportTriggered;

    [Header("Complete")]
    [SerializeField] private UnityEvent supportSequenceCompleted;

    private Coroutine supportRoutine;
    private Coroutine engineBuffRoutine;
    private bool sequenceTriggered;
    private bool supportCancelled;
    private bool completionNotified;

    public bool SequenceTriggered => sequenceTriggered;
    public bool SequenceCompleted => completionNotified;
    public event System.Action Completed;

    private void Awake()
    {
        ResolvePlayerReferences();
    }

    public void ConfigureFinalBoss(EnemyHealth bossHealth)
    {
        finalBossHealth = bossHealth;
    }

    public bool TriggerFullSupport()
    {
        if (sequenceTriggered)
        {
            return false;
        }

        sequenceTriggered = true;
        supportCancelled = false;
        ResolvePlayerReferences();

        if (supportRoutine != null)
        {
            StopCoroutine(supportRoutine);
        }

        if (Application.isPlaying && isActiveAndEnabled)
        {
            Coroutine started = StartCoroutine(SupportRoutine());
            supportRoutine = completionNotified || supportCancelled ? null : started;
        }
        return true;
    }

    public void ResetSupportSequence()
    {
        CancelSupportSequence();
        sequenceTriggered = false;
        completionNotified = false;
    }

    public void CancelSupportSequence()
    {
        supportCancelled = true;
        if (supportRoutine != null)
        {
            StopCoroutine(supportRoutine);
            supportRoutine = null;
        }
        if (engineBuffRoutine != null)
        {
            StopCoroutine(engineBuffRoutine);
            engineBuffRoutine = null;
        }
        ClearEngineSupport();
    }

    private void OnDisable() => CancelSupportSequence();

    private void FinishSupportSequence()
    {
        if (!sequenceTriggered || supportCancelled || completionNotified) return;
        completionNotified = true;
        supportRoutine = null;
        supportSequenceCompleted?.Invoke();
        Completed?.Invoke();
    }

    private IEnumerator SupportRoutine()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            ShowMessage("정착지 지원 정보를 찾지 못했습니다.");
            FinishSupportSequence();
            yield break;
        }

        ShowMessage("완전 코어 연결 · 정착지 원격 지원 개시");

        int hangarLevel = progress.GetBuildingLevel(BuildingType.Hangar);
        if (ShouldRunSupport(hangarLevel))
        {
            ApplyHangarSupport(hangarLevel);
            if (supportCancelled) yield break;
            yield return WaitStep();
        }

        int engineLevel = progress.GetBuildingLevel(BuildingType.EngineWorkshop);
        if (ShouldRunSupport(engineLevel))
        {
            ApplyEngineSupport(engineLevel);
            if (supportCancelled) yield break;
            yield return WaitStep();
        }

        int weaponLabLevel = progress.GetBuildingLevel(BuildingType.WeaponLab);
        if (ShouldRunSupport(weaponLabLevel))
        {
            ApplyWeaponLabSupport(weaponLabLevel);
            if (supportCancelled) yield break;
            yield return WaitStep();
        }

        int recoveryLevel = progress.GetBuildingLevel(BuildingType.RecoveryProcessor);
        if (ShouldRunSupport(recoveryLevel))
        {
            ApplyRecoverySupport(recoveryLevel);
            if (supportCancelled) yield break;
            yield return WaitStep();
        }

        ShowMessage("정착지 지원 연결 완료 · 중앙 코어를 마무리하십시오.");
        FinishSupportSequence();
    }

    private bool ShouldRunSupport(int buildingLevel)
    {
        return buildingLevel > 0 || !skipUnavailableBuildingSupport;
    }

    private void ApplyHangarSupport(int level)
    {
        if (playerHealth != null)
        {
            float healAmount = Mathf.Max(0f, hangarBaseHeal + hangarHealPerLevel * Mathf.Max(0, level));
            playerHealth.Heal(healAmount);
            ShowMessage($"격납고 지원 · 수리 드론 HP +{healAmount:0.#}");
        }

        hangarSupportTriggered?.Invoke();
    }

    private void ApplyEngineSupport(int level)
    {
        if (engineBuffRoutine != null)
        {
            StopCoroutine(engineBuffRoutine);
        }

        engineBuffRoutine = StartCoroutine(EngineBuffRoutine(Mathf.Max(0, level)));
        ShowMessage("엔진 공방 지원 · 위상 추진 경로 활성화");
        engineSupportTriggered?.Invoke();
    }

    private IEnumerator EngineBuffRoutine(int level)
    {
        if (playerController == null || playerDash == null)
        {
            engineBuffRoutine = null;
            yield break;
        }

        float originalDashCooldown = playerDash.DashCooldown;
        float moveMultiplier = 1f + Mathf.Max(0f, engineMoveSpeedPercentPerLevel) * level * 0.01f;
        float cooldownReduction = Mathf.Max(0f, engineDashCooldownReductionPerLevel) * level;

        playerController.SetExternalMoveSpeedMultiplier(this, moveMultiplier);
        playerDash.SetExternalCooldownMultiplier(this,
            Mathf.Max(0.05f, originalDashCooldown - cooldownReduction) / Mathf.Max(0.05f, originalDashCooldown));

        yield return new WaitForSeconds(Mathf.Max(0.1f, engineBuffDuration));

        ClearEngineSupport();
        engineBuffRoutine = null;
    }

    private void ClearEngineSupport()
    {
        playerController?.ClearExternalMoveSpeedMultiplier(this);
        playerDash?.ClearExternalCooldownMultiplier(this);
    }

    private void ApplyWeaponLabSupport(int level)
    {
        float damage = Mathf.Max(0f, weaponLabBaseDamage + weaponLabDamagePerLevel * Mathf.Max(0, level));

        if (finalBossHealth != null && !finalBossHealth.IsDead)
        {
            finalBossHealth.TakeDamage(damage, finalBossHealth.transform.position, Vector2.zero);
            ShowMessage($"화기 연구소 지원 · 원격 포격 {damage:0.#} 피해");
        }
        else
        {
            ShowMessage("화기 연구소 지원 신호 수신 · 포격 대상이 없습니다.");
        }

        weaponLabSupportTriggered?.Invoke();
    }

    private void ApplyRecoverySupport(int level)
    {
        if (playerArmor != null)
        {
            float armorAmount = Mathf.Max(0f, recoveryArmorPerLevel * Mathf.Max(0, level));
            float requiredMaxArmor = Mathf.Max(playerArmor.MaxArmor, playerArmor.CurrentArmor + armorAmount);
            playerArmor.SetMaxArmor(requiredMaxArmor, false);
            playerArmor.AddArmor(armorAmount);
            ShowMessage($"회수 처리장 지원 · 재구성 Armor +{armorAmount:0.#}");
        }

        recoverySupportTriggered?.Invoke();
    }

    private YieldInstruction WaitStep()
    {
        return new WaitForSeconds(Mathf.Max(0f, supportStepInterval));
    }

    private void ResolvePlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInParent<PlayerHealth>();
        }

        if (playerArmor == null)
        {
            playerArmor = player.GetComponentInParent<PlayerArmor>();
        }

        if (playerController == null)
        {
            playerController = player.GetComponentInParent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = player.GetComponentInParent<PlayerDash>();
        }
    }

    private void ShowMessage(string message)
    {
        // Pure/editor validation of support effects must not activate an authored scene HUD.
        ExpeditionHUD hud = Application.isPlaying ? FindFirstObjectByType<ExpeditionHUD>() : null;

        if (hud != null)
        {
            hud.ShowWarning(message);
        }

        Debug.Log(message, this);
    }
}
