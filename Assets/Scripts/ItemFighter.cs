using UnityEngine;

// 近接職の所持品操作を共通化します。武器や職業設定は派生クラスが選びます。
public abstract class ItemFighter : Fighter, IPlayerActionUser
{
    public int SelectedSlot { get; private set; }
    public BrawlerDefines.UsableAction SelectedAction
    {
        get => Inventory.GetAction(SelectedSlot);
        protected set
        {
            // 指定した種類がなければ空き枠へ移ります。通常の選択操作は位置を循環させます。
            int slot = Inventory.FindSlot(value);
            SelectedSlot = slot >= 0 ? slot : Inventory.FindSlot(BrawlerDefines.UsableAction.None);
            if (SelectedSlot < 0)
            {
                SelectedSlot = 0;
            }
        }
    }
    public BrawlerDefines.ActionUseStatus SelectedActionUseStatus => GetActionUseStatus(SelectedAction);
    public int SelectedActionUsesRemaining => Inventory.GetSlotCount(SelectedSlot);

    // 直接入力と選択入力で同じ判定を使用します。魔法固有の条件は派生クラスで追加します。
    public virtual BrawlerDefines.ActionUseStatus GetActionUseStatus(BrawlerDefines.UsableAction action)
    {
        if (action == BrawlerDefines.UsableAction.None)
        {
            return BrawlerDefines.ActionUseStatus.Empty;
        }
        if (!CanUseAction(action))
        {
            return BrawlerDefines.ActionUseStatus.Unavailable;
        }
        BrawlerDefines.ActionUseStatus commonStatus = CommonActionUseStatus;
        if (commonStatus != BrawlerDefines.ActionUseStatus.Ready)
        {
            return commonStatus;
        }
        if (Inventory.GetCount(action) <= 0)
        {
            return BrawlerDefines.ActionUseStatus.Empty;
        }
        return action == BrawlerDefines.UsableAction.HealingPotion && Health >= MaxHealth
            ? BrawlerDefines.ActionUseStatus.HealthFull : BrawlerDefines.ActionUseStatus.Ready;
    }

    protected override void InitializeInventory()
    {
        base.InitializeInventory();
        SelectedSlot = 0;
    }

    public void SelectNextAction()
    {
        if (IsPlayer && Health > 0 && !Game.IsRunFinished)
        {
            // 空き枠も含めて循環し、使い切っても選択位置を維持します。
            SelectedSlot = (SelectedSlot + 1) % Inventory.SlotCount;
        }
    }

    public virtual bool TryUseSelectedAction()
    {
        if (SelectedActionUseStatus != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        switch (SelectedAction)
        {
            case BrawlerDefines.UsableAction.ThrowingKnife:
                return TryThrowKnife();
            case BrawlerDefines.UsableAction.ThrowingHammer:
                return TryConsumeThrowingHammerWithRecovery();
            case BrawlerDefines.UsableAction.HealingPotion:
                return TryConsumeHealingPotionWithRecovery();
            default:
                return false;
        }
    }

    protected override bool UpdateSpecialAction()
    {
        // 攻撃中も選択可能。選択と使用が同時なら切り替え後の枠を使います。
        if (BrawlerInput.WasActionSelectPressed())
        {
            SelectNextAction();
        }
        return IsUsingSpecialAction || (BrawlerInput.WasActionUsePressed() && TryUseSelectedAction());
    }
}
