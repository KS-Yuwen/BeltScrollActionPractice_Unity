// スクロールは所持品には入れず、取得した瞬間に対応する魔法の使用回数を補充します。
// 未対応の職業・回数上限では床に残るため、消費後に戻って取得できます。
public sealed class MagicScrollPickup : CollectibleItem
{
    public BrawlerDefines.UsableAction TargetAction { get; private set; } = BrawlerDefines.UsableAction.Healing;

    protected override float PickupRadius => BrawlerBalance.ScrollPickupRadius;
    protected override float LifetimeSeconds => BrawlerBalance.ScrollLifetimeSeconds;
    protected override BrawlerDefines.PickupKind Kind => TargetAction switch
    {
        BrawlerDefines.UsableAction.Healing => BrawlerDefines.PickupKind.HealingScroll,
        BrawlerDefines.UsableAction.AttackBoost => BrawlerDefines.PickupKind.AttackBoostScroll,
        _ => BrawlerDefines.PickupKind.ProtectionScroll
    };

    public void Initialize(Fighter player, BrawlerAudio audio, BrawlerDefines.UsableAction action)
    {
        TargetAction = action;
        base.Initialize(player, audio);
    }

    protected override bool TryStore(Fighter player)
    {
        return player.TryRestoreSpellUses(TargetAction, BrawlerBalance.ScrollRestoreUses);
    }
}
