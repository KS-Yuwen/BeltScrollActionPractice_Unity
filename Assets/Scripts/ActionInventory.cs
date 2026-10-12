using System;

// アイテムと魔法を同じ固定枠で管理します。入力・職業・見た目には依存しません。
// スロットの位置は詰め直さず、数が0になった位置をそのまま空き枠に戻します。
public sealed class ActionInventory
{
    private readonly BrawlerDefines.UsableAction[] _actions = new BrawlerDefines.UsableAction[BrawlerBalance.InventorySlotCount];
    private readonly int[] _counts = new int[BrawlerBalance.InventorySlotCount];

    public int SlotCount => _actions.Length;

    public BrawlerDefines.UsableAction GetAction(int slot)
    {
        return slot >= 0 && slot < SlotCount ? _actions[slot] : BrawlerDefines.UsableAction.None;
    }

    public int GetSlotCount(int slot) => slot >= 0 && slot < SlotCount ? _counts[slot] : 0;

    public int FindSlot(BrawlerDefines.UsableAction action)
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (_actions[slot] == action)
            {
                return slot;
            }
        }
        return -1;
    }

    public int GetCount(BrawlerDefines.UsableAction action)
    {
        return action == BrawlerDefines.UsableAction.None ? 0 : GetSlotCount(FindSlot(action));
    }

    public bool TryAdd(BrawlerDefines.UsableAction action, int amount = 1)
    {
        if (action == BrawlerDefines.UsableAction.None || !Enum.IsDefined(typeof(BrawlerDefines.UsableAction), action)
            || amount <= 0 || amount > BrawlerBalance.InventoryStackLimit)
        {
            return false;
        }
        int slot = FindSlot(action);
        if (slot < 0)
        {
            slot = FindSlot(BrawlerDefines.UsableAction.None);
        }
        // 同種は既存枠へ加算します。満枠でも、既存の数に余裕があれば取得できます。
        if (slot < 0 || amount > BrawlerBalance.InventoryStackLimit - _counts[slot])
        {
            return false;
        }
        _actions[slot] = action;
        _counts[slot] += amount;
        return true;
    }

    public bool TryConsume(BrawlerDefines.UsableAction action)
    {
        int slot = FindSlot(action);
        if (action == BrawlerDefines.UsableAction.None || slot < 0 || _counts[slot] <= 0)
        {
            return false;
        }
        _counts[slot]--;
        if (_counts[slot] == 0)
        {
            _actions[slot] = BrawlerDefines.UsableAction.None;
        }
        return true;
    }

    // 初期化・検証時も同じスロット規則を通します。通常の取得・消費は上のメソッドを使用します。
    internal void SetCount(BrawlerDefines.UsableAction action, int count)
    {
        int slot = FindSlot(action);
        if (slot >= 0)
        {
            _counts[slot] = 0;
            _actions[slot] = BrawlerDefines.UsableAction.None;
        }
        count = Math.Clamp(count, 0, BrawlerBalance.InventoryStackLimit);
        if (count > 0)
        {
            if (slot >= 0)
            {
                _actions[slot] = action;
                _counts[slot] = count;
            }
            else
            {
                TryAdd(action, count);
            }
        }
    }

    public void Clear()
    {
        Array.Clear(_actions, 0, _actions.Length);
        Array.Clear(_counts, 0, _counts.Length);
    }
}
