using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : Fighter
{
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

    public int SelectedActionUsesRemaining => SelectedAction switch
    {
        BrawlerDefines.UsableAction.Healing => HealingUsesRemaining,
        BrawlerDefines.UsableAction.Protection => ProtectionUsesRemaining,
        BrawlerDefines.UsableAction.HealingPotion => HealingPotionsHeld,
        BrawlerDefines.UsableAction.ThrowingKnife => ThrowingKnivesHeld,
        _ => 0
    };

    public void SelectNextAction()
    {
        if (!IsPlayer || Health <= 0)
        {
            return;
        }
        // 所持数ゼロの項目も表示し、取得後に選択順が変わらないようにします。
        SelectedAction = SelectedAction switch
        {
            BrawlerDefines.UsableAction.Healing => BrawlerDefines.UsableAction.Protection,
            BrawlerDefines.UsableAction.Protection => BrawlerDefines.UsableAction.HealingPotion,
            BrawlerDefines.UsableAction.HealingPotion => BrawlerDefines.UsableAction.ThrowingKnife,
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
            BrawlerDefines.UsableAction.ThrowingKnife => TryThrowKnife(),
            _ => false
        };
    }

    public bool TryUseHealingPotion()
    {
        if (!TryConsumeHealingPotion())
        {
            return false;
        }
        // 使用直後は魔法と同様に足を止めます。回復量・使用後の隙は専用の定数です。
        _recoveryRemaining = BrawlerBalance.PotionRecoverySeconds;
        return true;
    }

    public bool TryThrowKnife()
    {
        if (!TryConsumeThrowingKnife())
        {
            return false;
        }
        // 消費に成功した時だけ使用後の隙を設定します。無敵は付けません。
        _recoveryRemaining = BrawlerBalance.KnifeRecoverySeconds;
        return true;
    }

    // 回復とは別に 2 回使用できます。効果中の再使用では回数を消費しません。
    public int ProtectionUsesRemaining { get; private set; } = BrawlerBalance.ProtectionUses;

    public float ProtectionSecondsRemaining => _protectionRemaining;

    public bool IsProtected => _protectionRemaining > 0;

    // 1 プレイで 3 回。ウェーブを進めても補充せず、リスタートで初期化します。
    public int HealingUsesRemaining { get; private set; } = BrawlerBalance.HealingUses;

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0;

    protected override bool IsUsingSpecialAction => IsRecoveringFromHeal || IsTurningUndead;

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
        if (!IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction
            || ProtectionUsesRemaining <= 0 || IsProtected)
        {
            return false;
        }
        ProtectionUsesRemaining--;
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
        if (!IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction
            || HealingUsesRemaining <= 0 || Health >= MaxHealth)
        {
            return false;
        }
        RestoreHealth(HealingAmount);
        HealingUsesRemaining--;
        Game.PlaySound(BrawlerSound.Heal);
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }
}
