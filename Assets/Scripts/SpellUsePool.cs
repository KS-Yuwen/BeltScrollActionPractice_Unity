using UnityEngine;

// 一つの魔法レベルの使用回数を管理します。個々の魔法・職業・入力には依存しません。
// 同レベルの魔法がこのインスタンスを共有すると、どの魔法を使っても共通の回数が減ります。
public sealed class SpellUsePool
{
    public int Capacity { get; }
    public int Remaining { get; private set; }

    public SpellUsePool(int capacity)
    {
        Capacity = Mathf.Max(0, capacity);
        Remaining = Capacity;
    }

    public bool TryConsume()
    {
        if (Remaining <= 0)
        {
            return false;
        }
        Remaining--;
        return true;
    }

    public bool TryRestore(int amount)
    {
        if (amount <= 0 || Remaining >= Capacity)
        {
            return false;
        }
        Remaining += Mathf.Min(amount, Capacity - Remaining);
        return true;
    }

    // 状態の再設定でも負数・上限超過を防ぎます。通常の増減は上の専用メソッドを使います。
    internal void SetRemaining(int value)
    {
        Remaining = Mathf.Clamp(value, 0, Capacity);
    }
}
