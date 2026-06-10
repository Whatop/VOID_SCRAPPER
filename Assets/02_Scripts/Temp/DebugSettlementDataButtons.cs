using UnityEngine;

[DisallowMultipleComponent]
public class DebugSettlementDataButtons : MonoBehaviour
{
    [Header("Resource Limit")]
    [SerializeField] private int maxScrapParts = 9999;
    [SerializeField] private int maxCoreShards = 999;

    [Header("UI Refresh Optional")]
    [SerializeField] private SettlementHUD settlementHUD;
    [SerializeField] private SettlementUIController settlementUIController;
    [SerializeField] private ShipTraitTreePanel shipTraitTreePanel;

    [Header("Debug")]
    [SerializeField] private bool logResult = true;

    /// <summary>
    /// 버튼 OnClick 연결용.
    /// 영구 데이터 초기화.
    /// 스크랩, 코어, 건물 레벨, 특성 레벨, 해금 플래그, 선택 기체/무기 초기화.
    /// </summary>
    public void ResetData()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            Debug.LogWarning("PermanentProgress.Instance가 없습니다. Boot/CoreRoot 세팅을 확인하세요.", this);
            return;
        }

        progress.ResetProgress();

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }
        else
        {
            Debug.LogWarning("SaveManager.Instance가 없어 저장 파일에는 반영하지 못했습니다.", this);
        }

        RefreshUI();

        if (logResult)
        {
            Debug.Log("디버그: 영구 데이터 초기화 완료", this);
        }
    }

    /// <summary>
    /// 버튼 OnClick 연결용.
    /// 스크랩, 코어 조각을 지정한 한계값까지 채움.
    /// </summary>
    public void FillResourcesToLimit()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            Debug.LogWarning("PermanentProgress.Instance가 없습니다. Boot/CoreRoot 세팅을 확인하세요.", this);
            return;
        }

        int targetScrap = Mathf.Max(0, maxScrapParts);
        int targetCore = Mathf.Max(0, maxCoreShards);

        int scrapToAdd = Mathf.Max(0, targetScrap - progress.ScrapParts);
        int coreToAdd = Mathf.Max(0, targetCore - progress.CoreShards);

        if (scrapToAdd > 0)
        {
            progress.AddPermanentCurrency(CurrencyType.ScrapParts, scrapToAdd);
        }

        if (coreToAdd > 0)
        {
            progress.AddPermanentCurrency(CurrencyType.CoreShards, coreToAdd);
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }
        else
        {
            Debug.LogWarning("SaveManager.Instance가 없어 저장 파일에는 반영하지 못했습니다.", this);
        }

        RefreshUI();

        if (logResult)
        {
            Debug.Log($"디버그: 자원 채우기 완료 / 스크랩 {progress.ScrapParts}, 코어 {progress.CoreShards}", this);
        }
    }

    private void RefreshUI()
    {
        if (settlementHUD == null)
        {
            settlementHUD = FindFirstObjectByType<SettlementHUD>();
        }

        if (settlementUIController == null)
        {
            settlementUIController = FindFirstObjectByType<SettlementUIController>();
        }

        if (shipTraitTreePanel == null)
        {
            shipTraitTreePanel = FindFirstObjectByType<ShipTraitTreePanel>();
        }

        if (settlementHUD != null)
        {
            settlementHUD.Refresh();
        }

        if (settlementUIController != null)
        {
            settlementUIController.Refresh();
        }

        if (shipTraitTreePanel != null)
        {
            shipTraitTreePanel.RefreshPanel();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Reset Data")]
    private void ContextResetData()
    {
        ResetData();
    }

    [ContextMenu("Debug/Fill Resources To Limit")]
    private void ContextFillResourcesToLimit()
    {
        FillResourcesToLimit();
    }
#endif
}