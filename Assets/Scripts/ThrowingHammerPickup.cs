// 銀色の床ハンマーを拾うと、職業共通の所持数を一つ増やします。
public sealed class ThrowingHammerPickup : CollectibleItem
{
    protected override BrawlerDefines.PickupKind Kind => BrawlerDefines.PickupKind.ThrowingHammer;
    protected override float PickupRadius => BrawlerBalance.HammerPickupRadius;
    protected override float LifetimeSeconds => BrawlerBalance.HammerPickupLifetimeSeconds;
    protected override bool TryStore(Fighter player) => player.TryStoreThrowingHammer();
}
