// ポーション固有の設定と保存先だけを定義します。拾った時点では回復しません。
public sealed class HealingPotion : CollectibleItem
{
    protected override float PickupRadius => BrawlerBalance.PotionPickupRadius;
    protected override float LifetimeSeconds => BrawlerBalance.PotionLifetimeSeconds;
    protected override bool TryStore(Fighter player) => player.TryStoreHealingPotion();
}
