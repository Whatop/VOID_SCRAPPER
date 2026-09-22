using UnityEngine;

public partial class PermanentProgress
{
    [SerializeField] private OperatingFrameType selectedOperatingFrame = OperatingFrameType.Standard;

    public OperatingFrameType SelectedOperatingFrame => OperatingFrameProfile.Normalize(selectedOperatingFrame);

    public int GetEffectiveEquipmentCount(WeaponTreeType weapon)
    {
        int count = 0;
        if (equipmentCatalog == null) return count;
        foreach (string id in equipmentLoadoutTraitIds)
        {
            if (IsEquipmentUsable(equipmentCatalog.FindById(id), weapon)) count++;
        }
        return count;
    }

    public OperatingFrameProfile ProjectedOperatingFrame =>
        new OperatingFrameProfile(SelectedOperatingFrame, GetEffectiveEquipmentCount(lastSelectedWeaponTree));

    public bool TrySetOperatingFrame(OperatingFrameType frame)
    {
        if (!OperatingFrameProfile.IsValid(frame) || !CanEditEquipment) return false;
        if (SelectedOperatingFrame == frame) return true;
        equipmentTransactionInProgress = true;
        try
        {
            SaveData snapshot = CreateSaveData();
            snapshot.selectedOperatingFrame = frame;
            // Same save-first failure policy as manufacturing/fitting; no currency or ownership changes.
            if (!SaveEquipmentTransaction(snapshot)) return false;
            selectedOperatingFrame = frame;
        }
        finally
        {
            equipmentTransactionInProgress = false;
        }
        Changed?.Invoke();
        return true;
    }
}
