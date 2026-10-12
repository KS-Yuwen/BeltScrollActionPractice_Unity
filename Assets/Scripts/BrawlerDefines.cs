// 数値の調整対象ではない、ゲーム内の意味・種類を定義します。
// 0/1/2/3 といった番号を直接使わず、名前から入力の意味を読めるようにします。
public static class BrawlerDefines
{
    public enum PlayerClass
    {
        Cleric,
        Fighter,
        Dwarf,
        Thief,
        Elf
    }

    public const float UnsetTime = -10;
    public const int UnsetInputSample = -1;
    public const int NoComboStep = 0;
    public const int StandardShaderFadeMode = 2;

    // 攻撃の排他的な種類。移動・空中・防御の状態とは分けて扱います。
    public enum AttackKind
    {
        None,
        Normal,
        Dash,
        DashSpecial,
        Uppercut,
        HeavyStrike,
        Push,
        Counter,
        Ground,
        Air,
        DownThrust,
        Crouch,
        Backstab
    }

    public enum ActionUseStatus
    {
        Ready,
        Empty,
        HealthFull,
        ProtectionActive,
        AttackBoostActive,
        Busy,
        Airborne,
        Defeated,
        Unavailable
    }

    public enum PickupKind
    {
        HealingPotion,
        ThrowingHammer,
        ThrowingKnife,
        HealingScroll,
        ProtectionScroll,
        AttackBoostScroll
    }

    public enum DamageKind
    {
        Normal,
        Counter,
        HeavyStrike,
        Healing,
        Critical
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
        None,
        Healing,
        Protection,
        HealingPotion,
        ThrowingHammer,
        ThrowingKnife,
        AttackBoost
    }

    public enum CommandDirection
    {
        Neutral,
        Down,
        DownForward,
        DownBackward,
        Up,
        Forward,
        Backward,
        UpForward,
        UpBackward
    }
}
