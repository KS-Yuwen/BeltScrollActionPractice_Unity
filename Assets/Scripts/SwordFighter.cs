using UnityEngine;

// 剣と盾を使うファイター。共通の戦闘を継承し、魔法を使わず所持アイテムを選択・使用します。
public sealed class SwordFighter : Fighter, IPlayerActionUser
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Fighter;
    public int SelectedSlot { get; private set; }
    public BrawlerDefines.UsableAction SelectedAction => Inventory.GetAction(SelectedSlot);
    public BrawlerDefines.ActionUseStatus SelectedActionUseStatus
    {
        get
        {
            if (SelectedAction == BrawlerDefines.UsableAction.None)
            {
                return BrawlerDefines.ActionUseStatus.Empty;
            }
            if (!CanUseAction(SelectedAction))
            {
                return BrawlerDefines.ActionUseStatus.Unavailable;
            }
            if (CommonActionUseStatus != BrawlerDefines.ActionUseStatus.Ready)
            {
                return CommonActionUseStatus;
            }
            if (SelectedAction == BrawlerDefines.UsableAction.HealingPotion && Health >= MaxHealth)
            {
                return BrawlerDefines.ActionUseStatus.HealthFull;
            }
            return BrawlerDefines.ActionUseStatus.Ready;
        }
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

    public bool TryUseSelectedAction()
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
