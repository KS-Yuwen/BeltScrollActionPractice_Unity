// 銀色の床ナイフを拾うと、職業共通の所持数を一つ増やします。
public sealed class ThrowingKnifePickup : CollectibleItem
{
    protected override BrawlerDefines.PickupKind Kind => BrawlerDefines.PickupKind.ThrowingKnife;
    protected override float PickupRadius => BrawlerBalance.KnifePickupRadius;
    protected override float LifetimeSeconds => BrawlerBalance.KnifePickupLifetimeSeconds;
    protected override bool TryStore(Fighter player) => player.TryStoreThrowingKnife();
}
