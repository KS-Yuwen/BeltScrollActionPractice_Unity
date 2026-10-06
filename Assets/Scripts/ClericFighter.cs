using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : Fighter
{
    private const int HealingAmount = 30;
    private const float RecoveryDuration = 0.6f;
    private float _recoveryRemaining;
    private const float ProtectionDuration = 8;
    private float _protectionRemaining;

    // 回復とは別に 2 回使用できます。効果中の再使用では回数を消費しません。
    public int ProtectionUsesRemaining { get; private set; } = 2;

    public float ProtectionSecondsRemaining => _protectionRemaining;

    public bool IsProtected => _protectionRemaining > 0;

    // 1 プレイで 3 回。ウェーブを進めても補充せず、リスタートで初期化します。
    public int HealingUsesRemaining { get; private set; } = 3;

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0;

    protected override bool IsUsingSpecialAction => IsRecoveringFromHeal;

    protected override Color? SpecialFeedbackColor => IsRecoveringFromHeal
        ? new Color(0.4f, 1, 0.6f) : IsProtected ? new Color(1, 0.8f, 0.3f) : (Color?)null;

    protected override void Update()
    {
        // 硬直中や回復中も効果時間が経過します。共通の戦闘更新より先に終了を判定します。
        UpdateProtection(Time.deltaTime);
        base.Update();
    }

    // 経過時間を受け取る形にして、フレームレートによらず効果時間を管理します。
    private void UpdateProtection(float deltaTime)
    {
        _protectionRemaining = Mathf.Max(0, _protectionRemaining - deltaTime);
    }

    protected override int CalculateReceivedDamage(int amount)
    {
        // 奇数のダメージは切り上げます。1 ダメージも無効にならず、完全無敵とは区別できます。
        return IsProtected ? Mathf.CeilToInt(Mathf.Max(0, amount) * 0.5f) : amount;
    }

    protected override bool UpdateSpecialAction()
    {
        // 回復後の短い隙には移動・攻撃・回避・ガードを止めます。無敵は付けません。
        if (IsRecoveringFromHeal)
        {
            _recoveryRemaining = Mathf.Max(0, _recoveryRemaining - Time.deltaTime);
            return IsRecoveringFromHeal;
        }
        // 同時押しは回復を優先し、一度に二つの魔法を消費しません。
        return (BrawlerInput.WasHealPressed() && TryHeal())
            || (BrawlerInput.WasProtectionPressed() && TryProtection());
    }

    public bool TryProtection()
    {
        if (!IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction
            || ProtectionUsesRemaining <= 0 || IsProtected)
        {
            return false;
        }
        ProtectionUsesRemaining--;
        _protectionRemaining = ProtectionDuration;
        // 発動直後は回復と同じ 0.6 秒の隙。効果自体はその場で発生します。
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
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
