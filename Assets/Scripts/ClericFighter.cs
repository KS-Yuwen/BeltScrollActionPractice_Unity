using System.Collections.Generic;
using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : Fighter
{
    // 回復と補助は別レベルに割り当て、既存の独立した回数を維持します。
    // 今後同レベルの魔法を追加するときは、新しい回数フィールドを作らず同じ枠を参照します。
    private readonly Dictionary<BrawlerDefines.SpellLevel, SpellUsePool> _spellUses
        = new Dictionary<BrawlerDefines.SpellLevel, SpellUsePool>
        {
            { BrawlerBalance.HealingSpellLevel, new SpellUsePool(BrawlerBalance.HealingUses) },
            { BrawlerBalance.ProtectionSpellLevel, new SpellUsePool(BrawlerBalance.ProtectionUses) }
        };

    public override bool SupportsSpellLevel(BrawlerDefines.SpellLevel level) => _spellUses.ContainsKey(level);

    public override bool TryRestoreSpellUses(BrawlerDefines.SpellLevel level, int amount)
    {
        // 回数だけの補充なので、HP満タン・発動後の隙でも取得可能です。死亡・終了後は拒否します。
        return IsPlayer && Health > 0 && !Game.IsRunFinished
            && _spellUses.TryGetValue(level, out SpellUsePool pool) && pool.TryRestore(amount);
    }

    public int GetSpellUsesRemaining(BrawlerDefines.SpellLevel level)
    {
        return _spellUses.TryGetValue(level, out SpellUsePool pool) ? pool.Remaining : 0;
    }
    private const int HealingAmount = BrawlerBalance.HealingAmount;
    private const float RecoveryDuration = BrawlerBalance.SpellRecoverySeconds;
    private float _recoveryRemaining;
    private const float ProtectionDuration = BrawlerBalance.ProtectionSeconds;
    private float _protectionRemaining;
    private float _turnUndeadCooldownRemaining;
    private float _turnUndeadRecoveryRemaining;

    public float TurnUndeadCooldownRemaining => _turnUndeadCooldownRemaining;

    public bool IsTurningUndead => _turnUndeadRecoveryRemaining > 0;

    // 初期選択は回復。残り回数がゼロでも選択は維持し、意図しない別魔法の発動を防ぎます。
    public BrawlerDefines.UsableAction SelectedAction { get; private set; } = BrawlerDefines.UsableAction.Healing;

    public BrawlerDefines.ActionUseStatus SelectedActionUseStatus => GetActionUseStatus(SelectedAction);

    // 刃物は禁止し、クレリックの魔法・ポーション・ハンマーだけを許可します。
    public override bool CanUseAction(BrawlerDefines.UsableAction action)
    {
        return action == BrawlerDefines.UsableAction.Healing
            || action == BrawlerDefines.UsableAction.Protection
            || action == BrawlerDefines.UsableAction.HealingPotion
            || action == BrawlerDefines.UsableAction.ThrowingHammer;
    }

    public BrawlerDefines.ActionUseStatus GetActionUseStatus(BrawlerDefines.UsableAction action)
    {
        if (!CanUseAction(action))
        {
            return BrawlerDefines.ActionUseStatus.Unavailable;
        }
        // 複数の理由があるときは死亡・空中・行動中を優先し、その後に回数や効果を判定します。
        BrawlerDefines.ActionUseStatus commonStatus = CommonActionUseStatus;
        if (commonStatus != BrawlerDefines.ActionUseStatus.Ready)
        {
            return commonStatus;
        }
        return action switch
        {
            BrawlerDefines.UsableAction.Healing => HealingUsesRemaining <= 0 ? BrawlerDefines.ActionUseStatus.Empty
                : Health >= MaxHealth ? BrawlerDefines.ActionUseStatus.HealthFull : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.Protection => ProtectionUsesRemaining <= 0 ? BrawlerDefines.ActionUseStatus.Empty
                : IsProtected ? BrawlerDefines.ActionUseStatus.ProtectionActive : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.HealingPotion => HealingPotionsHeld <= 0 ? BrawlerDefines.ActionUseStatus.Empty
                : Health >= MaxHealth ? BrawlerDefines.ActionUseStatus.HealthFull : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.ThrowingHammer => ThrowingHammersHeld <= 0 ? BrawlerDefines.ActionUseStatus.Empty : BrawlerDefines.ActionUseStatus.Ready,
            _ => BrawlerDefines.ActionUseStatus.Unavailable
        };
    }

    public int SelectedActionUsesRemaining => SelectedAction switch
    {
        BrawlerDefines.UsableAction.Healing => HealingUsesRemaining,
        BrawlerDefines.UsableAction.Protection => ProtectionUsesRemaining,
        BrawlerDefines.UsableAction.HealingPotion => HealingPotionsHeld,
        BrawlerDefines.UsableAction.ThrowingHammer => ThrowingHammersHeld,
        _ => 0
    };

    public void SelectNextAction()
    {
        if (!IsPlayer || Health <= 0 || Game.IsRunFinished)
        {
            return;
        }
        // 所持数ゼロの項目も表示し、取得後に選択順が変わらないようにします。
        SelectedAction = SelectedAction switch
        {
            BrawlerDefines.UsableAction.Healing => BrawlerDefines.UsableAction.Protection,
            BrawlerDefines.UsableAction.Protection => BrawlerDefines.UsableAction.HealingPotion,
            BrawlerDefines.UsableAction.HealingPotion => BrawlerDefines.UsableAction.ThrowingHammer,
            _ => BrawlerDefines.UsableAction.Healing
        };
    }

    public bool TryUseSelectedAction()
    {
        // 既存の発動処理に委譲し、回数・硬直・満タン時の制限を共通にします。
        return SelectedAction switch
        {
            BrawlerDefines.UsableAction.Healing => TryHeal(),
            BrawlerDefines.UsableAction.Protection => TryProtection(),
            BrawlerDefines.UsableAction.HealingPotion => TryUseHealingPotion(),
            BrawlerDefines.UsableAction.ThrowingHammer => TryThrowHammer(),
            _ => false
        };
    }

    public bool TryUseHealingPotion()
    {
        if (GetActionUseStatus(BrawlerDefines.UsableAction.HealingPotion) != BrawlerDefines.ActionUseStatus.Ready
            || !TryConsumeHealingPotion())
        {
            return false;
        }
        // 使用直後は魔法と同様に足を止めます。回復量・使用後の隙は専用の定数です。
        _recoveryRemaining = BrawlerBalance.PotionRecoverySeconds;
        return true;
    }

    public bool TryThrowHammer()
    {
        if (GetActionUseStatus(BrawlerDefines.UsableAction.ThrowingHammer) != BrawlerDefines.ActionUseStatus.Ready
            || !TryConsumeThrowingHammer())
        {
            return false;
        }
        // 消費に成功した時だけ使用後の隙を設定します。無敵は付けません。
        _recoveryRemaining = BrawlerBalance.HammerRecoverySeconds;
        return true;
    }

    // HUDや入力判定も、魔法固有の複製ではなくレベルの共通枠を参照します。
    public int ProtectionUsesRemaining
    {
        get => GetSpellUsesRemaining(BrawlerBalance.ProtectionSpellLevel);
        private set => _spellUses[BrawlerBalance.ProtectionSpellLevel].SetRemaining(value);
    }

    public float ProtectionSecondsRemaining => _protectionRemaining;

    public bool IsProtected => _protectionRemaining > 0;

    // 表示用タイマーを別に持たず、実際の効果時間から終了予告を求めます。
    public bool IsProtectionExpiring => Health > 0 && IsProtected
        && _protectionRemaining <= BrawlerBalance.ProtectionWarningSeconds;

    // ウェーブ進行だけでは補充せず、スクロール取得またはリスタートで回数を戻します。
    public int HealingUsesRemaining
    {
        get => GetSpellUsesRemaining(BrawlerBalance.HealingSpellLevel);
        private set => _spellUses[BrawlerBalance.HealingSpellLevel].SetRemaining(value);
    }

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0;

    protected override bool IsUsingSpecialAction => base.IsUsingSpecialAction || IsRecoveringFromHeal || IsTurningUndead;

    protected override Color? SpecialFeedbackColor => IsTurningUndead ? new Color(1, 1, 0.65f) : IsRecoveringFromHeal
        ? new Color(0.4f, 1, 0.6f) : IsProtected ? new Color(1, 0.8f, 0.3f) : (Color?)null;

    protected override void Update()
    {
        // 硬直中や回復中も効果時間が経過します。共通の戦闘更新より先に終了を判定します。
        UpdateProtection(Time.deltaTime);
        _turnUndeadCooldownRemaining = Mathf.Max(0, _turnUndeadCooldownRemaining - Time.deltaTime);
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
        return IsProtected ? Mathf.CeilToInt(Mathf.Max(0, amount) * BrawlerBalance.ProtectionDamageMultiplier) : amount;
    }

    protected override bool UpdateSpecialAction()
    {
        // 選択だけなら攻撃中や発動後の硬直中も可能です。選択と使用の同時押しは新しい選択を使います。
        if (BrawlerInput.WasActionSelectPressed())
        {
            SelectNextAction();
        }
        if (IsTurningUndead)
        {
            _turnUndeadRecoveryRemaining = Mathf.Max(0, _turnUndeadRecoveryRemaining - Time.deltaTime);
            return IsTurningUndead;
        }
        // 回復後の短い隙には移動・攻撃・回避・ガードを止めます。無敵は付けません。
        if (IsRecoveringFromHeal)
        {
            _recoveryRemaining = Mathf.Max(0, _recoveryRemaining - Time.deltaTime);
            return IsRecoveringFromHeal;
        }
        // 下＋ジャンプのしゃがみ・スライディング操作は維持し、それ以外の攻撃＋ジャンプを優先します。
        if (BrawlerInput.WasTurnUndeadPressed()
            && BrawlerInput.ReadMovement().z >= -BrawlerBalance.JumpDirectionThreshold
            && TryTurnUndead())
        {
            return true;
        }
        // 直接発動を優先します。成立したら短絡評価で終了し、一度に二つ消費しません。
        return (BrawlerInput.WasHealPressed() && TryHeal())
            || (BrawlerInput.WasProtectionPressed() && TryProtection())
            || (BrawlerInput.WasActionUsePressed() && TryUseSelectedAction());
    }

    public bool TryProtection()
    {
        if (GetActionUseStatus(BrawlerDefines.UsableAction.Protection) != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        _spellUses[BrawlerBalance.ProtectionSpellLevel].TryConsume();
        Game.PlaySound(BrawlerSound.Protection);
        _protectionRemaining = ProtectionDuration;
        // 発動直後は回復と同じ 0.6 秒の隙。効果自体はその場で発生します。
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }

    public bool TryTurnUndead()
    {
        // 無敵は付けません。対象がいなくても発動し、向きに関係なく周囲を攻撃します。
        if (!IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction || _turnUndeadCooldownRemaining > 0)
        {
            return false;
        }
        ReleaseDefenseForSpecialAction();
        _turnUndeadCooldownRemaining = BrawlerBalance.TurnUndeadCooldownSeconds;
        _turnUndeadRecoveryRemaining = BrawlerBalance.TurnUndeadRecoverySeconds;
        Game.PlaySound(BrawlerSound.Protection);
        Game.HitUndeadTargets(this);
        return true;
    }

    public bool TryHeal()
    {
        // 満タン・死亡・行動中の入力では回数を消費しません。最大 HP は共通の回復処理で守ります。
        if (GetActionUseStatus(BrawlerDefines.UsableAction.Healing) != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        RestoreHealth(HealingAmount);
        _spellUses[BrawlerBalance.HealingSpellLevel].TryConsume();
        Game.PlaySound(BrawlerSound.Heal);
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }
}
