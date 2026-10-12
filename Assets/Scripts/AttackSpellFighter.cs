// 攻撃魔法の種類と効果を定義します。詠唱・演出・中断はSpellFighterに任せます。
public abstract class AttackSpellFighter : SpellFighter
{
    public override bool TryRestoreSpellUses(BrawlerDefines.UsableAction action, int amount)
    {
        return action == BrawlerDefines.UsableAction.MagicMissile && CanUseAction(action)
            && IsPlayer && Health > 0 && !Game.IsRunFinished && Inventory.TryAdd(action, amount);
    }

    public override bool TryUseSelectedAction()
    {
        return SelectedAction == BrawlerDefines.UsableAction.MagicMissile
            ? TryBeginSpell(SelectedAction, BrawlerBalance.MagicMissileCastSeconds,
                BrawlerBalance.MagicMissileEffectSeconds, BrawlerBalance.MagicMissileRecoverySeconds)
            : base.TryUseSelectedAction();
    }

    protected override void ApplySpell(BrawlerDefines.UsableAction action)
    {
        if (action == BrawlerDefines.UsableAction.MagicMissile)
        {
            // 発動時の位置と向きで対象を探すため、詠唱中に移動した敵にも正しく対応します。
            Game.CastMagicMissile(this);
        }
    }
}
