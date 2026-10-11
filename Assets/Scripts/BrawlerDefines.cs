// 数値の調整対象ではない、ゲーム内の意味・種類を定義します。
// 0/1/2/3 といった番号を直接使わず、名前から入力の意味を読めるようにします。
public static class BrawlerDefines
{
    public const float UnsetTime = -10;

    public enum PickupKind
    {
        HealingPotion,
        ThrowingKnife
    }

    public enum DamageKind
    {
        Normal,
        Counter,
        HeavyStrike,
        Healing
    }

    public enum ImpactKind
    {
        Normal,
        Guard,
        Counter
    }

    // 魔法と所持品を名前で扱い、選択・使用・表示で共用します。
    public enum UsableAction
    {
        Healing,
        Protection,
        HealingPotion,
        ThrowingKnife
    }

    public enum CommandDirection
    {
        Neutral,
        Down,
        DownForward,
        DownBackward
    }
}
