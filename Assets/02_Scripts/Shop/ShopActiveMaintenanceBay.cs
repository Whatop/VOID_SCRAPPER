using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopActiveMaintenanceBay : MonoBehaviour
{
    [Header("정비소 보관함")]
    [SerializeField] private int capacity = 6;
    [SerializeField] private bool preventDuplicateEquipment = true;

    private readonly List<ReinforcementDefinition> storedItems = new List<ReinforcementDefinition>();
    private readonly List<int> storedCharges = new List<int>();

    public int Capacity => Mathf.Max(1, capacity);

    public int Count
    {
        get
        {
            EnsureSlotCapacity();

            int count = 0;

            for (int i = 0; i < storedItems.Count; i++)
            {
                if (storedItems[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public IReadOnlyList<ReinforcementDefinition> Items
    {
        get
        {
            EnsureSlotCapacity();
            return storedItems;
        }
    }

    private void Awake()
    {
        EnsureSlotCapacity();
    }

    public void SetCapacity(int newCapacity)
    {
        capacity = Mathf.Max(1, newCapacity);
        EnsureSlotCapacity();
    }

    public ReinforcementDefinition GetAt(int index)
    {
        EnsureSlotCapacity();

        if (index < 0 || index >= storedItems.Count)
        {
            return null;
        }

        return storedItems[index];
    }

    public int GetChargesAt(int index)
    {
        EnsureSlotCapacity();

        if (index < 0 || index >= storedCharges.Count)
        {
            return -1;
        }

        return storedCharges[index];
    }

    public bool IsSlotEmpty(int index)
    {
        return GetAt(index) == null;
    }

    public bool HasSpace()
    {
        EnsureSlotCapacity();
        return GetFirstEmptyIndex() >= 0;
    }

    public bool Contains(string equipmentId)
    {
        return ContainsExcept(equipmentId, -1);
    }

    public bool CanStore(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (!HasSpace())
        {
            return false;
        }

        if (preventDuplicateEquipment && Contains(definition.EquipmentId))
        {
            return false;
        }

        return true;
    }

    public bool TryStore(ReinforcementDefinition definition)
    {
        return TryStore(definition, -1);
    }

    public bool TryStore(ReinforcementDefinition definition, int charges)
    {
        EnsureSlotCapacity();

        int index = GetFirstEmptyIndex();

        if (index < 0)
        {
            return false;
        }

        return TryStoreAt(index, definition, charges);
    }

    public bool TryStoreAt(int index, ReinforcementDefinition definition, int charges)
    {
        EnsureSlotCapacity();

        if (definition == null)
        {
            return false;
        }

        if (index < 0 || index >= storedItems.Count)
        {
            return false;
        }

        if (storedItems[index] != null)
        {
            return false;
        }

        if (preventDuplicateEquipment && Contains(definition.EquipmentId))
        {
            return false;
        }

        storedItems[index] = definition;
        storedCharges[index] = charges;
        return true;
    }

    public bool TryStoreCurrentEquipped(PlayerReinforcementController controller)
    {
        if (controller == null || !controller.HasEquipment)
        {
            return false;
        }

        return TryStore(controller.EquippedDefinition, controller.CurrentCharges);
    }

    public bool TryStoreCurrentInSlot(int index, PlayerReinforcementController controller)
    {
        EnsureSlotCapacity();

        if (controller == null || !controller.HasEquipment)
        {
            return false;
        }

        if (index < 0 || index >= storedItems.Count)
        {
            return false;
        }

        ReinforcementDefinition currentDefinition = controller.EquippedDefinition;
        int currentCharges = controller.CurrentCharges;

        if (currentDefinition == null)
        {
            return false;
        }

        ReinforcementDefinition targetStored = storedItems[index];
        int targetCharges = storedCharges[index];

        // 빈칸에 현재 액티브 넣기. 플레이어 액티브는 비워진다.
        if (targetStored == null)
        {
            if (preventDuplicateEquipment && ContainsExcept(currentDefinition.EquipmentId, index))
            {
                return false;
            }

            storedItems[index] = currentDefinition;
            storedCharges[index] = currentCharges;

            controller.ClearEquipment(true);
            return true;
        }

        // 찬 칸에 현재 액티브 드랍 = 현재 액티브와 해당 보관 슬롯 교체.
        if (targetStored.EquipmentId == currentDefinition.EquipmentId)
        {
            return false;
        }

        bool equipped = controller.EquipWithoutDropping(targetStored, targetCharges, true);

        if (!equipped)
        {
            return false;
        }

        storedItems[index] = currentDefinition;
        storedCharges[index] = currentCharges;
        return true;
    }

    public bool TryEquipStored(int index, PlayerReinforcementController controller)
    {
        EnsureSlotCapacity();

        if (controller == null)
        {
            return false;
        }

        if (index < 0 || index >= storedItems.Count)
        {
            return false;
        }

        ReinforcementDefinition selectedStored = storedItems[index];
        int selectedCharges = storedCharges[index];

        if (selectedStored == null)
        {
            return false;
        }

        ReinforcementDefinition currentEquipped = controller.EquippedDefinition;
        int currentCharges = controller.CurrentCharges;

        // 현재 액티브가 없으면 보관 아이템을 장착하고 슬롯은 빈칸 처리.
        if (currentEquipped == null)
        {
            bool equippedOnly = controller.EquipWithoutDropping(selectedStored, selectedCharges, true);

            if (!equippedOnly)
            {
                return false;
            }

            ClearSlot(index);
            return true;
        }

        if (currentEquipped.EquipmentId == selectedStored.EquipmentId)
        {
            return false;
        }

        if (preventDuplicateEquipment && ContainsExcept(currentEquipped.EquipmentId, index))
        {
            return false;
        }

        bool equipped = controller.EquipWithoutDropping(selectedStored, selectedCharges, true);

        if (!equipped)
        {
            return false;
        }

        storedItems[index] = currentEquipped;
        storedCharges[index] = currentCharges;
        return true;
    }

    public bool TryMoveOrSwapStored(int fromIndex, int toIndex)
    {
        EnsureSlotCapacity();

        if (fromIndex == toIndex)
        {
            return false;
        }

        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
        {
            return false;
        }

        ReinforcementDefinition fromDefinition = storedItems[fromIndex];
        int fromCharges = storedCharges[fromIndex];

        if (fromDefinition == null)
        {
            return false;
        }

        ReinforcementDefinition toDefinition = storedItems[toIndex];
        int toCharges = storedCharges[toIndex];

        storedItems[toIndex] = fromDefinition;
        storedCharges[toIndex] = fromCharges;

        storedItems[fromIndex] = toDefinition;
        storedCharges[fromIndex] = toCharges;

        return true;
    }

    public bool RemoveAt(int index, out ReinforcementDefinition removed)
    {
        return RemoveAt(index, out removed, out _);
    }

    public bool RemoveAt(int index, out ReinforcementDefinition removed, out int removedCharges)
    {
        EnsureSlotCapacity();

        removed = null;
        removedCharges = -1;

        if (!IsValidIndex(index))
        {
            return false;
        }

        removed = storedItems[index];
        removedCharges = storedCharges[index];

        if (removed == null)
        {
            return false;
        }

        ClearSlot(index);
        return true;
    }

    public void Clear()
    {
        EnsureSlotCapacity();

        for (int i = 0; i < storedItems.Count; i++)
        {
            ClearSlot(i);
        }
    }

    private void EnsureSlotCapacity()
    {
        int targetCapacity = Capacity;

        while (storedItems.Count < targetCapacity)
        {
            storedItems.Add(null);
            storedCharges.Add(-1);
        }

        while (storedItems.Count > targetCapacity)
        {
            int last = storedItems.Count - 1;
            storedItems.RemoveAt(last);
        }

        while (storedCharges.Count < storedItems.Count)
        {
            storedCharges.Add(-1);
        }

        while (storedCharges.Count > storedItems.Count)
        {
            int last = storedCharges.Count - 1;
            storedCharges.RemoveAt(last);
        }
    }

    private int GetFirstEmptyIndex()
    {
        EnsureSlotCapacity();

        for (int i = 0; i < storedItems.Count; i++)
        {
            if (storedItems[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void ClearSlot(int index)
    {
        if (!IsValidIndex(index))
        {
            return;
        }

        storedItems[index] = null;
        storedCharges[index] = -1;
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < storedItems.Count;
    }

    private bool ContainsExcept(string equipmentId, int exceptIndex)
    {
        EnsureSlotCapacity();

        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            return false;
        }

        for (int i = 0; i < storedItems.Count; i++)
        {
            if (i == exceptIndex)
            {
                continue;
            }

            ReinforcementDefinition item = storedItems[i];

            if (item != null && item.EquipmentId == equipmentId)
            {
                return true;
            }
        }

        return false;
    }
}