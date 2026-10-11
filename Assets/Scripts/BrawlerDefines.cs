// 数値の調整対象ではない、ゲーム内の意味・種類を定義します。
// 0/1/2/3 といった番号を直接使わず、名前から入力の意味を読めるようにします。
public static class BrawlerDefines
{
    public const float UnsetTime = -10;

    // 魔法と所持品を名前で扱い、選択・使用・表示で共用します。
    public enum UsableAction
    {
        Healing,
        Protection,
        HealingPotion
    }

    public enum CommandDirection
    {
        Neutral,
        Down,
        DownForward,
        DownBackward
    }
}
