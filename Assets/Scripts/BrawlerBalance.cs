// 戦闘・操作の調整値を集約します。単位は名前とコメントで示します。
// スライディングはCSVから検証済み設定を読み込み、他の戦闘設定は名前付き定数を使います。
// 呼び出し側へCSVの列名やファイル操作を広げず、この窓口から値を読みます。
public static class BrawlerBalance
{
    // 得点・出現・命中演出の調整値。CSV化する際も呼び出し側ではこの名前を使います。
    public const int HudFontSize = 24;
    public const int PlayerMaxHealth = 100;
    public const int EnemyBaseHealth = 55;
    public const int EnemyHealthPerWave = 5;
    public const int HeavyBaseHealth = 100;
    public const int HeavyHealthPerWave = 8;
    public const int WaveBaseEnemyCount = 2;
    public const int WaveMaxEnemyCount = 7;
    public const int MixedEnemiesStartWave = 2;
    public const int RangedSpawnInterval = 3;
    public const int RangedSpawnOffset = 2;
    public const int HeavySpawnIndex = 3;
    public const int PotionDropInterval = 3;
    public const int HitScore = 10;
    public const int HitScoreMultiplierLimit = 10;
    public const int DefeatScore = 100;
    public const float HitChainSeconds = 2;
    public const float NormalHitStopSeconds = 0.04f;
    public const float StrongHitStopSeconds = 0.08f;
    public const float CameraShakeSeconds = 0.16f;
    public const float CameraShakeStrength = 0.7f;
    public const float CameraFollowHalfWidth = 16;
    public const float SpawnBaseDistance = 7;
    public const float SpawnMinimumDistance = 4;
    public const float SpawnLaneHalfWidth = 2.5f;
    public const float HitImpactHeight = 1.2f;
    public const int HitImpactRayCount = 6;
    public const float HitImpactRayOffset = 0.3f;
    public const float HitImpactRayLength = 0.3f;
    public const float HitImpactRayThickness = 0.06f;
    public const float StrongHitImpactSize = 1.4f;
    public const float NormalHitImpactSize = 1;
    public const float BodyCheckReach = 0.9f;
    public const float SlideHitReach = 1.1f;
    public const float ContactLaneHalfWidth = 0.6f;
    public const float GroundAttackReach = 1.8f;
    public const int EnemyProjectileDamage = 8;

    public const int StageWaveCount = 5;
    public const float ResultPanelWidth = 560f;
    public const float ResultPanelHeight = 300f;
    public const float ResultPanelMargin = 24f;
    public const float ResultRowHeight = 48f;
    public const int ResultTitleFontSize = 44;
    public const int ResultBodyFontSize = 24;
    public const float LowHealthWarningFraction = 0.25f;
    public const float LowHealthWarningPulsesPerSecond = 1.5f;
    public const float LowHealthWarningBorderWidth = 3f;
    public const float LowHealthWarningLabelWidth = 110f;
    public const float LowHealthWarningLabelGap = 8f;
    public const float ProtectionWarningSeconds = 3f;
    public const float ProtectionWarningPulsesPerSecond = 2f;
    public const int SelectionStatusFontSize = 14;
    public const float HitDirectionSeconds = 0.6f;
    public const float HitDirectionFadeSeconds = 0.2f;
    public const float HitDirectionWorldOffset = 1f;
    public const float HitDirectionWorldHeight = 1.4f;
    public const float HitDirectionLabelSize = 60f;
    public const int HitDirectionFontSize = 42;
    // HPは即座に反映し、赤い残像だけを待機後に最大HPの一定割合/秒で縮めます。
    public const float HealthTrailDelaySeconds = 0.25f;
    public const float HealthTrailFractionPerSecond = 1.2f;
    public const float PlayerHealthBarWidth = 300f;
    public const float PlayerHealthBarHeight = 24f;
    public const int PlayerHealthBarFontSize = 18;
    public const float EnemyHealthBarWidth = 70f;
    public const float EnemyHealthBarHeight = 10f;
    public const float EnemyHealthBarWorldHeight = 2.3f;
    // ウェーブ進行と中央の通知。既存の待ち時間を維持し、表示はその間に収めます。
    public const float NextWaveDelaySeconds = 2f;
    public const float SpawnWarningSeconds = 1.5f;
    public const float WaveBannerSeconds = 1.2f;
    public const float WaveBannerFadeSeconds = 0.3f;
    public const float WaveBannerWidth = 600f;
    public const float WaveBannerHeight = 80f;
    public const int WaveBannerFontSize = 44;
    // 選択UI。小さい画面では四つの枠をまとめて縮小し、操作説明の上へ配置します。
    public const float SelectionHudMargin = 24f;
    public const float SelectionCardWidth = 154f;
    public const float SelectionCardHeight = 92f;
    public const float SelectionCardGap = 8f;
    public const float SelectionCardPadding = 8f;
    public const float SelectionBorderWidth = 3f;
    public const float SelectionHintHeight = 30f;
    public const float SelectionBottomOffset = 52f;
    public const int SelectionTitleFontSize = 18;
    public const int SelectionCountFontSize = 22;
    public const float PickupNoticeSeconds = 0.9f;
    public const float PickupNoticeWidth = 280f;
    public const float PickupNoticeHeight = 2.8f;
    public const float PickupNoticeItemSpacing = 0.6f;
    public const int PickupNoticeFontSize = 24;
    public const int WaveHealingAmount = 15;
    // ダメージ表示は実時間で上昇・消滅させます。文字サイズと配置もここで調整します。
    public const float DamageNumberSeconds = 0.7f;
    public const float DamageNumberRiseSpeed = 1f;
    public const float DamageNumberHeight = 2.2f;
    public const int DamageNumberFontSize = 28;
    public const int SpecialDamageNumberFontSize = 32;
    public const float DamageNumberWidth = 120f;
    public const float DamageNumberLabelHeight = 48f;
    // 実際の命中判定と予告表示で共用し、調整後も範囲がずれないようにします。
    public const float PlayerAttackReach = 2.475f;
    public const float EnemyAttackReach = 1.65f;
    public const float AttackLaneHalfWidth = 0.8f;
    public const float AttackBehindAllowance = 0.25f;
    public const float EnemyProjectileSpeed = 8f;
    public const float EnemyProjectileLifetimeSeconds = 4f;
    public const float EnemyProjectileHitRadius = 0.45f;
    public const float EnemyProjectileLaunchOffset = 0.7f;
    public const float EnemyProjectileHeight = 1.2f;

    // 足元の予告枠。床との重なりを避ける高さと、敵の向きを示す短い棒を定義します。
    public const float WarningMarkerHeight = 0.04f;
    public const float WarningMarkerWidth = 1.2f;
    public const float WarningMarkerDepth = 0.8f;
    public const float WarningMarkerThickness = 0.05f;
    public const float WarningDirectionOffset = 0.85f;
    public const float WarningDirectionLength = 0.4f;
    // 防御・反撃の演出設定。時間は実時間で進め、ヒットストップ中も消えるようにします。
    public const float GuardImpactSeconds = 0.22f;
    public const float CounterImpactSeconds = 0.3f;
    public const float NormalImpactSeconds = 0.18f;
    public const int GuardSparkCount = 8;
    public const int CounterRayCount = 4;
    public const float ImpactFullCircleDegrees = 360f;
    public const float DefenseRayOffset = 0.3f;
    public const float DefenseRayLength = 0.45f;
    public const float DefenseRayThickness = 0.06f;
    public const float GuardImpactSize = 0.8f;
    public const float CounterImpactSize = 1.8f;
    public const float CounterImpactHeight = 1.2f;
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
    public const int DefaultDashSpecialDamage = 40;
    public const float DefaultDashSpecialSpeed = 10;
    public const float DefaultDashSpecialMovementSeconds = 0.25f;
    public const float DefaultDashSpecialAttackSeconds = 0.4f;
    public const float DefaultDashSpecialCooldownSeconds = 0.75f;
    public const float DefaultDashSpecialHitDelaySeconds = 0.1f;
    public static int DashSpecialDamage => s_slideSettings.DashSpecialDamage;
    public static float DashSpecialSpeed => s_slideSettings.DashSpecialSpeed;
    public static float DashSpecialMovementSeconds => s_slideSettings.DashSpecialMovementSeconds;
    public static float DashSpecialAttackSeconds => s_slideSettings.DashSpecialAttackSeconds;
    public static float DashSpecialCooldownSeconds => s_slideSettings.DashSpecialCooldownSeconds;
    public static float DashSpecialHitDelaySeconds => s_slideSettings.DashSpecialHitDelaySeconds;
    public const int DefaultUppercutDamage = 36;
    public const float DefaultUppercutDirectionWindowSeconds = 0.9f;
    public const float DefaultUppercutButtonWindowSeconds = 0.6f;
    public const float DefaultUppercutAttackSeconds = 0.45f;
    public const float DefaultUppercutCooldownSeconds = 0.7f;
    public const float DefaultUppercutHitDelaySeconds = 0.1f;
    public const float DefaultUppercutReach = 1.8f;
    public static int UppercutDamage => s_slideSettings.UppercutDamage;
    public static float UppercutDirectionWindowSeconds => s_slideSettings.UppercutDirectionWindowSeconds;
    public static float UppercutButtonWindowSeconds => s_slideSettings.UppercutButtonWindowSeconds;
    public static float UppercutAttackSeconds => s_slideSettings.UppercutAttackSeconds;
    public static float UppercutCooldownSeconds => s_slideSettings.UppercutCooldownSeconds;
    public static float UppercutHitDelaySeconds => s_slideSettings.UppercutHitDelaySeconds;
    public static float UppercutReach => s_slideSettings.UppercutReach;

    // 大斬り・突き飛ばしの既定値とCSVから読み込んだ値。
    public const int DefaultHeavyStrikeDamage = 34;
    public static int HeavyStrikeDamage => s_slideSettings.HeavyStrikeDamage;
    public const float DefaultHeavyStrikeAttackSeconds = 0.5f;
    public static float HeavyStrikeAttackSeconds => s_slideSettings.HeavyStrikeAttackSeconds;
    public const float DefaultHeavyStrikeCooldownSeconds = 0.7f;
    public static float HeavyStrikeCooldownSeconds => s_slideSettings.HeavyStrikeCooldownSeconds;
    public const float DefaultHeavyStrikeHitDelaySeconds = 0.18f;
    public static float HeavyStrikeHitDelaySeconds => s_slideSettings.HeavyStrikeHitDelaySeconds;
    public const float DefaultHeavyStrikeReach = 2.8f;
    public static float HeavyStrikeReach => s_slideSettings.HeavyStrikeReach;
    public const int DefaultPushDamage = 10;
    public static int PushDamage => s_slideSettings.PushDamage;
    public const float DefaultPushHoldSeconds = 0.35f;
    public static float PushHoldSeconds => s_slideSettings.PushHoldSeconds;
    public const float DefaultPushAttackSeconds = 0.3f;
    public static float PushAttackSeconds => s_slideSettings.PushAttackSeconds;
    public const float DefaultPushCooldownSeconds = 0.5f;
    public static float PushCooldownSeconds => s_slideSettings.PushCooldownSeconds;
    public const float DefaultPushHitDelaySeconds = 0.1f;
    public static float PushHitDelaySeconds => s_slideSettings.PushHitDelaySeconds;
    public const float DefaultPushReach = 1.8f;
    public static float PushReach => s_slideSettings.PushReach;
    public const float DefaultPushKnockbackSpeed = 12;
    public static float PushKnockbackSpeed => s_slideSettings.PushKnockbackSpeed;

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
    // バックステップのボタン間隔。走りの2回入力とは独立して調整します。
    public const float BackstepInputWindowSeconds = 0.25f;
    public const float FacingInputThreshold = 0.01f;
    // パッドの奥行き操作に混じる微小な左右入力で、意図せず反転するのを抑えます。
    public const float PlayerFacingInputThreshold = 0.2f;

    // 移動速度（Unity単位/秒）・行動時間（秒）。
    // シーフの基本性能（試作用）。専用アクションの性能とは分けて管理します。
    // ステージ端を壁として扱う三角跳びの試作調整値。
    public const int ThiefWallJumps = 1;
    public const float ThiefWallJumpMargin = 0.45f;
    public const float ThiefWallJumpVelocity = 7f;
    public const float ThiefWallJumpDriftSpeed = 8f;
    public const int ThiefExtraAirJumps = 1;
    public const float ThiefAirJumpVelocity = 7f;
    public const float ThiefWalkSpeed = 6f;
    public const float ThiefRunSpeed = 9.5f;
    public const float ThiefNormalAttackReach = 1.8f;
    public const float ThiefWeaponReachMultiplier = 0.75f;
    public const float DaggerWidth = 0.1f;
    public const float DaggerLength = 0.45f;

    // ドワーフの初期調整値。職業差はここから調整し、既存職の性能を変えません。
    public const float DwarfWalkSpeed = 4.5f;
    public const float DwarfRunSpeed = 7f;
    public const float DwarfNormalAttackReach = 2.2f;
    public const float DwarfWeaponReachMultiplier = 0.9f;
    public const float DwarfModelHeightScale = 0.8f;
    public const float DwarfNormalAttackArmAngle = 120f;
    public const float NormalAttackArmAngle = 90f;
    public const float AxeHandleWidth = 0.12f;
    public const float AxeHandleLength = 0.75f;
    public const float AxeBladeWidth = 0.5f;
    public const float AxeBladeHeight = 0.35f;
    public const float AxeBladeOffset = 0.2f;
    public const float SwordWidth = 0.12f;
    public const float SwordLength = 0.9f;
    public const float ClassButtonWidth = 170f;
    public const float ClassButtonHeight = 28f;
    public const float ClassPanelMargin = 12f;

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
    // 大斬りも方向を先に入れてから押す技です。方向コマンドと同じ余裕を確保します。
    public static float HeavyStrikeButtonWindowSeconds => SlideButtonWindowSeconds;
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
    // コマンド技の基礎威力に適用する共通倍率。端数は切り上げます。
    public const float CriticalDamageMultiplier = 1.5f;
    public const int KnockdownDamageThreshold = 30;
    public const float KnockbackSpeed = 5;
    public const float StrongKnockbackSpeed = 9;
    public const float KnockbackDecay = 12;
    public const float BlockKnockbackSpeed = 1.5f;
    public const float BlockFlashSeconds = 0.15f;
    public const float CounterWindowSeconds = 0.45f;
    // ガード解除後の無敵時間（試作の調整値）。対空反撃への移行にも適用します。
    public const float GuardReleaseInvulnerabilitySeconds = 0.2f;
    public const float DamageFlashSeconds = 0.12f;
    public const float EnemyRemovalSeconds = 0.5f;
    public const float StageHalfWidth = 22;
    public const float StageHalfDepth = 3;
    public const float HeavySpeed = 1.3f;
    public const float HeavyWindupSeconds = 0.95f;
    public const float HeavyAttackIntervalSeconds = 1.8f;
    public const int HeavyDamage = 24;
    public const int HealingAmount = 30;
    // ターニングアンデッドの試作用設定。HPや魔法回数は消費せず、再使用時間で連打を制限します。
    public const int TurnUndeadDamage = 40;
    public const float TurnUndeadRadius = 4f;
    public const float TurnUndeadRecoverySeconds = 0.6f;
    public const float TurnUndeadCooldownSeconds = 4f;
    public const int UndeadSpawnIndex = 1;
    public const int HealingUses = 3;
    // 最終ウェーブのボス。狭く長い一撃と、短く幅広い攻撃を交互に使います。
    public const int BossHealth = 320;
    public const float BossSpeed = 2f;
    public const float BossStrikeReach = 2.4f;
    public const float BossStrikeLaneHalfWidth = 0.75f;
    public const float BossStrikeStopDistance = 2f;
    public const float BossStrikeWindupSeconds = 0.9f;
    public const int BossStrikeDamage = 22;
    public const float BossWideReach = 1.8f;
    public const float BossWideLaneHalfWidth = 1.4f;
    public const float BossWideStopDistance = 1.5f;
    public const float BossWideWindupSeconds = 0.7f;
    public const int BossWideDamage = 18;
    public const float BossApproachLaneTolerance = 0.5f;
    public const float BossRecoverySeconds = 1.4f;
    public const float BossAttackIntervalSeconds = 0.3f;
    // 後半も予兆時間・反撃の隙・攻撃力は共通です。追跡と攻撃間の待機だけを調整します。
    public const float BossSecondPhaseHealthFraction = 0.5f;
    public const float BossSecondPhaseSpeed = 2.4f;
    public const float BossSecondPhaseAttackIntervalSeconds = 0.1f;
    public const float BossPhaseNoticeSeconds = 2f;
    public static readonly UnityEngine.Color BossSecondPhaseColor = new UnityEngine.Color(1f, 0.35f, 0.2f);
    public const int BossWaveEnemyCount = 1;
    public const int BossPreparationHealingAmount = 40;
    public const int BossPreparationSupplyCount = 5;
    public const int BossPreparationSpellSupplyOffset = 2;
    public const float BossPreparationSupplySpacing = 1.6f;
    public const float BossPreparationHudTop = 370f;
    public const float BossPreparationHudWidth = 1050f;
    public const float BossPreparationHudLineHeight = 35f;
    public const float BossHudWidth = 320f;
    public const float BossHudMargin = 24f;
    public const float BossHudTitleHeight = 36f;
    public const float BossHudBarHeight = 22f;
    public const float BossMaceSize = 0.65f;
    // 予兆の種類は色と範囲の形で区別します。開始から発動まで同じ色系統で濃くします。
    public static readonly UnityEngine.Color BossStrikeWarningStartColor = new UnityEngine.Color(1f, 0.7f, 0.15f);
    public static readonly UnityEngine.Color BossStrikeWarningEndColor = new UnityEngine.Color(1f, 0.25f, 0f);
    public static readonly UnityEngine.Color BossWideWarningStartColor = new UnityEngine.Color(0.85f, 0.5f, 1f);
    public static readonly UnityEngine.Color BossWideWarningEndColor = new UnityEngine.Color(0.65f, 0.1f, 1f);
    public static readonly UnityEngine.Color BossOpeningColor = UnityEngine.Color.green;
    public const float OpeningMarkerHalfSize = 0.65f;
    public const float BossOpeningBarHeight = 8f;
    // 撃破演出は実時間で計測し、低速再生でも必ず終了するようにします。
    public const float BossDefeatSlowTimeScale = 0.25f;
    public const float BossDefeatSlowSeconds = 0.6f;
    public const float BossDefeatNoticeSeconds = 1.2f;
    public const float BossDefeatNoticeWidth = 640f;
    public const float BossDefeatNoticeHeight = 80f;
    public const float BossDefeatNoticeTopFraction = 0.35f;
    public const int BossDefeatNoticeFontSize = 42;
    public const int ProtectionUses = 2;
    public const int AttackBoostUses = 2;
    public const int InventorySlotCount = 6;
    public const int InventoryStackLimit = 9;
    public const float AttackBoostSeconds = 13f;
    public const float AttackBoostDamageMultiplier = 1.5f;
    public const float AttackBoostWarningSeconds = 3f;
    public const int ScrollRestoreUses = 1;
    public const int ScrollDropInterval = 4;
    public const int ScrollActionCycleLength = 3;
    public const float ScrollPickupRadius = 0.7f;
    public const float ScrollLifetimeSeconds = 20f;
    public const float ScrollLaneOffset = -0.8f;
    public const float ScrollModelHeight = 0.25f;
    public const float ScrollModelWidth = 0.6f;
    public const float ScrollModelDepth = 0.3f;
    public const float ScrollBandWidth = 0.12f;
    public const float ScrollBandHeight = 0.27f;
    public const float ScrollBandDepth = 0.32f;
    public const float SpellRecoverySeconds = 0.6f;
    public const float ProtectionSeconds = 8;
    public const float ProtectionDamageMultiplier = 0.5f;
    public const int PotionHealingAmount = 20;
    // 有限の所持品。射程は時間ではなく飛んだ距離で制限します。
    // ハンマーの軌道・気絶は試作用の調整値です。通常の転倒や被弾硬直とは区別します。
    // 大きな弧を描き、0.5秒で発射位置から3.0上昇します。着地までは約1.09秒です。
    // 動画を参考にした試作値で、映像から実測した数値ではありません。
    public const float HammerVerticalSpeed = 12f;
    public const float HammerGravity = 24f;
    public const int LargeEnemyHammerHitLimit = 2;
    public const float LargeEnemyHammerBodyRadius = 0.45f;
    public const float HammerRepeatHitIntervalSeconds = 0.1f;
    public const float ThrownProjectileCollisionStepSeconds = 0.025f;
    // 胴体の中心から上下への命中範囲。弧の上昇区間でも頭・胴体に当たるようにします。
    public const float HammerTargetVerticalHalfHeight = 0.6f;
    public const float HammerSpinDegreesPerSecond = 720f;
    public const float HammerStunSeconds = 1.5f;
    public const float HammerStunLabelWidth = 90f;
    public const float HammerStunLabelHeight = 35f;
    public const float HammerHeadWidth = 0.4f;
    public const float HammerHeadHeight = 0.24f;
    public const float HammerHeadDepth = 0.24f;
    public const float HammerHandleLength = 0.5f;
    public const float HammerHandleThickness = 0.1f;
    public const float HammerHandleOffset = -0.3f;
    public const float HammerPickupHeight = 0.2f;
    public const float HammerPickupLaneOffset = 0.4f;
    // 所持上限は共通の9。射程・使用後の隙などの挙動は種類ごとに調整できます。
    public const int KnifeCapacity = InventoryStackLimit;
    public const int KnifeDamage = 24;
    public const int KnifeDropInterval = 2;
    public const float KnifeSpeed = 12f;
    public const float KnifeRange = 9f;
    public const float KnifeRecoverySeconds = 0.35f;
    public const float KnifePickupRadius = 0.7f;
    public const float KnifePickupLifetimeSeconds = 20f;
    public const float KnifeHitRadius = 0.45f;
    public const float KnifeLaunchOffset = 0.7f;
    public const float KnifeHeight = 1.2f;
    public const float KnifeBladeLength = 0.65f;
    public const float KnifeBladeThickness = 0.08f;
    public const float KnifeBladeDepth = 0.12f;
    public const float KnifeGripLength = 0.2f;
    public const float KnifeGripThickness = 0.12f;
    public const float KnifeGripDepth = 0.14f;
    public const float KnifeGripOffset = 0.4f;
    public const float KnifePickupHeight = 0.2f;
    public const float KnifePickupLaneOffset = -0.4f;
    public const int HammerCapacity = InventoryStackLimit;
    public const int HammerDamage = 24;
    public const int HammerDropInterval = 2;
    public const float HammerSpeed = 9f;
    public const float HammerRecoverySeconds = 0.35f;
    public const float HammerPickupRadius = 0.7f;
    public const float HammerPickupLifetimeSeconds = 20f;
    public const float HammerHitRadius = 0.45f;
    public const float HammerLaunchOffset = 0.7f;
    public const float HammerHeight = 1.2f;
    // 所持上限と使用後の隙は、将来の外部データ調整でも同じ窓口を使います。
    public const int PotionCapacity = InventoryStackLimit;
    public const float PotionRecoverySeconds = 0.6f;
    public const float PotionPickupRadius = 0.7f;
    public const float PotionLifetimeSeconds = 20;
}
