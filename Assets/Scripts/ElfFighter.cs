// エルフの職業設定と初期所持。攻撃魔法の入力・消費・硬直は基底クラスで共用します。
public sealed class ElfFighter : AttackSpellFighter
{
    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Elf;

    protected override void InitializeInventory()
    {
        base.InitializeInventory();
        if (IsPlayer)
        {
            Inventory.TryAdd(BrawlerDefines.UsableAction.MagicMissile, BrawlerBalance.MagicMissileUses);
        }
    }
}
