// スクロールは所持品には入れず、取得した瞬間に対応レベルの魔法回数を補充します。
// 未対応の職業・回数上限では床に残るため、消費後に戻って取得できます。
public sealed class MagicScrollPickup : CollectibleItem
{
    public BrawlerDefines.SpellLevel Level { get; private set; } = BrawlerBalance.HealingSpellLevel;

    protected override float PickupRadius => BrawlerBalance.ScrollPickupRadius;
    protected override float LifetimeSeconds => BrawlerBalance.ScrollLifetimeSeconds;
    protected override BrawlerDefines.PickupKind Kind => Level == BrawlerBalance.HealingSpellLevel
        ? BrawlerDefines.PickupKind.HealingScroll : BrawlerDefines.PickupKind.ProtectionScroll;

    public void Initialize(Fighter player, BrawlerAudio audio, BrawlerDefines.SpellLevel level)
    {
        Level = level;
        base.Initialize(player, audio);
    }

    protected override bool TryStore(Fighter player)
    {
        return player.TryRestoreSpellUses(Level, BrawlerBalance.ScrollRestoreUses);
    }
}
