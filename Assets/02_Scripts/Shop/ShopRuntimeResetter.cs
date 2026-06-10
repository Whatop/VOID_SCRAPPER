using UnityEngine;

public class ShopRuntimeResetter : MonoBehaviour
{
    [SerializeField] private bool syncOnAwake = true;

    private void Awake()
    {
        if (syncOnAwake)
        {
            ShopStructure.SyncGlobalHostilityFromRun();
        }
    }

    public void ResetShopRuntime()
    {
        ShopStructure.ResetGlobalHostility(false);
    }

    public void SyncShopRuntimeFromRun()
    {
        ShopStructure.SyncGlobalHostilityFromRun();
    }
}