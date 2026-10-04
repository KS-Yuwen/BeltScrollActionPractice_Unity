using UnityEngine;

// HP は職業や操作方法が違っても共通なので、戦闘キャラクターの基底クラスにまとめます。
// abstract のため単体では使わず、Fighter などの派生クラスから利用します。
public abstract class Combatant : MonoBehaviour
{
    public int Health { get; private set; }

    public int MaxHealth { get; private set; }

    protected void InitializeHealth(int maxHealth)
    {
        MaxHealth = Mathf.Max(1, maxHealth);
        Health = MaxHealth;
    }

    // 回復魔法を追加する際も、この共通処理を使って上限を守ります。
    public void RestoreHealth(int amount)
    {
        if (Health <= 0)
        {
            return;
        }
        Health = Mathf.Min(MaxHealth, Health + Mathf.Max(0, amount));
    }

    // 被ダメージの可否は派生クラスが判断し、HP の計算だけ基底クラスが担当します。
    protected void ReduceHealth(int amount)
    {
        Health = Mathf.Max(0, Health - Mathf.Max(0, amount));
    }
}
