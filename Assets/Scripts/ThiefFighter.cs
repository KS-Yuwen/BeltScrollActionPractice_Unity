// 短剣を使うシーフ。速い移動と短い間合いを設定し、基本攻撃・所持品操作は共用します。
public sealed class ThiefFighter : ItemFighter
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Thief;
}
