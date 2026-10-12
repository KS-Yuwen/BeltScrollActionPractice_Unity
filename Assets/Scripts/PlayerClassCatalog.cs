// 職業別設定の一覧です。別職業を追加するときは設定と派生Fighterの組み合わせを用意します。
public static class PlayerClassCatalog
{
    // 現在の性能を維持する武器倍率。今後の職業では個別の名前付き定数を追加します。
    private const float StandardWeaponReachMultiplier = 1f;

    // 共通キャラの従来の使用許可を維持します。ナイフの処理は他職業向けに残します。
    public static PlayerClassSettings Common { get; } = new PlayerClassSettings(
        BrawlerBalance.WalkSpeed, BrawlerBalance.RunSpeed, BrawlerBalance.PlayerAttackReach,
        StandardWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingKnife,
        BrawlerDefines.UsableAction.ThrowingHammer);

    // 最初のファイターは共通の操作性能を使用します。職業差はここで個別に調整できます。
    public static PlayerClassSettings Fighter { get; } = new PlayerClassSettings(
        BrawlerBalance.WalkSpeed, BrawlerBalance.RunSpeed, BrawlerBalance.PlayerAttackReach,
        StandardWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingKnife,
        BrawlerDefines.UsableAction.ThrowingHammer);

    // 小柄な近接職。魔法は使わず、ファイターと同じアイテムを使用します。
    public static PlayerClassSettings Dwarf { get; } = new PlayerClassSettings(
        BrawlerBalance.DwarfWalkSpeed, BrawlerBalance.DwarfRunSpeed, BrawlerBalance.DwarfNormalAttackReach,
        BrawlerBalance.DwarfWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingKnife,
        BrawlerDefines.UsableAction.ThrowingHammer);

    // シーフは速い移動と短いリーチを持ち、魔法を使わずアイテムを使用します。
    public static PlayerClassSettings Thief { get; } = new PlayerClassSettings(
        BrawlerBalance.ThiefWalkSpeed, BrawlerBalance.ThiefRunSpeed, BrawlerBalance.ThiefNormalAttackReach,
        BrawlerBalance.ThiefWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingKnife,
        BrawlerDefines.UsableAction.ThrowingHammer);

    // エルフの基本部分。魔法の使用許可は攻撃魔法の実装時に追加します。
    public static PlayerClassSettings Elf { get; } = new PlayerClassSettings(
        BrawlerBalance.ElfWalkSpeed, BrawlerBalance.ElfRunSpeed, BrawlerBalance.ElfNormalAttackReach,
        BrawlerBalance.ElfWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingKnife,
        BrawlerDefines.UsableAction.ThrowingHammer);

    // 刃物を禁止し、回復・補助魔法とポーション・ハンマーを使用できます。
    public static PlayerClassSettings Cleric { get; } = new PlayerClassSettings(
        BrawlerBalance.WalkSpeed, BrawlerBalance.RunSpeed, BrawlerBalance.PlayerAttackReach,
        StandardWeaponReachMultiplier,
        BrawlerDefines.UsableAction.HealingPotion, BrawlerDefines.UsableAction.ThrowingHammer,
        BrawlerDefines.UsableAction.Healing, BrawlerDefines.UsableAction.Protection,
        BrawlerDefines.UsableAction.AttackBoost);
}
