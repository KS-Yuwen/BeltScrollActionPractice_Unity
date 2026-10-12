using UnityEngine;

// クレリック固有の魔法を担当します。移動・攻撃・被ダメージは Fighter の共通処理を使います。
public sealed class ClericFighter : SpellFighter
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
    }
    private const int HealingAmount = BrawlerBalance.HealingAmount;
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

    // 移動・リーチ・使用許可は職業設定を選択し、魔法の実処理だけをこのクラスに置きます。
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Cleric;

    public override BrawlerDefines.ActionUseStatus GetActionUseStatus(BrawlerDefines.UsableAction action)
    {
        // 空き枠・職業の許可・行動状態・所持数は所持品の基底クラスで判定します。
        BrawlerDefines.ActionUseStatus commonStatus = base.GetActionUseStatus(action);
        if (commonStatus != BrawlerDefines.ActionUseStatus.Ready)
        {
            return commonStatus;
        }
        // ここではクレリックの魔法だけが持つ、不成立の理由を追加します。
        return action switch
        {
            BrawlerDefines.UsableAction.Healing => Health >= MaxHealth
                ? BrawlerDefines.ActionUseStatus.HealthFull : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.Protection => IsProtected
                ? BrawlerDefines.ActionUseStatus.ProtectionActive : BrawlerDefines.ActionUseStatus.Ready,
            BrawlerDefines.UsableAction.AttackBoost => IsAttackBoosted
                ? BrawlerDefines.ActionUseStatus.AttackBoostActive : BrawlerDefines.ActionUseStatus.Ready,
            _ => commonStatus
        };
    }

    public override bool TryUseSelectedAction()
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

    public bool IsRecoveringFromHeal => _recoveryRemaining > 0 || IsRecoveringFromSpell;

    protected override bool IsUsingSpecialAction => base.IsUsingSpecialAction || IsRecoveringFromHeal || IsTurningUndead;

    // 選択枠ではなく予約した魔法で色を決め、詠唱中に枠を切り替えても演出を維持します。
    protected override Color SpellEffectColor => ActiveSpell switch
    {
        BrawlerDefines.UsableAction.Protection => new Color(1, 0.8f, 0.3f),
        BrawlerDefines.UsableAction.AttackBoost => new Color(1, 0.45f, 0.2f),
        _ => new Color(0.4f, 1, 0.6f)
    };

    protected override Color? SpecialFeedbackColor => IsCastingSpell || IsReleasingSpell || IsRecoveringFromSpell
        ? base.SpecialFeedbackColor : IsTurningUndead ? new Color(1, 1, 0.65f) : IsRecoveringFromHeal
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
        // 詠唱・発動・後隙の時間は基底クラスが更新します。ここでは入力を止めるだけです。
        if (IsCastingSpell || IsReleasingSpell || IsRecoveringFromSpell)
        {
            return true;
        }
        if (IsTurningUndead)
        {
            _turnUndeadRecoveryRemaining = Mathf.Max(0, _turnUndeadRecoveryRemaining - Time.deltaTime);
            return IsTurningUndead;
        }
        // 回復後の短い隙には移動・攻撃・回避・ガードを止めます。無敵は付けません。
        if (_recoveryRemaining > 0)
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
        // 効果中の再使用は共通の使用条件で拒否し、詠唱開始時にこの魔法だけを消費します。
        return TryBeginSpell(BrawlerDefines.UsableAction.AttackBoost, BrawlerBalance.AttackBoostCastSeconds,
            BrawlerBalance.AttackBoostEffectSeconds, BrawlerBalance.SpellRecoverySeconds);
    }

    public bool TryProtection()
    {
        // 防御効果は詠唱完了まで付けません。詠唱中の被弾は他の魔法と同じく中断されます。
        return TryBeginSpell(BrawlerDefines.UsableAction.Protection, BrawlerBalance.ProtectionCastSeconds,
            BrawlerBalance.ProtectionEffectSeconds, BrawlerBalance.SpellRecoverySeconds);
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
        // 満タン・死亡・行動中は消費せず、開始できた場合だけ基底クラスへ予約します。
        return TryBeginSpell(BrawlerDefines.UsableAction.Healing, BrawlerBalance.HealingCastSeconds,
            BrawlerBalance.HealingEffectSeconds, BrawlerBalance.SpellRecoverySeconds);
    }

    protected override void ApplySpell(BrawlerDefines.UsableAction action)
    {
        // 予約した魔法の効果を一度だけ開始します。持続時間は詠唱完了から数えます。
        // 消費・詠唱・敵停止・後隙・被弾中断はSpellFighterで共用します。
        switch (action)
        {
            case BrawlerDefines.UsableAction.Healing:
                RestoreHealth(HealingAmount);
                Game.PlaySound(BrawlerSound.Heal);
                break;
            case BrawlerDefines.UsableAction.Protection:
                _protectionRemaining = ProtectionDuration;
                Game.PlaySound(BrawlerSound.Protection);
                break;
            case BrawlerDefines.UsableAction.AttackBoost:
                _attackBoostRemaining = BrawlerBalance.AttackBoostSeconds;
                Game.PlaySound(BrawlerSound.Protection);
                break;
        }
    }
}
