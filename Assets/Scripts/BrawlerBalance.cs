// 戦闘・操作の調整値を集約します。単位は名前とコメントで示します。
// スライディングはCSVから検証済み設定を読み込み、他の戦闘設定は名前付き定数を使います。
// 呼び出し側へCSVの列名やファイル操作を広げず、この窓口から値を読みます。
public static class BrawlerBalance
{
    // 方向コマンド：下→斜めに0.9秒、斜め→ジャンプに0.6秒の余裕を持たせます。
    public const float DefaultCommandDirectionThreshold = 0.35f;
    public const float DefaultSlideDirectionWindowSeconds = 0.9f;
    public const float DefaultSlideButtonWindowSeconds = 0.6f;
    private static SlideBalanceSettings s_slideSettings = SlideBalanceSettings.CreateDefault();
    public static float CommandDirectionThreshold => s_slideSettings.DirectionThreshold;
    public static float SlideDirectionWindowSeconds => s_slideSettings.DirectionWindowSeconds;
    public static float SlideButtonWindowSeconds => s_slideSettings.ButtonWindowSeconds;
    public static float SlideSpeed => s_slideSettings.Speed;
    public static float SlideMinSeconds => s_slideSettings.MinSeconds;
    public static float SlideMaxSeconds => s_slideSettings.MaxSeconds;
    public static float SlideCooldownSeconds => s_slideSettings.CooldownSeconds;
    public static int SlideDamage => s_slideSettings.Damage;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSlideSettings()
    {
        // Play開始時に必ずリセットし、ドメインリロード無効時も前回の値を残しません。
        s_slideSettings = SlideBalanceSettings.CreateDefault();
        UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Balance/slide_balance");
        if (asset == null)
        {
            UnityEngine.Debug.LogWarning("スライディングCSVがないため既定値を使用します。");
            return;
        }
        if (SlideBalanceSettings.TryParse(asset.text, out SlideBalanceSettings settings, out string error))
        {
            s_slideSettings = settings;
        }
        else
        {
            UnityEngine.Debug.LogWarning("スライディングCSVを適用できず既定値を使用します：" + error);
        }
    }
    public const float JumpDirectionThreshold = 0.5f;
    public const float ForwardInputThreshold = 0.6f;
    public const float DoubleTapWindowSeconds = 0.25f;
    public const float FacingInputThreshold = 0.01f;

    // 移動速度（Unity単位/秒）・行動時間（秒）。
    public const float WalkSpeed = 5;
    public const float RunSpeed = 8;
    public const float DefaultSlideSpeed = 8;
    public const float DefaultSlideMinSeconds = 0.12f;
    public const float DefaultSlideMaxSeconds = 0.4f;
    public const float DefaultSlideCooldownSeconds = 0.55f;
    public const int DefaultSlideDamage = 12;
    public const int BodyCheckDamage = 8;
    public const float DashSpeed = 13;
    public const float DashSeconds = 0.2f;
    public const float DashCooldownSeconds = 0.85f;
    public const float BackstepSeconds = 0.14f;
    public const float BackstepCooldownSeconds = 0.6f;
    public const float JumpVelocity = 7;
    public const float HighJumpVelocity = 9;
    public const float JumpDriftSpeed = 4;
    public const float JumpGravity = 22;
    public const float DownThrustVelocity = -6;
    public const float GroundAttackAvoidanceHeight = 0.6f;

    // 攻撃ダメージと、演出・入力受付・再使用までの秒数。
    public const int NormalDamage = 18;
    public const int FinisherDamage = 30;
    public const int CounterDamage = 36;
    public const int DashAttackDamage = 32;
    public const int AirAttackDamage = 20;
    public const int DownThrustDamage = 32;
    public const int CrouchAttackDamage = 12;
    public const int GroundAttackDamage = 24;
    public const int ComboSteps = 3;
    public const float AttackBufferSeconds = 0.2f;
    public const float ComboWindowSeconds = 0.85f;
    public const float AttackSeconds = 0.3f;
    public const float NormalCooldownSeconds = 0.32f;
    public const float FinisherCooldownSeconds = 0.55f;
    public const float DashAttackSpeed = 8;
    public const float DashAttackMovementSeconds = 0.18f;
    public const float DashAttackCooldownSeconds = 0.6f;
    public const float CounterCooldownSeconds = 0.5f;
    public const float AirAttackCooldownSeconds = 0.35f;
    public const float CrouchAttackSeconds = 0.25f;
    public const float CrouchAttackCooldownSeconds = 0.3f;
    public const float GroundAttackSeconds = 0.35f;
    public const float GroundAttackCooldownSeconds = 0.55f;
    public const float NormalHitRemainingSeconds = 0.16f;
    public const float FastHitRemainingSeconds = 0.24f;
    public const float GroundHitRemainingSeconds = 0.23f;

    // 敵の標準能力と、被ダメージ・防御後の反応。
    public const float MeleeSpeed = 2.2f;
    public const float MeleeWindupSeconds = 0.55f;
    public const float MeleeAttackIntervalSeconds = 1.2f;
    public const int MeleeDamage = 10;
    public const float MeleeStopDistance = 1.1f;
    public const float MeleeLaneTolerance = 0.5f;
    public const float StunSeconds = 0.25f;
    public const float EnemyHitCooldownSeconds = 0.6f;
    public const float DownSeconds = 0.7f;
    public const int KnockdownDamageThreshold = 30;
    public const float KnockbackSpeed = 5;
    public const float StrongKnockbackSpeed = 9;
    public const float KnockbackDecay = 12;
    public const float BlockKnockbackSpeed = 1.5f;
    public const float BlockFlashSeconds = 0.15f;
    public const float CounterWindowSeconds = 0.45f;
    public const float DamageFlashSeconds = 0.12f;
    public const float EnemyRemovalSeconds = 0.5f;
    public const float StageHalfWidth = 22;
    public const float StageHalfDepth = 3;
    public const float HeavySpeed = 1.3f;
    public const float HeavyWindupSeconds = 0.95f;
    public const float HeavyAttackIntervalSeconds = 1.8f;
    public const int HeavyDamage = 24;
    public const int HealingAmount = 30;
    public const int HealingUses = 3;
    public const int ProtectionUses = 2;
    public const float SpellRecoverySeconds = 0.6f;
    public const float ProtectionSeconds = 8;
    public const float ProtectionDamageMultiplier = 0.5f;
    public const int PotionHealingAmount = 20;
    public const float PotionPickupRadius = 0.7f;
    public const float PotionLifetimeSeconds = 20;
}
