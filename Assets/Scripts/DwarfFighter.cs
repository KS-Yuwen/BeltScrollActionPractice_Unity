// 斧を使うドワーフ。職業設定と見た目の差だけを宣言し、戦闘・所持品操作は共用します。
public sealed class DwarfFighter : ItemFighter
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Dwarf;
    protected override float ModelHeightScale => BrawlerBalance.DwarfModelHeightScale;
    protected override float NormalAttackArmAngle => BrawlerBalance.DwarfNormalAttackArmAngle;
}
