using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : Fighter
{
    private const int HealingAmount = 30;
    private const float RecoveryDuration = 0.6f;
    private float _recoveryRemaining;

    // 1 プレイで 3 回。ウェーブを進めても補充せず、リスタートで初期化します。
    public int HealingUsesRemaining { get; private set; } = 3;

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0;

    protected override bool IsUsingSpecialAction => IsRecoveringFromHeal;

    protected override Color? SpecialFeedbackColor => IsRecoveringFromHeal
        ? new Color(0.4f, 1, 0.6f) : (Color?)null;

    protected override bool UpdateSpecialAction()
    {
        // 回復後の短い隙には移動・攻撃・回避・ガードを止めます。無敵は付けません。
        if (IsRecoveringFromHeal)
        {
            _recoveryRemaining = Mathf.Max(0, _recoveryRemaining - Time.deltaTime);
            return IsRecoveringFromHeal;
        }
        return BrawlerInput.WasHealPressed() && TryHeal();
    }

    public bool TryHeal()
    {
        // 満タン・死亡・行動中の入力では回数を消費しません。最大 HP は共通の回復処理で守ります。
        if (!IsPlayer || !CanUseSpecialAction || IsRecoveringFromHeal
            || HealingUsesRemaining <= 0 || Health >= MaxHealth)
        {
            return false;
        }
        RestoreHealth(HealingAmount);
        HealingUsesRemaining--;
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }
}
