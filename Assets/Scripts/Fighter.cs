using UnityEngine;

// キャラクター単体の管理役。入力または AI、移動、攻撃演出、被ダメージを担当します。
public class Fighter : Combatant
{
    // 敵の属性は派生クラスが宣言します。見た目やクラス名で魔法の対象を判定しません。
    public virtual bool IsUndead => false;
    public virtual bool CanBeBackstabbed => !IsPlayer;
    public bool IsBackstabbing => _attackKind == BrawlerDefines.AttackKind.Backstab && _attackRemaining > 0;
    private Fighter _backstabTarget;

    // 近接予告の範囲。遠距離型は弾の射線へ上書きし、表示側に敵の型判定を持たせません。
    public virtual float WarningRangeStart => -BrawlerBalance.AttackBehindAllowance;
    public virtual float MeleeAttackReach => BrawlerBalance.EnemyAttackReach;
    public virtual float MeleeLaneHalfWidth => BrawlerBalance.AttackLaneHalfWidth;
    public virtual float WarningRangeEnd => MeleeAttackReach;
    public virtual float WarningLaneHalfWidth => BrawlerBalance.AttackLaneHalfWidth;
    // 予兆の配色と反撃の隙は派生クラスが宣言し、表示側は具体的な職業・敵種別を判定しません。
    public virtual Color WarningStartColor => Color.yellow;
    public virtual Color WarningEndColor => Color.red;
    public virtual float OpeningSecondsRemaining => 0f;
    // 効果音も派生クラスで選び、予兆・攻撃開始の再生タイミングは共通化します。
    protected virtual BrawlerSound EnemyWarningSound => BrawlerSound.EnemyWarning;
    protected virtual BrawlerSound EnemySwingSound => BrawlerSound.EnemySwing;
    public virtual BrawlerSound EnemyMissSound => BrawlerSound.EnemyMiss;
    private BeltBrawler _game;
    // 敵ごとに独立した残像を保持します。計算はプレイヤーと同じ表示専用クラスを共用します。
    private readonly HealthBarFeedback _enemyHealthBarFeedback = new HealthBarFeedback();
    public float EnemyHealthTrail => _enemyHealthBarFeedback.TrailHealth;
    // 身長と通常攻撃の振り角だけを職業で変更できます。判定用の親Transformは拡縮しません。
    protected virtual float ModelHeightScale => 1f;
    protected virtual float NormalAttackArmAngle => BrawlerBalance.NormalAttackArmAngle;
    private Transform _visual;
    private Transform _attackArm;
    private Transform _rightLeg;
    private Transform _leftLeg;

    // _attackRemaining = 攻撃演出の残り秒数、_attackCooldown = 次の攻撃までの待ち時間。
    // _stunRemaining = 被ダメージによる硬直の残り秒数、_lastAttackTime = 前回攻撃を開始した時刻。
    // _walkPhase = 足を振るための位相。_lastAttackTime の初期値は最初の攻撃を 1 段目にするための値です。
    private float _attackRemaining;
    private float _attackCooldown;
    private float _stunRemaining;
    private float _hammerStunRemaining;

    // 通常の被弾硬直と区別し、HUDに気絶中の表示を出すための読み取り専用状態です。
    public bool IsHammerStunned => Health > 0 && _hammerStunRemaining > 0;
    private float _lastAttackTime = BrawlerDefines.UnsetTime;
    private float _walkPhase;
    // 現在のコンボ段数（1～3）。
    private int _comboStep;
    // 1 回の攻撃でダメージ判定を繰り返さないためのフラグ。
    private bool _hasDealtHit;
    private bool _canContinueCombo;
    private float _comboHitAt = BrawlerDefines.UnsetTime;
    private float _comboHitFacing;
    private readonly MeleeHitHistory _meleeHitHistory = new MeleeHitHistory();
    // 同時に複数の技が有効にならないよう、種類は一つの値で保持します。
    private BrawlerDefines.AttackKind _attackKind;
    // 被ダメージ時に押し戻される速度。
    private Vector3 _knockbackVelocity;
    // ダッシュ中だけ入力方向に高速移動し、ダメージを無効にします。
    // 再使用の待ち時間を設け、回避を連打するだけにならないようにしています。
    private float _dashRemaining;
    private float _dashCooldown;
    private float _windupRemaining;
    private float _windupDuration;
    private float _flashRemaining;
    private Vector3 _dashDirection;
    private float _attackBufferRemaining;
    private float _attackBufferFacing = 1;
    private bool _attackBufferForward;
    private bool _canPushFromHold;

    public bool IsHeavyStriking => _attackKind == BrawlerDefines.AttackKind.HeavyStrike && _attackRemaining > 0;
    public bool IsPushAttacking => _attackKind == BrawlerDefines.AttackKind.Push && _attackRemaining > 0;
    private Renderer[] _bodyRenderers;
    private MaterialPropertyBlock _flashProperties;
    private float _downRemaining;
    private float _blockFlashRemaining;
    public bool IsUppercutAttacking => _attackKind == BrawlerDefines.AttackKind.Uppercut && _attackRemaining > 0;
    public bool IsDashSpecialAttacking => _attackKind == BrawlerDefines.AttackKind.DashSpecial && _attackRemaining > 0;
    private float _dashAttackMovementRemaining;
    // 防御成功から反撃できる残り時間。攻撃を一度出したら消費します。
    private float _counterWindowRemaining;
    // 後ろ＋攻撃で構えた盾は、攻撃ボタンを離すまで維持します。
    // 前に入れ直すと向きを維持したまま対空攻撃へ移ります。
    private bool _commandGuardHeld;
    // ガードを解除した直後の保護時間。ゲーム時間なのでヒットストップ中は減りません。
    private float _guardReleaseRemaining;
    private Fighter _groundAttackTarget;
    private bool _hasReceivedGroundHit;
    private float _jumpHeight;
    private float _jumpVelocity;
    private int _airJumpsRemaining;
    private int _wallJumpsRemaining;
    protected virtual int WallJumpCount => 0;
    protected virtual float WallJumpMargin => 0f;
    protected virtual float WallJumpVelocity => BrawlerBalance.JumpVelocity;
    protected virtual float WallJumpDriftSpeed => BrawlerBalance.JumpDriftSpeed;
    protected virtual int ExtraAirJumpCount => 0;
    protected virtual float AirJumpVelocity => BrawlerBalance.JumpVelocity;
    public int AirJumpsRemaining => IsAirborne ? _airJumpsRemaining : 0;
    private Vector3 _jumpDrift;
    private bool _isCrouching;
    private float _lastJumpPressedAt = BrawlerDefines.UnsetTime;
    private bool _canBackstepFromJump;
    // 攻撃・選択・使用の押下履歴。ジャンプと同じ入力更新で押した場合は成立させません。
    private readonly ButtonSequenceBuffer _backstepButtons = new ButtonSequenceBuffer();
    private bool _isBackstep;
    private bool _isRunning;
    private float _runDirection;
    private float _lastForwardTapAt = BrawlerDefines.UnsetTime;
    private float _lastForwardTapDirection;
    private bool _wasForwardHeld;
    private readonly System.Collections.Generic.HashSet<Fighter> _bodyCheckedEnemies = new System.Collections.Generic.HashSet<Fighter>();

    public bool IsRunning => _isRunning;
    private readonly DirectionCommandBuffer _directionCommands = new DirectionCommandBuffer();
    // 入力を読み取った順番。Time.timeが停止していても、方向とボタンの前後関係を区別します。
    private int _inputSample;
    private readonly System.Collections.Generic.HashSet<Fighter> _slideHitEnemies = new System.Collections.Generic.HashSet<Fighter>();
    private float _slideRemaining;
    private float _slideDirection;
    private float _slideElapsed;

    public bool IsSliding => _slideRemaining > 0;

    public bool IsCrouching => _isCrouching;

    public bool IsCrouchAttacking => _attackKind == BrawlerDefines.AttackKind.Crouch && _attackRemaining > 0;

    public bool IsBackstepping => _isBackstep && _dashRemaining > 0;

    // 攻撃距離の基準位置は床に残し、見た目の高さだけ別に管理します。
    public float JumpHeight => _jumpHeight;

    public bool IsAirborne => _jumpHeight > 0 || _jumpVelocity > 0;

    public bool IsAirAttacking => (_attackKind == BrawlerDefines.AttackKind.Air || _attackKind == BrawlerDefines.AttackKind.DownThrust) && _attackRemaining > 0;

    public bool IsGroundAttacking => _attackKind == BrawlerDefines.AttackKind.Ground && _attackRemaining > 0;

    // 通常の硬直無敵とは別に、1回のダウンにつき追撃を一度だけ許可します。
    public bool CanReceiveGroundHit => !IsPlayer && Health > 0 && IsDowned && !_hasReceivedGroundHit;

    // ガードは前方だけ有効です。向きを固定して守るため、背後は位置取りで対処します。
    public bool IsGuarding { get; private set; }

    // 盾はModelの子なので、左向きへの反転や構えの位置を含めた実際の位置を返します。
    public Vector3 GuardImpactPosition => _visual.Find("Shield") is Transform shield
        ? shield.position : transform.position;

    public bool IsDowned => _downRemaining > 0;

    public int ComboStep => _comboStep;

    public bool IsDashAttacking => (_attackKind == BrawlerDefines.AttackKind.Dash || _attackKind == BrawlerDefines.AttackKind.DashSpecial) && _attackRemaining > 0;

    public bool CanCounter => _counterWindowRemaining > 0 && _stunRemaining <= 0
        && _attackRemaining <= 0 && _dashRemaining <= 0 && Health > 0;

    public bool IsCounterAttacking => _attackKind == BrawlerDefines.AttackKind.Counter && _attackRemaining > 0;

    // 状態の読み取りは公開し、変更はこのクラスのメソッドを通して行います。
    // private set により、他クラスが HP や向きを直接書き換えることを防ぎます。
    public bool IsPlayer { get; private set; }

    protected override void OnHealthRestored(int actualAmount)
    {
        if (!IsPlayer)
        {
            _enemyHealthBarFeedback.Refresh(Health, MaxHealth, 0);
        }
        // 回復魔法・ポーション・ウェーブ報酬が通る共通の通知で、重複表示を防ぎます。
        if (IsPlayer && _game != null)
        {
            _game.ShowHealingNumber(this, actualAmount);
        }
    }

    // 所持品と魔法は職業共通の6枠に保存し、種類ごとの数を独立して管理します。
    public ActionInventory Inventory { get; } = new ActionInventory();
    public int HealingPotionsHeld
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.HealingPotion);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.HealingPotion, value);
    }

    public int ThrowingHammersHeld
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.ThrowingHammer);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.ThrowingHammer, value);
    }

    // 魔法を使わない職業はスクロールを取得しません。魔法職は種類ごとの補充を上書きします。
    public virtual bool TryRestoreSpellUses(BrawlerDefines.UsableAction action, int amount) => false;
    // 武器による近接攻撃だけに適用します。魔法・投擲・移動接触の威力とは分離します。
    public virtual int CalculateMeleeDamage(int amount) => amount;

    // コマンド技だけを確定クリティカルにします。通常攻撃やアイテムには乱数補正を追加しません。
    public bool IsCriticalAttack => IsPlayer && (_attackKind == BrawlerDefines.AttackKind.HeavyStrike
        || _attackKind == BrawlerDefines.AttackKind.DashSpecial || _attackKind == BrawlerDefines.AttackKind.Uppercut);

    public int CalculateMeleeHitDamage(int baseDamage)
    {
        // クリティカル→職業の攻撃強化の順で各補正を一度だけ適用します。
        int damage = IsCriticalAttack ? Mathf.CeilToInt(Mathf.Max(0, baseDamage)
            * BrawlerBalance.CriticalDamageMultiplier) : Mathf.Max(0, baseDamage);
        return CalculateMeleeDamage(damage);
    }

    public int ThrowingKnivesHeld
    {
        get => Inventory.GetCount(BrawlerDefines.UsableAction.ThrowingKnife);
        private set => Inventory.SetCount(BrawlerDefines.UsableAction.ThrowingKnife, value);
    }
    private float _itemRecoveryRemaining;

    // 他職業はこの許可判定を上書きします。所持品の処理に具体的な職業名は持ち込みません。
    // 派生クラスは設定の選択だけを変更し、移動や命中判定を複製しません。
    public virtual PlayerClassSettings ClassSettings => PlayerClassCatalog.Common;
    public float AttackReach => IsPlayer ? ClassSettings.GetAttackReach(_attackKind) : MeleeAttackReach;
    public float GroundAttackReach => ClassSettings.GetAttackReach(BrawlerDefines.AttackKind.Ground);

    public virtual bool CanUseAction(BrawlerDefines.UsableAction action)
    {
        return ClassSettings.CanUseAction(action);
    }

    public bool TryStoreThrowingKnife()
    {
        if (!CanUseAction(BrawlerDefines.UsableAction.ThrowingKnife) || !IsPlayer || Health <= 0
            || Game.IsRunFinished || ThrowingKnivesHeld >= BrawlerBalance.KnifeCapacity)
        {
            return false;
        }
        return Inventory.TryAdd(BrawlerDefines.UsableAction.ThrowingKnife);
    }

    public bool TryThrowKnife()
    {
        if (!CanUseAction(BrawlerDefines.UsableAction.ThrowingKnife) || !CanUseSpecialAction
            || ThrowingKnivesHeld <= 0)
        {
            return false;
        }
        Game.ThrowKnife(this);
        Inventory.TryConsume(BrawlerDefines.UsableAction.ThrowingKnife);
        _itemRecoveryRemaining = BrawlerBalance.KnifeRecoverySeconds;
        ReleaseDefenseForSpecialAction();
        return true;
    }

    // アイテムの消費・硬直は共通処理へ集約し、職業ごとの入力側で複製しません。
    protected bool TryConsumeThrowingHammerWithRecovery()
    {
        if (!TryConsumeThrowingHammer())
        {
            return false;
        }
        _itemRecoveryRemaining = BrawlerBalance.HammerRecoverySeconds;
        return true;
    }

    protected bool TryConsumeHealingPotionWithRecovery()
    {
        if (!TryConsumeHealingPotion())
        {
            return false;
        }
        _itemRecoveryRemaining = BrawlerBalance.PotionRecoverySeconds;
        return true;
    }

    public void NotifyItemPickup(BrawlerDefines.PickupKind kind)
    {
        if (IsPlayer && _game != null)
        {
            _game.ShowPickupNotice(this, kind);
        }
    }

    public bool TryStoreThrowingHammer()
    {
        if (!CanUseAction(BrawlerDefines.UsableAction.ThrowingHammer) || !IsPlayer || Health <= 0 || _game.IsRunFinished || ThrowingHammersHeld >= BrawlerBalance.HammerCapacity)
        {
            return false;
        }
        return Inventory.TryAdd(BrawlerDefines.UsableAction.ThrowingHammer);
    }

    protected bool TryConsumeThrowingHammer()
    {
        // 発射時の向きを使い、後から振り向いても弾の方向は変えません。
        if (!CanUseAction(BrawlerDefines.UsableAction.ThrowingHammer) || !IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction || ThrowingHammersHeld <= 0)
        {
            return false;
        }
        Game.ThrowHammer(this);
        Inventory.TryConsume(BrawlerDefines.UsableAction.ThrowingHammer);
        ReleaseDefenseForSpecialAction();
        return true;
    }

    public bool TryStoreHealingPotion()
    {
        // HP満タンでも保存できます。死亡・所持上限では床のポーションを残します。
        if (!IsPlayer || Health <= 0 || _game.IsRunFinished || HealingPotionsHeld >= BrawlerBalance.PotionCapacity)
        {
            return false;
        }
        return Inventory.TryAdd(BrawlerDefines.UsableAction.HealingPotion);
    }

    protected bool TryConsumeHealingPotion()
    {
        // 不成立時は消費しません。最大HPでの切り止めは共通の回復処理に任せます。
        if (!IsPlayer || !CanUseSpecialAction || IsUsingSpecialAction
            || HealingPotionsHeld <= 0 || Health >= MaxHealth)
        {
            return false;
        }
        RestoreHealth(BrawlerBalance.PotionHealingAmount);
        Inventory.TryConsume(BrawlerDefines.UsableAction.HealingPotion);
        Game.PlaySound(BrawlerSound.Heal);
        ReleaseDefenseForSpecialAction();
        return true;
    }

    // 右向きは +1、左向きは -1。見た目と攻撃判定で共用します。
    public float Facing { get; protected set; } = 1;

    // 派生クラスには必要な参照と判断だけを公開し、タイマー本体は非公開に保ちます。
    protected BeltBrawler Game => _game;
    public bool IsSpellEffectActive => _game != null && _game.IsSpellEffectActive;
    public bool IsPausedBySpellEffect => !IsPlayer && IsSpellEffectActive;

    protected bool CanStartEnemyAttack => _attackCooldown <= 0;

    protected virtual float EnemyAttackInterval => BrawlerBalance.MeleeAttackIntervalSeconds;

    // 敵の種類ごとの差は派生クラスで上書きし、近接 AI と命中処理は共用します。
    protected virtual float EnemyMovementSpeed => BrawlerBalance.MeleeSpeed;

    protected virtual float EnemyWindupDuration => BrawlerBalance.MeleeWindupSeconds;

    protected virtual int EnemyAttackDamage => BrawlerBalance.MeleeDamage;

    // 職業固有の行動は派生クラスが担当し、共通の戦闘状態はここで管理します。
    protected virtual bool IsUsingSpecialAction => _itemRecoveryRemaining > 0;

    protected virtual Color? SpecialFeedbackColor => null;

    // 固有行動の構えだけを差し替えます。通常攻撃の命中処理は変更しません。
    protected virtual float? SpecialActionArmAngle => null;

    // 実際にHPが減った場合だけ通知します。防御・無敵による不成立では呼びません。
    protected virtual void OnDamageReceived(int actualAmount)
    {
    }

    // 発動とUIが同じ共通判定を使います。状態を変更せず、理由だけ返します。
    protected BrawlerDefines.ActionUseStatus CommonActionUseStatus
    {
        get
        {
            if (!IsPlayer)
            {
                return BrawlerDefines.ActionUseStatus.Unavailable;
            }
            if (Health <= 0)
            {
                return BrawlerDefines.ActionUseStatus.Defeated;
            }
            if (_game.IsRunFinished)
            {
                return BrawlerDefines.ActionUseStatus.Unavailable;
            }
            if (IsAirborne)
            {
                return BrawlerDefines.ActionUseStatus.Airborne;
            }
            if (_stunRemaining > 0 || _attackRemaining > 0 || _dashRemaining > 0
                || _attackCooldown > 0 || _isRunning || IsSliding || IsUsingSpecialAction)
            {
                return BrawlerDefines.ActionUseStatus.Busy;
            }
            return BrawlerDefines.ActionUseStatus.Ready;
        }
    }

    protected bool CanUseSpecialAction => CommonActionUseStatus == BrawlerDefines.ActionUseStatus.Ready;
    public bool IsReadyForStageTransition => CanUseSpecialAction;

    protected virtual bool UpdateSpecialAction() => false;

    // 防御成立・回避無敵の判定後に、職業固有のダメージ軽減だけを差し替えます。
    protected virtual int CalculateReceivedDamage(int amount) => amount;

    protected void ReleaseDefenseForSpecialAction()
    {
        ClearAttackHold();
        _commandGuardHeld = false;
        EndGuard();
        _counterWindowRemaining = 0;
        _attackBufferRemaining = 0;
        _comboStep = 0;
        _lastAttackTime = BrawlerDefines.UnsetTime;
    }

    public bool DashReady => _dashCooldown <= 0;

    public bool IsWindingUp => _windupRemaining > 0;

    // 敵ごとの予告秒数が異なっても、表示側には0～1の進み具合として渡します。
    public float AttackWindupProgress => IsWindingUp
        ? 1 - Mathf.Clamp01(_windupRemaining / Mathf.Max(_windupDuration, Mathf.Epsilon)) : 0;

    // 生成直後に管理役から一度だけ呼び、戦闘に必要な状態とパーツをまとめて渡します。
    // Start より前に初期化を完了するため、Start で安全に Renderer を取得できます。
    public void Initialize(BeltBrawler game, bool isPlayer, int maxHealth,
        Transform visual, Transform attackArm, Transform rightLeg, Transform leftLeg)
    {
        _game = game;
        IsPlayer = isPlayer;
        InitializeHealth(maxHealth);
        _enemyHealthBarFeedback.Refresh(Health, MaxHealth, 0);
        _visual = visual;
        _attackArm = attackArm;
        _rightLeg = rightLeg;
        _leftLeg = leftLeg;
        InitializeInventory();
    }

    protected virtual void InitializeInventory()
    {
        Inventory.Clear();
    }

    protected virtual void Start()
    {
        // Unity のネイティブ機能を使うオブジェクトはフィールド初期化では作らず、Start で作ります。
        _flashProperties = new MaterialPropertyBlock();
        // パーツ生成後に Renderer を集めます。素材を複製せず、個体ごとに色を上書きします。
        _bodyRenderers = _visual.GetComponentsInChildren<Renderer>();
    }

    protected virtual void Update()
    {
        // 倒れた後は移動や攻撃の更新を止めます。
        if (OptionsMenu.BlocksGameplay || Health <= 0 || _game.IsRunFinished || IsPausedBySpellEffect)
        {
            // AIだけでなく攻撃発生・硬直・ノックバック・アニメーションの時間も保存します。
            return;
        }
        float deltaTime = Time.deltaTime;
        if (!IsPlayer)
        {
            _enemyHealthBarFeedback.Refresh(Health, MaxHealth, deltaTime);
        }
        // 更新順もゲームの挙動の一部です。元の順序を維持し、担当する処理ごとに分けます。
        UpdateTimers(deltaTime);
        Vector3 input = ReadPlayerInput();
        UpdateJump(deltaTime);
        bool wasWindingUp = UpdateAttackWindup(deltaTime);
        ApplyKnockback(deltaTime);
        Vector3 movement = GetMovement(input, wasWindingUp);
        Move(movement, deltaTime);
        if (_isRunning)
        {
            _game.HitRunningTargets(this);
        }
        if (IsSliding)
        {
            _game.HitSlidingTargets(this);
        }
        UpdateWalkAnimation(movement, deltaTime);
        UpdateAttackAnimation(deltaTime);
        UpdateVisualFeedback();
    }

    // 残り時間は経過秒数で減らし、フレームレートが違っても同じ時間になるようにします。
    private void UpdateTimers(float deltaTime)
    {
        _guardReleaseRemaining = Mathf.Max(0, _guardReleaseRemaining - deltaTime);
        _itemRecoveryRemaining = Mathf.Max(0, _itemRecoveryRemaining - deltaTime);
        _attackCooldown -= deltaTime;
        _stunRemaining -= deltaTime;
        if (_stunRemaining <= 0)
        {
            // 起き上がり・通常硬直が終われば、次の攻撃では同じ技も再び命中できます。
            _meleeHitHistory.Reset();
        }
        _hammerStunRemaining = Mathf.Max(0, _hammerStunRemaining - deltaTime);
        _dashCooldown -= deltaTime;
        _flashRemaining -= deltaTime;
        _attackBufferRemaining = Mathf.Max(0, _attackBufferRemaining - deltaTime);
        _downRemaining = Mathf.Max(0, _downRemaining - deltaTime);
        _blockFlashRemaining = Mathf.Max(0, _blockFlashRemaining - deltaTime);
        _counterWindowRemaining = Mathf.Max(0, _counterWindowRemaining - deltaTime);
    }

    // 入力の読み取りと先行受付だけを担当し、通常移動に使う方向を返します。
    private Vector3 ReadPlayerInput()
    {
        Vector3 input = Vector3.zero;
        if (IsPlayer)
        {
            bool attackPressed = BrawlerInput.WasAttackPressed();
            bool jumpPressed = BrawlerInput.WasJumpPressed();
            bool backstepButtonPressed = attackPressed || BrawlerInput.WasActionSelectPressed()
                || BrawlerInput.WasActionUsePressed();
            bool backstepRequested = _backstepButtons.Record(backstepButtonPressed, jumpPressed,
                Time.time, BrawlerBalance.BackstepInputWindowSeconds);
            // 固有行動が成立したフレームは、同時押しの攻撃・回避より優先します。
            if (UpdateSpecialAction())
            {
                _isCrouching = false;
                return Vector3.zero;
            }
            input = BrawlerInput.ReadMovement();
            if (IsSliding)
            {
                ClearAttackHold();
                return Vector3.zero;
            }
            _inputSample++;
            _directionCommands.Record(input, Facing, Time.time, _inputSample);
            if (attackPressed && !jumpPressed && !IsAirborne && _stunRemaining <= 0
                && CanStartCommandAttack(BrawlerDefines.AttackKind.Uppercut)
                && _directionCommands.TryConsumeDownUp(input, Time.time, _inputSample))
            {
                Facing = GetInputFacing(input);
                StartUppercut();
                return Vector3.zero;
            }
            if (jumpPressed && !IsAirborne && _stunRemaining <= 0 && _attackRemaining <= 0
                && _dashRemaining <= 0 && _attackCooldown <= 0
                && _directionCommands.TryConsumeDownForward(input, Time.time, out float slideDirection, sample: _inputSample))
            {
                StartSlide(slideDirection);
                return Vector3.zero;
            }
            if (attackPressed && !jumpPressed && !IsAirborne && _stunRemaining <= 0
                && CanStartCommandAttack(BrawlerDefines.AttackKind.DashSpecial)
                && _directionCommands.TryConsumeDownForward(input, Time.time, out float specialDirection, requiresNeutral: true, sample: _inputSample))
            {
                StartDashSpecial(specialDirection);
                return Vector3.zero;
            }
            // 下入力は奥行き移動にも使うため、ジャンプボタンを保持した時だけしゃがみます。
            _isCrouching = !IsAirborne && _dashRemaining <= 0 && _stunRemaining <= 0
                && BrawlerInput.IsJumpHeld()
                && (_isCrouching ? input.z < -BrawlerBalance.JumpDirectionThreshold
                    : _directionCommands.IsDownHeldBeforeButton(Time.time, _inputSample));
            // 通常ジャンプの2回押し、または地上の別ボタン→ジャンプで後方回避します。
            // 空中攻撃や大ジャンプからの切り替えと、使用後の硬直の省略は許可しません。
            bool jumpBackstepRequested = jumpPressed && _canBackstepFromJump
                && Time.time - _lastJumpPressedAt <= BrawlerBalance.BackstepInputWindowSeconds;
            if ((jumpBackstepRequested || (backstepRequested && !IsAirborne))
                && input.z >= -BrawlerBalance.JumpDirectionThreshold && _stunRemaining <= 0
                && _dashRemaining <= 0 && !IsUsingSpecialAction && DashReady)
            {
                StartBackstep();
                return Vector3.zero;
            }
            // 三角跳びは通常の空中ジャンプより優先し、同じ押下で両方の回数を消費しません。
            if (jumpPressed && IsAirborne && TryStartWallJump())
            {
                return Vector3.zero;
            }
            // 先に通常ジャンプの2回押し回避と三角跳びを判定し、残る空中入力を追加ジャンプへ渡します。
            if (jumpPressed && IsAirborne && TryStartAirJump())
            {
                return input;
            }
            if (jumpPressed && !_isCrouching && !IsAirborne && _stunRemaining <= 0
                && _attackRemaining <= 0 && !IsUsingSpecialAction)
            {
                // 上＋ジャンプの同時入力や斜め上は通常ジャンプにします。
                Vector3 jumpInput = input;
                if (!_directionCommands.IsUpHeldBeforeButton(Time.time, _inputSample))
                {
                    jumpInput.z = 0;
                }
                StartJump(jumpInput);
            }
            if (IsAirborne)
            {
                _commandGuardHeld = false;
                ClearAttackHold();
                _commandGuardHeld = false;
                EndGuard();
                // 空中では攻撃ボタンを1回押して発動。奥行きの下入力＋攻撃で下突きに変化します。
                if (attackPressed && _attackRemaining <= 0 && _attackCooldown <= 0 && _stunRemaining <= 0)
                {
                    Facing = GetInputFacing(input);
                    StartAirAttack(input.z < -BrawlerBalance.JumpDirectionThreshold);
                }
                return input;
            }
            // ガードの向きを基準に判定し、前入力で振り向きや走りが先に発生しないようにします。
            if (IsGuarding && input.x * Facing > BrawlerBalance.ForwardInputThreshold
                && _stunRemaining <= 0 && _attackRemaining <= 0 && _attackCooldown <= 0
                && _dashRemaining <= 0 && !IsUsingSpecialAction)
            {
                StartUppercut();
                return Vector3.zero;
            }
            UpdateRunningInput(input);
            UpdateGuardInput(input);
            if (IsGuarding)
            {
                ClearAttackHold();
                _isRunning = false;
            }
            if (UpdateAttackHold(BrawlerInput.IsAttackHeld()))
            {
                return Vector3.zero;
            }
            if (attackPressed && _isRunning && _attackCooldown <= 0)
            {
                _dashDirection = new Vector3(_runDirection, 0, 0);
                _isRunning = false;
                StartDashAttack();
                return Vector3.zero;
            }
            bool heavyStrikeRequested = attackPressed && !jumpPressed
                && _directionCommands.TryConsumeForward(Facing, Time.time, _inputSample);
            // 前方向を先に入力してから攻撃を押した場合だけ、大斬りへキャンセルします。
            if (attackPressed && !jumpPressed && !IsGuarding
                && heavyStrikeRequested
                && CanCancelAttackTo(BrawlerDefines.AttackKind.HeavyStrike))
            {
                StartHeavyStrike();
                return Vector3.zero;
            }
            // 命中前の連打は予約しません。命中後の次段入力だけ0.2秒間覚えます。
            if (attackPressed && !IsGuarding && CanAcceptNormalAttackInput())
            {
                _attackBufferRemaining = BrawlerBalance.AttackBufferSeconds;
                // 先行入力はボタンを押した時点の向きも記憶します。方向を離しても失いません。
                _attackBufferFacing = GetInputFacing(input);
                _attackBufferForward = heavyStrikeRequested;
            }
            // 攻撃の後隙からも回避できますが、被ダメージ硬直中は回避できません。
            if (BrawlerInput.WasDashPressed() && DashReady && _stunRemaining <= 0)
            {
                // 回避距離はスティックの倒し具合によらず一定にするため、回避方向だけ正規化します。
                _dashDirection = input.sqrMagnitude > 0 ? input.normalized : new Vector3(Facing, 0, 0);
                ClearAttackHold();
                _dashRemaining = BrawlerBalance.DashSeconds;
                _isRunning = false;
                _isBackstep = false;
                _isCrouching = false;
                _dashCooldown = BrawlerBalance.DashCooldownSeconds;
                _attackRemaining = 0;
                ResetAttackKind();
                _counterWindowRemaining = 0;
                _dashAttackMovementRemaining = 0;
                _attackBufferRemaining = 0;
                _knockbackVelocity = Vector3.zero;
                _commandGuardHeld = false;
                EndGuard();
            }
            // 回避＋攻撃の同時押しも、回避中に攻撃を追加する操作も受け付けます。
            // 無敵の回避を攻撃へ変換するため、発動後は被ダメージを受けるようになります。
            if (attackPressed && _dashRemaining > 0 && _stunRemaining <= 0 && !_isBackstep)
            {
                StartDashAttack();
            }
            else if (attackPressed && CanCounter && _attackCooldown <= 0)
            {
                // ガードを押したままでも反撃可能。防御していた向きのまま攻撃します。
                StartCounterAttack();
            }
        }
        return input;
    }

    // 命中判定前はモーションを維持します。判定後から上位の技へ切り替えられます。
    // 必殺技同士はダッシュ必殺技→対空必殺技の一方通行とし、同じ技の連続キャンセルを防ぎます。
    private bool CanCancelAttackTo(BrawlerDefines.AttackKind nextKind)
    {
        if (!IsPlayer || Health <= 0 || Game.IsRunFinished || _stunRemaining > 0
            || IsAirborne || IsSliding || _dashRemaining > 0 || IsUsingSpecialAction || !_hasDealtHit)
        {
            return false;
        }
        bool isInRecovery = _attackRemaining > 0 || _attackCooldown > 0;
        if (!isInRecovery)
        {
            return false;
        }
        if (nextKind == BrawlerDefines.AttackKind.HeavyStrike)
        {
            return _attackKind == BrawlerDefines.AttackKind.Normal;
        }
        if (nextKind == BrawlerDefines.AttackKind.Uppercut && _attackKind == BrawlerDefines.AttackKind.DashSpecial)
        {
            return true;
        }
        return (nextKind == BrawlerDefines.AttackKind.DashSpecial || nextKind == BrawlerDefines.AttackKind.Uppercut)
            && (_attackKind == BrawlerDefines.AttackKind.Normal || _attackKind == BrawlerDefines.AttackKind.Dash
                || _attackKind == BrawlerDefines.AttackKind.HeavyStrike || _attackKind == BrawlerDefines.AttackKind.Push
                || _attackKind == BrawlerDefines.AttackKind.Counter || _attackKind == BrawlerDefines.AttackKind.Ground
                || _attackKind == BrawlerDefines.AttackKind.Crouch);
    }

    private bool CanStartCommandAttack(BrawlerDefines.AttackKind nextKind)
    {
        // 通常の発動条件と、後隙を省略するキャンセル条件を分けて読みやすくします。
        return _dashRemaining <= 0 && !IsUsingSpecialAction
            && ((_attackRemaining <= 0 && _attackCooldown <= 0) || CanCancelAttackTo(nextKind));
    }

    private bool CanAcceptNormalAttackInput()
    {
        if (_attackKind == BrawlerDefines.AttackKind.Normal && (_attackRemaining > 0 || _attackCooldown > 0))
        {
            return _canContinueCombo && _comboStep < BrawlerBalance.ComboSteps;
        }
        // 特殊技・必殺技の途中から通常攻撃を予約し、下位技へ戻すことはしません。
        return _attackRemaining <= 0 && _attackCooldown <= 0;
    }

    public void NotifyMeleeHit()
    {
        if (IsPlayer && _attackKind == BrawlerDefines.AttackKind.Normal)
        {
            // 当たった時刻を基準に次段の受付を開始します。範囲内でも無敵で拒否された場合は呼びません。
            _canContinueCombo = _comboStep < BrawlerBalance.ComboSteps;
            _comboHitAt = Time.time;
            _comboHitFacing = Facing;
            TryStartPushFromHit(BrawlerInput.IsAttackHeld());
        }
    }

    private void UpdateGuardInput(Vector3 input)
    {
        bool canGuard = _stunRemaining <= 0 && _attackRemaining <= 0
            && _dashRemaining <= 0 && !_isCrouching;
        bool attackHeld = BrawlerInput.IsAttackHeld();
        if (!canGuard || !attackHeld)
        {
            _commandGuardHeld = false;
        }
        else if (input.x * Facing < -BrawlerBalance.ForwardInputThreshold)
        {
            // 移動で向きが更新される前に判定し、後ろ入力で振り向くことを防ぎます。
            _commandGuardHeld = true;
        }
        bool guardRequested = canGuard && (BrawlerInput.IsGuardHeld() || _commandGuardHeld);
        if (!guardRequested)
        {
            EndGuard();
        }
        else
        {
            IsGuarding = true;
        }
    }

    private void EndGuard()
    {
        // 一度構えを解除した時だけ時間を設定し、待機中に延長し続けることを防ぎます。
        if (IsPlayer && IsGuarding)
        {
            _guardReleaseRemaining = BrawlerBalance.GuardReleaseInvulnerabilitySeconds;
        }
        IsGuarding = false;
    }

    private bool UpdateAttackWindup(float deltaTime)
    {
        // 敵は種類ごとの予告時間中に足を止め、攻撃方向を固定します。
        // プレイヤーが奥行き方向へ逃げれば、その場所に空振りさせられます。
        bool wasWindingUp = _windupRemaining > 0;
        if (wasWindingUp)
        {
            _windupRemaining -= deltaTime;
            if (_windupRemaining <= 0 && _game.Target.Health > 0)
            {
                Attack();
            }
        }
        // このフレーム開始時に予告中だったかを返し、予告終了フレームにも足を止めます。
        return wasWindingUp;
    }

    private void ApplyKnockback(float deltaTime)
    {
        // 速度に経過秒数を掛けて移動量へ変換し、ノックバック速度を徐々にゼロへ近づけます。
        transform.position += _knockbackVelocity * deltaTime;
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, deltaTime * BrawlerBalance.KnockbackDecay);
    }

    // プレイヤー入力か敵 AI のどちらかから、そのフレームの通常移動方向を決めます。
    private Vector3 GetMovement(Vector3 input, bool wasWindingUp)
    {
        if (IsSliding)
        {
            return Vector3.zero;
        }
        if (IsPlayer && IsAirborne)
        {
            // 空中攻撃中も方向入力で着地点を調整できます。被ダメージ時は操作を止めます。
            return _stunRemaining <= 0 ? input : Vector3.zero;
        }
        Vector3 movement = Vector3.zero;
        // 攻撃中・被ダメージ硬直中は、新しい操作や AI の行動を受け付けません。
        if (_stunRemaining <= 0 && _attackRemaining <= 0 && _dashRemaining <= 0 && !wasWindingUp && !IsUsingSpecialAction)
        {
            if (IsPlayer)
            {
                // ガード中は足を止め、向きを維持します。解除直後の勝手な攻撃も防ぎます。
                if (IsGuarding)
                {
                    _attackBufferRemaining = 0;
                    return Vector3.zero;
                }
                if (_isCrouching)
                {
                    if (_attackCooldown <= 0 && _attackBufferRemaining > 0)
                    {
                        Facing = _attackBufferFacing;
                        StartCrouchAttack();
                    }
                    return Vector3.zero;
                }
                // 入力の縦方向は Y ではなく Z に対応させます。
                // 入力側で長さを最大 1 に制限し、アナログの倒し具合も維持しています。
                movement = input;
                // 記憶した入力を一度消費して攻撃します。押しっぱなしで自動連打にはなりません。
                if (_attackCooldown <= 0 && _attackBufferRemaining > 0)
                {
                    // 反対方向＋攻撃を同時に入力したとき、攻撃開始前に向きを更新します。
                    // 攻撃開始後は向きを固定し、途中で判定だけが裏返らないようにします。
                    Facing = _attackBufferFacing;
                    Fighter groundTarget = _game.FindGroundAttackTarget(this);
                    if (groundTarget != null)
                    {
                        StartGroundAttack(groundTarget);
                    }
                    else if (_attackBufferForward)
                    {
                        StartHeavyStrike();
                    }
                    else
                    {
                        Attack();
                    }
                    _attackBufferRemaining = 0;
                    movement = Vector3.zero;
                }
            }
            else if (_game.Target.Health > 0)
            {
                movement = GetEnemyMovement();
            }
        }
        return movement;
    }

    // 近接型の標準 AI。遠距離型はこの判断だけを上書きし、硬直・転倒・演出は共用します。
    protected virtual Vector3 GetEnemyMovement()
    {
        Vector3 delta = _game.Target.transform.position - transform.position;
        Facing = delta.x >= 0 ? 1 : -1;
        if (Mathf.Abs(delta.x) > BrawlerBalance.MeleeStopDistance || Mathf.Abs(delta.z) > BrawlerBalance.MeleeLaneTolerance)
        {
            return delta.normalized;
        }
        if (CanStartEnemyAttack)
        {
            BeginAttackWindup(EnemyWindupDuration);
        }
        return Vector3.zero;
    }

    protected void BeginAttackWindup(float duration)
    {
        _windupDuration = duration;
        _windupRemaining = duration;
        if (!IsPlayer && Health > 0 && !Game.IsRunFinished && duration > 0)
        {
            Game.PlaySound(EnemyWarningSound);
        }
    }

    // 発射や直接攻撃など、実際の命中処理だけを派生クラスで差し替えられます。
    protected virtual void DealAttackDamage(int damage)
    {
        _game.Hit(this, damage);
    }

    private float GetInputFacing(Vector3 input)
    {
        return Mathf.Abs(input.x) > BrawlerBalance.PlayerFacingInputThreshold ? Mathf.Sign(input.x) : Facing;
    }

    private void Move(Vector3 movement, float deltaTime)
    {
        // 奥行きだけの移動では向きを維持し、左右に動いた場合だけ向きを変更します。
        float facingThreshold = IsPlayer ? BrawlerBalance.PlayerFacingInputThreshold : BrawlerBalance.FacingInputThreshold;
        // 空中では攻撃中も移動可能ですが、攻撃の向きは開始時のまま固定します。
        if (_attackRemaining <= 0 && Mathf.Abs(movement.x) > facingThreshold)
        {
            Facing = Mathf.Sign(movement.x);
        }
        transform.position += movement * (IsPlayer ? (_isRunning ? ClassSettings.RunSpeed : ClassSettings.WalkSpeed) : EnemyMovementSpeed) * deltaTime;
        if (IsAirborne)
        {
            transform.position += _jumpDrift * deltaTime;
        }
        // 最終フレームの移動量を残り時間で制限し、FPS が低いときの飛びすぎを防ぎます。
        if (_dashRemaining > 0)
        {
            transform.position += _dashDirection * BrawlerBalance.DashSpeed * Mathf.Min(deltaTime, _dashRemaining);
            _dashRemaining = Mathf.Max(0, _dashRemaining - deltaTime);
        }
        // 回避より速度を落とした短い踏み込みです。通常移動と同時には発生しません。
        if (_dashAttackMovementRemaining > 0)
        {
            float speed = _attackKind == BrawlerDefines.AttackKind.DashSpecial ? BrawlerBalance.DashSpecialSpeed : BrawlerBalance.DashAttackSpeed;
            transform.position += _dashDirection * speed * Mathf.Min(deltaTime, _dashAttackMovementRemaining);
            _dashAttackMovementRemaining = Mathf.Max(0, _dashAttackMovementRemaining - deltaTime);
        }
        Vector3 position = transform.position;
        if (IsSliding)
        {
            // 最短0.12秒、保持で最大0.4秒。離すと最短分だけ滑って終了します。
            if (!BrawlerInput.IsJumpHeld())
            {
                _slideRemaining = Mathf.Min(_slideRemaining, Mathf.Max(0, BrawlerBalance.SlideMinSeconds - _slideElapsed));
            }
            float travelTime = Mathf.Min(deltaTime, _slideRemaining);
            position.x += _slideDirection * BrawlerBalance.SlideSpeed * travelTime;
            _slideElapsed += travelTime;
            _slideRemaining = Mathf.Max(0, _slideRemaining - deltaTime);
        }
        // ステージの端で位置を制限し、高さは常に床の Y = 0 に固定します。
        transform.position = new Vector3(Mathf.Clamp(position.x, -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth), 0, Mathf.Clamp(position.z, -BrawlerBalance.StageHalfDepth, BrawlerBalance.StageHalfDepth));
    }

    private void UpdateWalkAnimation(Vector3 movement, float deltaTime)
    {
        // Animator の代わりに Sin で左右の足を逆方向に振る、仮の歩行演出です。
        _walkPhase += movement.magnitude * deltaTime * 12;
        _rightLeg.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_walkPhase) * movement.magnitude * 20);
        _leftLeg.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(_walkPhase) * movement.magnitude * 20);
        // X を負にすると Model が左右反転します。
        _visual.localScale = new Vector3(Facing, ModelHeightScale, 1);
    }

    // 腕の演出に合わせて命中判定を一度だけ発生させます。
    private void UpdateAttackAnimation(float deltaTime)
    {
        if (_attackRemaining > 0)
        {
            _attackRemaining -= deltaTime;
            // 攻撃中は腕を横に伸ばします。角度から Quaternion を作って回転を指定します。
            _attackArm.localRotation = Quaternion.Euler(0, 0, GetAttackArmAngle());
            bool isDashAttack = _attackKind == BrawlerDefines.AttackKind.Dash
                || _attackKind == BrawlerDefines.AttackKind.DashSpecial;
            bool isLowAttack = _attackKind == BrawlerDefines.AttackKind.Ground
                || _attackKind == BrawlerDefines.AttackKind.Crouch;
            _attackArm.localPosition = new Vector3(isDashAttack ? 0.9f : 0.65f,
                _attackKind == BrawlerDefines.AttackKind.Uppercut ? 1.6f : isLowAttack ? 0.65f : 1.35f, -0.1f);
            // 技の開始から所定の時間を過ぎたら、一度だけ命中判定を行います。
            float hitTiming = GetAttackHitRemainingSeconds();
            if (!_hasDealtHit && _attackRemaining < hitTiming)
            {
                _hasDealtHit = true;
                int damage = GetAttackDamage();
                if (_attackKind == BrawlerDefines.AttackKind.Ground)
                {
                    // 開始時に選んだ1体だけに命中。起き上がりや距離の変化は命中時に再判定します。
                    _game.HitGroundTarget(this, _groundAttackTarget, BrawlerBalance.GroundAttackDamage);
                }
                else if (_attackKind == BrawlerDefines.AttackKind.Backstab)
                {
                    _game.HitBackstabTarget(this, _backstabTarget, damage);
                }
                else
                {
                    DealAttackDamage(damage);
                }
            }
        }
        else
        {
            if (_attackCooldown <= 0 && _attackKind != BrawlerDefines.AttackKind.Normal)
            {
                ResetAttackKind();
            }
            // 回転なしの状態に戻します。
            _attackArm.localRotation = SpecialActionArmAngle.HasValue
                ? Quaternion.Euler(0, 0, SpecialActionArmAngle.Value)
                : IsGuarding ? Quaternion.Euler(0, 0, 55) : Quaternion.identity;
            _attackArm.localPosition = new Vector3(0.4f, 1.2f, -0.1f);
        }
    }

    // 種類ごとの値はここで選びます。技を増やすときに長い条件式を複数箇所へ追加する必要はありません。
    private int GetAttackDamage()
    {
        return _attackKind switch
        {
            BrawlerDefines.AttackKind.Backstab => BrawlerBalance.BackstabDamage,
            BrawlerDefines.AttackKind.HeavyStrike => BrawlerBalance.HeavyStrikeDamage,
            BrawlerDefines.AttackKind.Push => BrawlerBalance.PushDamage,
            BrawlerDefines.AttackKind.Uppercut => BrawlerBalance.UppercutDamage,
            BrawlerDefines.AttackKind.DashSpecial => BrawlerBalance.DashSpecialDamage,
            BrawlerDefines.AttackKind.Crouch => BrawlerBalance.CrouchAttackDamage,
            BrawlerDefines.AttackKind.DownThrust => BrawlerBalance.DownThrustDamage,
            BrawlerDefines.AttackKind.Air => BrawlerBalance.AirAttackDamage,
            BrawlerDefines.AttackKind.Counter => BrawlerBalance.CounterDamage,
            BrawlerDefines.AttackKind.Dash => BrawlerBalance.DashAttackDamage,
            BrawlerDefines.AttackKind.Ground => BrawlerBalance.GroundAttackDamage,
            _ => IsPlayer ? (_comboStep == BrawlerBalance.ComboSteps
                ? BrawlerBalance.FinisherDamage : BrawlerBalance.NormalDamage) : EnemyAttackDamage
        };
    }

    // 現在の実装は残り秒数で命中を判定するため、発生までの時間から差し引いて返します。
    private float GetAttackHitRemainingSeconds()
    {
        return _attackKind switch
        {
            BrawlerDefines.AttackKind.Backstab => BrawlerBalance.BackstabAttackSeconds - BrawlerBalance.BackstabHitDelaySeconds,
            BrawlerDefines.AttackKind.HeavyStrike => BrawlerBalance.HeavyStrikeAttackSeconds - BrawlerBalance.HeavyStrikeHitDelaySeconds,
            BrawlerDefines.AttackKind.Push => BrawlerBalance.PushAttackSeconds - BrawlerBalance.PushHitDelaySeconds,
            BrawlerDefines.AttackKind.Uppercut => BrawlerBalance.UppercutAttackSeconds - BrawlerBalance.UppercutHitDelaySeconds,
            BrawlerDefines.AttackKind.DashSpecial => BrawlerBalance.DashSpecialAttackSeconds - BrawlerBalance.DashSpecialHitDelaySeconds,
            BrawlerDefines.AttackKind.Ground => BrawlerBalance.GroundHitRemainingSeconds,
            BrawlerDefines.AttackKind.Dash => BrawlerBalance.FastHitRemainingSeconds,
            BrawlerDefines.AttackKind.Counter => BrawlerBalance.FastHitRemainingSeconds,
            _ => BrawlerBalance.NormalHitRemainingSeconds
        };
    }

    private float GetAttackArmAngle()
    {
        return _attackKind switch
        {
            BrawlerDefines.AttackKind.Backstab => BrawlerBalance.BackstabArmAngle,
            BrawlerDefines.AttackKind.Normal => NormalAttackArmAngle,
            BrawlerDefines.AttackKind.Push => 35,
            BrawlerDefines.AttackKind.HeavyStrike => 135,
            BrawlerDefines.AttackKind.Uppercut => -30,
            BrawlerDefines.AttackKind.DownThrust => -90,
            BrawlerDefines.AttackKind.Ground => -65,
            BrawlerDefines.AttackKind.Dash => 110,
            BrawlerDefines.AttackKind.DashSpecial => 110,
            _ => 90
        };
    }

    private void UpdateVisualFeedback()
    {
        // 生存中の転倒は一定時間で起き上がります。死亡姿勢は Update の早期終了で保持します。
        _visual.localRotation = IsDowned ? Quaternion.Euler(0, 0, 80) : Quaternion.identity;
        // モデルだけ縦に縮めます。HPや通常の攻撃判定には影響しません。
        Vector3 visualScale = _visual.localScale;
        visualScale.y = ModelHeightScale * (IsSliding ? 0.4f : _isCrouching || IsCrouchAttacking ? 0.6f : 1);
        _visual.localScale = visualScale;
        // 被ダメージ中は見た目だけ少し浮かせます。攻撃距離を測る親の座標は変わりません。
        _visual.localPosition = new Vector3(0, _jumpHeight + (_stunRemaining > 0 ? 0.08f : 0), 0);
        // 被ダメージは白、回避中は水色、敵の攻撃予告は黄色で見分けられるようにします。
        // SetPropertyBlock(null) で上書きを解除すると、元の各パーツの色に戻ります。
        foreach (var body in _bodyRenderers)
        {
            if (_flashRemaining > 0 || _dashRemaining > 0 || _windupRemaining > 0 || IsGuarding || _blockFlashRemaining > 0 || SpecialFeedbackColor.HasValue)
            {
                // 防御の成立は緑、防御姿勢は青。攻撃予告の黄と見分けます。
                Color feedbackColor = _flashRemaining > 0 ? Color.white
                    : _blockFlashRemaining > 0 ? Color.green
                    : IsGuarding ? new Color(0.3f, 0.5f, 1)
                    : _dashRemaining > 0 ? Color.cyan : SpecialFeedbackColor ?? Color.yellow;
                _flashProperties.SetColor("_Color", feedbackColor);
                body.SetPropertyBlock(_flashProperties);
            }
            else
            {
                body.SetPropertyBlock(null);
            }
        }
    }

    // 攻撃を開始し、演出時間・次の攻撃までの時間・コンボ段数を設定します。
    private void Attack()
    {
        // 命中フレームではなく、予兆が終わって腕を振り始める瞬間に一度だけ鳴らします。
        if (!IsPlayer && Health > 0 && !Game.IsRunFinished)
        {
            Game.PlaySound(EnemySwingSound);
        }
        bool continueCombo = IsPlayer && _canContinueCombo && Facing == _comboHitFacing
            && Time.time - _comboHitAt <= BrawlerBalance.ComboWindowSeconds;
        int nextComboStep = continueCombo ? _comboStep + 1 : 1;
        ResetAttackKind();
        _attackKind = BrawlerDefines.AttackKind.Normal;
        if (IsPlayer)
        {
            _canPushFromHold = true;
        }
        _counterWindowRemaining = 0;
        // 空振りは初段に戻り、実際の命中後に入力した場合だけ次段へ進みます。
        _comboStep = nextComboStep;
        _lastAttackTime = Time.time;
        _attackRemaining = BrawlerBalance.AttackSeconds;
        // プレイヤーは 3 段目の後に長めの隙を作り、敵は攻撃間隔を長くして避けやすくします。
        _attackCooldown = IsPlayer ? (_comboStep == BrawlerBalance.ComboSteps ? BrawlerBalance.FinisherCooldownSeconds : BrawlerBalance.NormalCooldownSeconds) : EnemyAttackInterval;
        _hasDealtHit = false;
    }

    // 回避を攻撃へ変換します。通常コンボとは別の技として、コンボ段数をリセットします。
    private void StartDashAttack()
    {
        ResetAttackKind();
        ClearAttackHold();
        _counterWindowRemaining = 0;
        if (Mathf.Abs(_dashDirection.x) > BrawlerBalance.FacingInputThreshold)
        {
            Facing = Mathf.Sign(_dashDirection.x);
        }
        _dashRemaining = 0;
        _attackKind = BrawlerDefines.AttackKind.Dash;
        _dashAttackMovementRemaining = BrawlerBalance.DashAttackMovementSeconds;
        _attackRemaining = BrawlerBalance.AttackSeconds;
        _attackCooldown = BrawlerBalance.DashAttackCooldownSeconds;
        _attackBufferRemaining = 0;
        _comboStep = 0;
        _lastAttackTime = BrawlerDefines.UnsetTime;
        _hasDealtHit = false;
        _commandGuardHeld = false;
        EndGuard();
    }

    // 時間や入力バッファは各行動の規則に従って更新し、技の種類と対象だけを共通で解除します。
    private void ResetAttackKind()
    {
        _canContinueCombo = false;
        _attackKind = BrawlerDefines.AttackKind.None;
        _groundAttackTarget = null;
        _backstabTarget = null;
    }

    // 着地など、一部の行動だけを解除する場面で使います。他の技は変更しません。
    private void ClearAttackKind(BrawlerDefines.AttackKind kind)
    {
        bool matches = _attackKind == kind
            || kind == BrawlerDefines.AttackKind.Air && _attackKind == BrawlerDefines.AttackKind.DownThrust
            || kind == BrawlerDefines.AttackKind.Dash && _attackKind == BrawlerDefines.AttackKind.DashSpecial;
        if (matches)
        {
            ResetAttackKind();
        }
    }

    private void ClearAttackHold()
    {
        _canPushFromHold = false;
    }

    private bool UpdateAttackHold(bool held)
    {
        // 保持を途中で離したら、その一撃では押し攻撃へ移りません。時間による自動発動は行いません。
        if (!held || IsGuarding || _stunRemaining > 0 || _dashRemaining > 0 || _isCrouching)
        {
            ClearAttackHold();
        }
        return false;
    }

    private bool TryStartPushFromHit(bool held)
    {
        if (!IsPlayer || _attackKind != BrawlerDefines.AttackKind.Normal || !_canPushFromHold || !held)
        {
            return false;
        }
        // 通常攻撃の実命中通知からだけ呼びます。空振りや大斬りの保持では発動しません。
        // 次段の予約も消費し、1回の保持につき一度だけ押し攻撃へ移ります。
        StartCounterAttack();
        _attackKind = BrawlerDefines.AttackKind.Push;
        _attackRemaining = BrawlerBalance.PushAttackSeconds;
        _attackCooldown = BrawlerBalance.PushCooldownSeconds;
        return true;
    }

    private void StartHeavyStrike()
    {
        StartCounterAttack();
        _attackKind = BrawlerDefines.AttackKind.HeavyStrike;
        _attackRemaining = BrawlerBalance.HeavyStrikeAttackSeconds;
        _attackCooldown = BrawlerBalance.HeavyStrikeCooldownSeconds;
    }

    // HPが実際に減ったときだけ気絶を付与し、無敵・終了後・撃破済みでは効果を残しません。
    public void TakeThrownKnifeDamage(int amount, float direction)
    {
        if (CanReceiveDamage && amount > 0)
        {
            // ナイフはガードを通さず、通常の被弾・撃破処理だけを適用します。
            ApplyDamage(amount, direction, false);
        }
    }

    // 大型の職業・敵は派生クラスで体格と一投あたりの命中上限を指定します。
    public virtual int HammerHitLimit => 1;
    public virtual float HammerBodyRadius => 0;

    public void TakeHammerDamage(int amount, float direction, bool isSameProjectileFollowup = false)
    {
        // 同じハンマーの2発目だけは、そのハンマーが付けた気絶中も受理します。
        // 別の投擲や通常攻撃まで硬直無敵を解除しないよう、例外を限定します。
        bool canReceiveFollowup = isSameProjectileFollowup && HammerHitLimit > 1 && IsHammerStunned
            && !_game.IsRunFinished && _dashRemaining <= 0
            && _jumpHeight <= BrawlerBalance.GroundAttackAvoidanceHeight;
        if ((!CanReceiveDamage && !canReceiveFollowup) || amount <= 0)
        {
            return;
        }
        int healthBeforeHit = Health;
        // ハンマーはガード不能ですが、硬直・回避・ジャンプによる無敵は通常攻撃と共用します。
        ApplyDamage(amount, direction, false);
        if (!IsPlayer && Health > 0 && Health < healthBeforeHit)
        {
            _hammerStunRemaining = BrawlerBalance.HammerStunSeconds;
            _stunRemaining = Mathf.Max(_stunRemaining, BrawlerBalance.HammerStunSeconds);
            _attackCooldown = Mathf.Max(_attackCooldown, BrawlerBalance.HammerStunSeconds);
        }
    }
    public void ApplyPushKnockback(float direction)
    {
        _knockbackVelocity = new Vector3(direction * BrawlerBalance.PushKnockbackSpeed, 0, 0);
    }

    protected void StartBackstab(Fighter target, float direction)
    {
        StartCounterAttack();
        _attackKind = BrawlerDefines.AttackKind.Backstab;
        _backstabTarget = target;
        Facing = direction;
        _isRunning = false;
        _attackRemaining = BrawlerBalance.BackstabAttackSeconds;
        _attackCooldown = BrawlerBalance.BackstabCooldownSeconds;
    }

    private void StartUppercut()
    {
        // 共通の単発攻撃初期化を使い、対空技の威力・時間・振り上げ演出を適用します。
        StartCounterAttack();
        _attackKind = BrawlerDefines.AttackKind.Uppercut;
        _isRunning = false;
        _isCrouching = false;
        _dashAttackMovementRemaining = 0;
        _attackRemaining = BrawlerBalance.UppercutAttackSeconds;
        _attackCooldown = BrawlerBalance.UppercutCooldownSeconds;
    }

    private void StartDashSpecial(float direction)
    {
        // 基本のダッシュ攻撃を共用し、コマンド技の調整値だけを差し替えます。無敵はありません。
        _dashDirection = new Vector3(direction, 0, 0);
        StartDashAttack();
        _attackKind = BrawlerDefines.AttackKind.DashSpecial;
        _isRunning = false;
        _isCrouching = false;
        _knockbackVelocity = Vector3.zero;
        _dashAttackMovementRemaining = BrawlerBalance.DashSpecialMovementSeconds;
        _attackRemaining = BrawlerBalance.DashSpecialAttackSeconds;
        _attackCooldown = BrawlerBalance.DashSpecialCooldownSeconds;
    }

    // 単発攻撃の初期化。ガードから移る場合だけ、共通の解除後保護を引き継ぎます。
    private void StartCounterAttack()
    {
        ResetAttackKind();
        ClearAttackHold();
        _counterWindowRemaining = 0;
        _attackKind = BrawlerDefines.AttackKind.Counter;
        _attackRemaining = BrawlerBalance.AttackSeconds;
        _attackCooldown = BrawlerBalance.CounterCooldownSeconds;
        _attackBufferRemaining = 0;
        _comboStep = 0;
        _lastAttackTime = BrawlerDefines.UnsetTime;
        _hasDealtHit = false;
        _knockbackVelocity = Vector3.zero;
        _commandGuardHeld = false;
        EndGuard();
    }

    private void StartSlide(float direction)
    {
        _slideDirection = direction;
        Facing = direction;
        _slideRemaining = BrawlerBalance.SlideMaxSeconds;
        _slideElapsed = 0;
        _slideHitEnemies.Clear();
        _isRunning = false;
        _isCrouching = false;
        _canBackstepFromJump = false;
        _knockbackVelocity = Vector3.zero;
        _attackCooldown = BrawlerBalance.SlideCooldownSeconds;
        ReleaseDefenseForSpecialAction();
    }

    public void TrySlideHit(Fighter target)
    {
        if (!IsSliding || target == null || target.IsPlayer || _slideHitEnemies.Contains(target))
        {
            return;
        }
        int previousHealth = target.Health;
        target.TakeDamage(BrawlerBalance.SlideDamage, _slideDirection);
        if (target.Health < previousHealth)
        {
            _slideHitEnemies.Add(target);
        }
    }

    private void UpdateRunningInput(Vector3 input)
    {
        bool forwardHeld = input.x * Facing > BrawlerBalance.ForwardInputThreshold;
        bool canRun = !_isCrouching && _stunRemaining <= 0 && _attackRemaining <= 0
            && _dashRemaining <= 0 && !IsUsingSpecialAction;
        if (_isRunning && (!canRun || input.x * _runDirection <= BrawlerBalance.ForwardInputThreshold))
        {
            _isRunning = false;
        }
        if (_directionCommands.EnteredForwardFromNeutral && canRun)
        {
            float direction = Mathf.Sign(input.x);
            if (_lastForwardTapDirection == direction && Time.time - _lastForwardTapAt <= BrawlerBalance.DoubleTapWindowSeconds)
            {
                _isRunning = true;
                _runDirection = direction;
                _bodyCheckedEnemies.Clear();
                _lastForwardTapAt = BrawlerDefines.UnsetTime;
            }
            else
            {
                _lastForwardTapAt = Time.time;
                _lastForwardTapDirection = direction;
            }
        }
        if (!_isRunning && !_directionCommands.IsNeutral
            && (Mathf.Abs(input.z) > BrawlerBalance.CommandDirectionThreshold || !forwardHeld))
        {
            // 斜め・逆方向を挟んだ2回入力を純粋な横ダッシュとして扱いません。
            _lastForwardTapAt = BrawlerDefines.UnsetTime;
        }
        _wasForwardHeld = forwardHeld;
    }

    public void TryBodyCheck(Fighter target)
    {
        if (!_isRunning || target == null || target.IsPlayer || _bodyCheckedEnemies.Contains(target))
        {
            return;
        }
        int previousHealth = target.Health;
        target.TakeDamage(BrawlerBalance.BodyCheckDamage, _runDirection);
        // 硬直無敵で拒否された接触は消費せず、実際に命中した相手だけ記録します。
        if (target.Health < previousHealth)
        {
            _bodyCheckedEnemies.Add(target);
        }
    }

    private void StartJump(Vector3 input)
    {
        ClearAttackKind(BrawlerDefines.AttackKind.Crouch);
        _isCrouching = false;
        _canBackstepFromJump = input.z <= BrawlerBalance.JumpDirectionThreshold && _dashRemaining <= 0 && !_isRunning;
        _lastJumpPressedAt = Time.time;
        _airJumpsRemaining = ExtraAirJumpCount;
        _wallJumpsRemaining = WallJumpCount;
        // 上＋ジャンプは大ジャンプ。回避中のジャンプは前方への慣性を引き継ぎます。
        _jumpVelocity = input.z > BrawlerBalance.JumpDirectionThreshold ? BrawlerBalance.HighJumpVelocity : BrawlerBalance.JumpVelocity;
        _jumpDrift = _isRunning ? new Vector3(_runDirection * BrawlerBalance.JumpDriftSpeed, 0, 0)
            : _dashRemaining > 0 ? _dashDirection * BrawlerBalance.JumpDriftSpeed : Vector3.zero;
        _isRunning = false;
        _wasForwardHeld = false;
        _dashRemaining = 0;
        _knockbackVelocity = Vector3.zero;
        ReleaseDefenseForSpecialAction();
    }

    private bool TryStartWallJump()
    {
        if (!IsPlayer || !IsAirborne || _wallJumpsRemaining <= 0 || _stunRemaining > 0
            || _attackRemaining > 0 || _dashRemaining > 0 || IsUsingSpecialAction)
        {
            return false;
        }
        // X軸のステージ端だけを壁として扱います。奥行きの境界や中央では発動しません。
        float x = transform.position.x;
        if (Mathf.Abs(x) < BrawlerBalance.StageHalfWidth - WallJumpMargin)
        {
            return false;
        }
        float inwardDirection = x > 0 ? -1f : 1f;
        _wallJumpsRemaining--;
        _jumpVelocity = WallJumpVelocity;
        _jumpDrift = new Vector3(inwardDirection * WallJumpDriftSpeed, 0, 0);
        Facing = inwardDirection;
        _canBackstepFromJump = false;
        ClearAttackHold();
        // 高度・2段ジャンプの残り回数・攻撃の待ち時間は変更しません。無敵も追加しません。
        return true;
    }

    private bool TryStartAirJump()
    {
        if (!IsPlayer || !IsAirborne || _airJumpsRemaining <= 0 || _stunRemaining > 0
            || _attackRemaining > 0 || _dashRemaining > 0 || IsUsingSpecialAction)
        {
            return false;
        }
        // 現在の高さを維持して上向き速度だけを再設定します。無敵の追加や攻撃硬直の解除は行いません。
        _airJumpsRemaining--;
        _jumpVelocity = AirJumpVelocity;
        _canBackstepFromJump = false;
        ClearAttackHold();
        return true;
    }

    private void UpdateJump(float deltaTime)
    {
        if (!IsAirborne)
        {
            return;
        }
        const float gravity = BrawlerBalance.JumpGravity;
        _jumpHeight += _jumpVelocity * deltaTime - gravity * deltaTime * deltaTime * 0.5f;
        _jumpVelocity -= gravity * deltaTime;
        if (_jumpHeight <= 0)
        {
            _jumpHeight = 0;
            _jumpVelocity = 0;
            _airJumpsRemaining = 0;
            _wallJumpsRemaining = 0;
            _jumpDrift = Vector3.zero;
            if ((_attackKind == BrawlerDefines.AttackKind.Air || _attackKind == BrawlerDefines.AttackKind.DownThrust))
            {
                _attackRemaining = 0;
                ClearAttackKind(BrawlerDefines.AttackKind.Air);
            }
        }
    }

    private void StartAirAttack(bool isDownThrust)
    {
        ResetAttackKind();
        ClearAttackHold();
        _canBackstepFromJump = false;
        _attackKind = isDownThrust ? BrawlerDefines.AttackKind.DownThrust : BrawlerDefines.AttackKind.Air;
        _attackRemaining = BrawlerBalance.AttackSeconds;
        _attackCooldown = BrawlerBalance.AirAttackCooldownSeconds;
        _hasDealtHit = false;
        if (isDownThrust)
        {
            // 下突きは降下を速めます。攻撃が着地より遅ければ命中せず終了します。
            _jumpVelocity = BrawlerBalance.DownThrustVelocity;
        }
    }

    private void StartCrouchAttack()
    {
        ResetAttackKind();
        ClearAttackHold();
        // しゃがみ攻撃は低い位置の素早い一撃。通常コンボとは分けて段数をリセットします。
        _attackKind = BrawlerDefines.AttackKind.Crouch;
        _counterWindowRemaining = 0;
        _attackRemaining = BrawlerBalance.CrouchAttackSeconds;
        _attackCooldown = BrawlerBalance.CrouchAttackCooldownSeconds;
        _attackBufferRemaining = 0;
        _comboStep = 0;
        _lastAttackTime = BrawlerDefines.UnsetTime;
        _hasDealtHit = false;
    }

    private void StartBackstep()
    {
        _isRunning = false;
        _wasForwardHeld = false;
        // 中断した攻撃の命中判定・踏み込み・予約を残さず、回避だけを進めます。
        ResetAttackKind();
        ClearAttackHold();
        _attackRemaining = 0;
        _attackCooldown = 0;
        _attackBufferRemaining = 0;
        _dashAttackMovementRemaining = 0;
        _backstepButtons.Reset();
        // 向きを変えず、共通の回避無敵・ステージ端制限を使います。
        _canBackstepFromJump = false;
        _jumpHeight = 0;
        _jumpVelocity = 0;
        _airJumpsRemaining = 0;
        _wallJumpsRemaining = 0;
        _jumpDrift = Vector3.zero;
        _isBackstep = true;
        _dashDirection = new Vector3(-Facing, 0, 0);
        _dashRemaining = BrawlerBalance.BackstepSeconds;
        _dashCooldown = BrawlerBalance.BackstepCooldownSeconds;
        _knockbackVelocity = Vector3.zero;
        ReleaseDefenseForSpecialAction();
    }

    private void StartGroundAttack(Fighter target)
    {
        ResetAttackKind();
        ClearAttackHold();
        _attackKind = BrawlerDefines.AttackKind.Ground;
        _groundAttackTarget = target;
        _counterWindowRemaining = 0;
        _attackRemaining = BrawlerBalance.GroundAttackSeconds;
        _attackCooldown = BrawlerBalance.GroundAttackCooldownSeconds;
        _attackBufferRemaining = 0;
        _comboStep = 0;
        _lastAttackTime = BrawlerDefines.UnsetTime;
        _hasDealtHit = false;
        _commandGuardHeld = false;
        EndGuard();
    }

    public bool TryTakeGroundHit(int amount, float direction)
    {
        if (_game.IsRunFinished || !CanReceiveGroundHit || amount <= 0)
        {
            return false;
        }
        _hasReceivedGroundHit = true;
        ApplyDamage(amount, direction, true);
        return true;
    }

    // 通常攻撃と投擲で、ダメージを受理できる条件を共用します。
    private bool CanReceiveDamage => Health > 0 && !_game.IsRunFinished && _stunRemaining <= 0
        && _dashRemaining <= 0 && _guardReleaseRemaining <= 0
        && _jumpHeight <= BrawlerBalance.GroundAttackAvoidanceHeight;

    // 攻撃が当たった相手側で呼び出されます。direction は攻撃者の向きです。
    public void TakeDamage(int amount, float direction, BrawlerDefines.DamageKind kind = BrawlerDefines.DamageKind.Normal)
    {
        // 硬直中は追加ダメージを受けないため、短い無敵時間も兼ねています。
        if (!CanReceiveDamage)
        {
            return;
        }
        // direction は攻撃者の向き。自分と逆向きなら相手は正面にいると判定できます。
        if (IsGuarding && direction * Facing < 0)
        {
            _game.PlaySound(BrawlerSound.Guard);
            _game.CreateGuardImpact(this);
            _blockFlashRemaining = BrawlerBalance.BlockFlashSeconds;
            _counterWindowRemaining = BrawlerBalance.CounterWindowSeconds;
            _knockbackVelocity = new Vector3(direction * BrawlerBalance.BlockKnockbackSpeed, 0, 0);
            return;
        }
        ApplyDamage(amount, direction, false, kind);
    }

    public void TakeMeleeDamage(int amount, Fighter attacker, BrawlerDefines.DamageKind kind)
    {
        int healthBefore = Health;
        if (attacker == null)
        {
            return;
        }
        // 同じ相手から異なる技をつないだ場合だけ、通常の被弾硬直中にも追加入力の成果を反映します。
        // 転倒・ハンマー気絶・回避・空中の無敵や、別の攻撃者の攻撃にはこの例外を適用しません。
        bool canReceiveChain = !IsPlayer && !IsGuarding && Health > 0 && !Game.IsRunFinished
            && _meleeHitHistory.CanReceiveDifferentAttack(attacker, attacker._attackKind, attacker.ComboStep) && _stunRemaining > 0
            && _downRemaining <= 0 && _hammerStunRemaining <= 0 && _dashRemaining <= 0
            && _jumpHeight <= BrawlerBalance.GroundAttackAvoidanceHeight;
        if (canReceiveChain && amount > 0)
        {
            ApplyDamage(amount, attacker.Facing, false, kind, preserveMeleeChain: true);
        }
        else
        {
            TakeDamage(amount, attacker.Facing, kind);
        }
        if (Health < healthBefore)
        {
            _meleeHitHistory.Record(attacker, attacker._attackKind, attacker.ComboStep);
        }
    }

    private void ApplyDamage(int amount, float direction, bool isGroundHit,
        BrawlerDefines.DamageKind kind = BrawlerDefines.DamageKind.Normal, bool preserveMeleeChain = false)
    {
        // 別のダメージや硬直終了後の新しい攻撃では履歴を消し、近接連携だけ履歴を引き継ぎます。
        if (!preserveMeleeChain)
        {
            _meleeHitHistory.Reset();
        }
        ResetAttackKind();
        ClearAttackHold();
        _slideRemaining = 0;
        _isRunning = false;
        _wasForwardHeld = false;
        _lastForwardTapAt = BrawlerDefines.UnsetTime;
        _commandGuardHeld = false;
        EndGuard();
        _guardReleaseRemaining = 0;
        int healthBeforeDamage = Health;
        ReduceHealth(CalculateReceivedDamage(amount));
        if (Health < healthBeforeDamage)
        {
            OnDamageReceived(healthBeforeDamage - Health);
        }
        if (!IsPlayer)
        {
            // 被弾確定時に残像の待機を開始し、同フレームの描画にも直前HPを残します。
            _enemyHealthBarFeedback.Refresh(Health, MaxHealth, 0);
        }
        _stunRemaining = isGroundHit ? Mathf.Max(_stunRemaining, _downRemaining) : BrawlerBalance.StunSeconds;
        // 被ダメージによって進行中の攻撃を中断します。
        _attackRemaining = 0;
        _isCrouching = false;
        _canBackstepFromJump = false;
        _airJumpsRemaining = 0;
        _wallJumpsRemaining = 0;
        _counterWindowRemaining = 0;
        _dashAttackMovementRemaining = 0;
        _attackBufferRemaining = 0;
        // 予告中に殴れば敵の攻撃を止められます。
        _windupRemaining = 0;
        _attackCooldown = Mathf.Max(_attackCooldown, IsPlayer ? BrawlerBalance.StunSeconds : BrawlerBalance.EnemyHitCooldownSeconds);
        _flashRemaining = BrawlerBalance.DamageFlashSeconds;
        // 最終段で敵を転倒させます。硬直時間と合わせて、起き上がるまで行動を止めます。
        bool isStrongHit = kind == BrawlerDefines.DamageKind.Critical || amount >= BrawlerBalance.KnockdownDamageThreshold;
        // 低威力へ調整したコマンド技でも、クリティカルなら確実に転倒させます。
        if (!isGroundHit && !IsPlayer && isStrongHit && Health > 0)
        {
            _hasReceivedGroundHit = false;
            _downRemaining = BrawlerBalance.DownSeconds;
            _stunRemaining = _downRemaining;
        }
        // コンボ最終段では大きく吹き飛ばし、敵との間合いを作ります。
        _knockbackVelocity = isGroundHit ? Vector3.zero : new Vector3(direction * (isStrongHit ? BrawlerBalance.StrongKnockbackSpeed : BrawlerBalance.KnockbackSpeed), 0, 0);
        // 残りHPを超えた攻撃や軽減があっても、表示は実際に減ったHPに合わせます。
        if (IsPlayer && Health < healthBeforeDamage)
        {
            _game.ShowHitDirection(direction);
        }
        _game.RegisterDamage(this, amount, healthBeforeDamage - Health, kind);
        if (Health == 0)
        {
            // 仮の倒れる演出。敵だけ 0.5 秒後に消し、プレイヤーは敗北表示のため残します。
            _visual.localRotation = Quaternion.Euler(0, 0, 80);
            if (!IsPlayer)
            {
                gameObject.AddComponent<EnemyDefeatFade>().Initialize(this, _visual);
            }
        }
    }
}
