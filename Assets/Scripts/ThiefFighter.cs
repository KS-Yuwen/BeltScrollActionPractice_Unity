// 短剣を使うシーフ。速い移動と短い間合いを設定し、基本攻撃・所持品操作は共用します。
public sealed class ThiefFighter : ItemFighter
{
    // 着地ごとに空中で一度だけ跳び直せます。回避・被弾の状態判断は基底クラスへ委譲します。
    protected override int ExtraAirJumpCount => BrawlerBalance.ThiefExtraAirJumps;
    protected override float AirJumpVelocity => BrawlerBalance.ThiefAirJumpVelocity;
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Thief;
}
