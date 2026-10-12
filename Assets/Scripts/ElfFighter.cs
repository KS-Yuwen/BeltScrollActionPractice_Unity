// 剣を使うエルフの基本部分。近接戦闘・所持品操作を共用し、攻撃魔法は次の段階で追加します。
public sealed class ElfFighter : ItemFighter
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Elf;
}
