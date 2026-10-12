// 剣と盾を使うファイター。戦闘・所持品操作は基底クラスを共用します。
public sealed class SwordFighter : ItemFighter
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Fighter;
}
