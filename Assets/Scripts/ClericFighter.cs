using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : Fighter, IPlayerActionUser
{
    // 補充先はレベルではなく魔法そのものです。所持枠・上限はアイテムと共用します。
    public override bool TryRestoreSpellUses(BrawlerDefines.UsableAction action, int amount)
    {
        bool isSpell = action == BrawlerDefines.UsableAction.Healing
            || action == BrawlerDefines.UsableAction.Protection || action == BrawlerDefines.UsableAction.AttackBoost;
        return isSpell && CanUseAction(action) && IsPlayer && Health > 0 && !Game.IsRunFinished
            && Inventory.TryAdd(action, amount);
    }

    protected override void InitializeInventory()
    {
        base.InitializeInventory();
        if (IsPlayer)
        {
            Inventory.TryAdd(BrawlerDefines.UsableAction.Healing, BrawlerBalance.HealingUses);
            Inventory.TryAdd(BrawlerDefines.UsableAction.Protection, BrawlerBalance.ProtectionUses);
            Inventory.TryAdd(BrawlerDefines.UsableAction.AttackBoost, BrawlerBalance.AttackBoostUses);
        }
        SelectedSlot = 0;
    }
    private const int HealingAmount = BrawlerBalance.HealingAmount;
    private const float RecoveryDuration = BrawlerBalance.SpellRecoverySeconds;
    private float _recoveryRemaining;
    private const float ProtectionDuration = BrawlerBalance.ProtectionSeconds;
    private float _protectionRemaining;
    private float _attackBoostRemaining;

    public float AttackBoostSecondsRemaining => _attackBoostRemaining;
    public bool IsAttackBoosted => Health > 0 && _attackBoostRemaining > 0;
    public bool IsAttackBoostExpiring => IsAttackBoosted
        && _attackBoostRemaining <= BrawlerBalance.AttackBoostWarningSeconds;
    public int AttackBoostUsesRemaining
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.AttackBoost);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.AttackBoost, value);
    }

    public override int CalculateMeleeDamage(int amount)
    {
        // 端数は切り上げます。HP回復や投擲の処理からは呼ばれません。
        return IsAttackBoosted
            ? Mathf.CeilToInt(Mathf.Max(0, amount) * BrawlerBalance.AttackBoostDamageMultiplier) : amount;
    }
    private float _turnUndeadCooldownRemaining;
    private float _turnUndeadRecoveryRemaining;

    public float TurnUndeadCooldownRemaining => _turnUndeadCooldownRemaining;

    public bool IsTurningUndead => _turnUndeadRecoveryRemaining > 0;

    // 選択は種類ではなくスロットの位置を保持します。消費で空になっても勝手に別の魔法へ移りません。
    public int SelectedSlot { get; private set; }
    public BrawlerDefines.UsableAction SelectedAction
    {
        get => Inventory.GetAction(SelectedSlot);
        private set
        {
            int slot = Inventory.FindSlot(value);
            SelectedSlot = slot >= 0 ? slot : Inventory.FindSlot(BrawlerDefines.UsableAction.None);
            if (SelectedSlot < 0)
            {
                SelectedSlot = 0;
            }
        }
    }

    public BrawlerDefines.ActionUseStatus SelectedActionUseStatus => GetActionUseStatus(SelectedAction);

    // 移動・リーチ・使用許可は職業設定を選択し、魔法の実処理だけをこのクラスに置きます。
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Cleric;

    public BrawlerDefines.ActionUseStatus GetActionUseStatus(BrawlerDefines.UsableAction action)
    {
        if (action == BrawlerDefines.UsableAction.None)
        {
            return BrawlerDefines.ActionUseStatus.Empty;
        }
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
            BrawlerDefines.UsableAction.AttackBoost => AttackBoostUsesRemaining <= 0 ? BrawlerDefines.ActionUseStatus.Empty
                : IsAttackBoosted ? BrawlerDefines.ActionUseStatus.AttackBoostActive : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.HealingPotion => HealingPotionsHeld <= 0 ? BrawlerDefines.ActionUseStatus.Empty
                : Health >= MaxHealth ? BrawlerDefines.ActionUseStatus.HealthFull : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.ThrowingHammer => ThrowingHammersHeld <= 0 ? BrawlerDefines.ActionUseStatus.Empty : BrawlerDefines.ActionUseStatus.Ready,
            _ => BrawlerDefines.ActionUseStatus.Unavailable
        };
    }

    public int SelectedActionUsesRemaining => Inventory.GetSlotCount(SelectedSlot);

    public void SelectNextAction()
    {
        if (!IsPlayer || Health <= 0 || Game.IsRunFinished)
        {
            return;
        }
        // 空き枠も含めて6枠を循環します。NOTHINGの使用は何も消費しません。
        SelectedSlot = (SelectedSlot + 1) % Inventory.SlotCount;
    }

    public bool TryUseSelectedAction()
    {
        // 既存の発動処理に委譲し、回数・硬直・満タン時の制限を共通にします。
        return SelectedAction switch
        {
            BrawlerDefines.UsableAction.Healing => TryHeal(),
            BrawlerDefines.UsableAction.Protection => TryProtection(),
            BrawlerDefines.UsableAction.AttackBoost => TryAttackBoost(),
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

    // HUDと入力判定は6スロットの所持数を参照し、別の回数状態を持ちません。
    public int ProtectionUsesRemaining
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.Protection);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.Protection, value);
    }

    public float ProtectionSecondsRemaining => _protectionRemaining;

    public bool IsProtected => _protectionRemaining > 0;

    // 表示用タイマーを別に持たず、実際の効果時間から終了予告を求めます。
    public bool IsProtectionExpiring => Health > 0 && IsProtected
        && _protectionRemaining <= BrawlerBalance.ProtectionWarningSeconds;

    // ウェーブ進行だけでは補充せず、スクロール取得またはリスタートで回数を戻します。
    public int HealingUsesRemaining
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.Healing);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.Healing, value);
    }

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0;

    protected override bool IsUsingSpecialAction => base.IsUsingSpecialAction || IsRecoveringFromHeal || IsTurningUndead;

    protected override Color? SpecialFeedbackColor => IsTurningUndead ? new Color(1, 1, 0.65f) : IsRecoveringFromHeal
        ? new Color(0.4f, 1, 0.6f) : IsProtected ? new Color(1, 0.8f, 0.3f)
        : IsAttackBoosted ? new Color(1, 0.45f, 0.2f) : (Color?)null;

    protected override void Update()
    {
        // 硬直中や回復中も効果時間が経過します。共通の戦闘更新より先に終了を判定します。
        UpdateProtection(Time.deltaTime);
        UpdateAttackBoost(Time.deltaTime);
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

    private void UpdateAttackBoost(float deltaTime)
    {
        // 発動後の隙や被弾中も経過し、ヒットストップ中はdeltaTime=0で停止します。
        _attackBoostRemaining = Mathf.Max(0, _attackBoostRemaining - Mathf.Max(0, deltaTime));
    }

    public bool TryAttackBoost()
    {
        if (GetActionUseStatus(BrawlerDefines.UsableAction.AttackBoost) != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        // この魔法だけを消費します。防御魔法とは独立し、効果は両方同時に持てます。
        Inventory.TryConsume(BrawlerDefines.UsableAction.AttackBoost);
        _attackBoostRemaining = BrawlerBalance.AttackBoostSeconds;
        _recoveryRemaining = RecoveryDuration;
        Game.PlaySound(BrawlerSound.Protection);
        ReleaseDefenseForSpecialAction();
        return true;
    }

    public bool TryProtection()
    {
        if (GetActionUseStatus(BrawlerDefines.UsableAction.Protection) != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        Inventory.TryConsume(BrawlerDefines.UsableAction.Protection);
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
        Inventory.TryConsume(BrawlerDefines.UsableAction.Healing);
        Game.PlaySound(BrawlerSound.Heal);
        _recoveryRemaining = RecoveryDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }
}
