using UnityEngine;

// 重装型の被弾・気絶・転倒を共用し、ボスの攻撃選択と反撃できる隙だけを追加します。
public sealed class BossFighter : HeavyFighter
{
    private bool _nextAttackIsWide;
    private bool _isWideAttack;
    private float _recoveryRemaining;
    private float _attackRestRemaining;
    private float _phaseNoticeRemaining;

    // 一度後半へ移行したら、回復しても前半には戻りません。
    public bool IsSecondPhase { get; private set; }
    public bool IsShowingPhaseNotice => Health > 0 && _phaseNoticeRemaining > 0;

    public bool IsRecovering => Health > 0 && _recoveryRemaining > 0;
    public float RecoverySecondsRemaining => _recoveryRemaining;
    public bool IsWideAttack => _isWideAttack;
    public override float OpeningSecondsRemaining => IsRecovering ? _recoveryRemaining : 0f;
    public override Color WarningStartColor => _isWideAttack
        ? BrawlerBalance.BossWideWarningStartColor : BrawlerBalance.BossStrikeWarningStartColor;
    public override Color WarningEndColor => _isWideAttack
        ? BrawlerBalance.BossWideWarningEndColor : BrawlerBalance.BossStrikeWarningEndColor;

    // 床の予告と実際の命中判定は、同じ範囲を参照します。
    public override float MeleeAttackReach => _isWideAttack ? BrawlerBalance.BossWideReach : BrawlerBalance.BossStrikeReach;
    public override float MeleeLaneHalfWidth => _isWideAttack ? BrawlerBalance.BossWideLaneHalfWidth : BrawlerBalance.BossStrikeLaneHalfWidth;
    public override float WarningLaneHalfWidth => MeleeLaneHalfWidth;
    protected override float EnemyMovementSpeed => IsSecondPhase ? BrawlerBalance.BossSecondPhaseSpeed : BrawlerBalance.BossSpeed;
    protected override float EnemyWindupDuration => _isWideAttack ? BrawlerBalance.BossWideWindupSeconds : BrawlerBalance.BossStrikeWindupSeconds;
    protected override int EnemyAttackDamage => _isWideAttack ? BrawlerBalance.BossWideDamage : BrawlerBalance.BossStrikeDamage;
    protected override float EnemyAttackInterval => IsSecondPhase
        ? BrawlerBalance.BossSecondPhaseAttackIntervalSeconds : BrawlerBalance.BossAttackIntervalSeconds;

    protected override void Update()
    {
        if (Health > 0 && !Game.IsRunFinished)
        {
            UpdatePhase(Time.deltaTime);
            UpdateRecovery(Time.deltaTime);
        }
        base.Update();
    }

    private void UpdatePhase(float deltaTime)
    {
        // ゲーム時間を使うため、ヒットストップ中は移行通知の表示時間も減りません。
        if (Health <= 0 || Game.IsRunFinished)
        {
            return;
        }
        _phaseNoticeRemaining = Mathf.Max(0, _phaseNoticeRemaining - Mathf.Max(0, deltaTime));
        if (!IsSecondPhase && Health <= MaxHealth * BrawlerBalance.BossSecondPhaseHealthFraction)
        {
            IsSecondPhase = true;
            _phaseNoticeRemaining = BrawlerBalance.BossPhaseNoticeSeconds;
            // 進行中の攻撃・予兆・反撃の隙は変更しません。次の攻撃から待機時間を短縮します。
        }
    }

    private void UpdateRecovery(float deltaTime)
    {
        float elapsed = Mathf.Max(0, deltaTime);
        _recoveryRemaining = Mathf.Max(0, _recoveryRemaining - elapsed);
        _attackRestRemaining = Mathf.Max(0, _attackRestRemaining - elapsed);
    }

    protected override Vector3 GetEnemyMovement()
    {
        // 攻撃後は追跡も振り向きもしません。この間に近づいて反撃できます。
        if (IsRecovering)
        {
            return Vector3.zero;
        }
        Vector3 delta = Game.Target.transform.position - transform.position;
        Facing = delta.x >= 0 ? 1 : -1;
        float stopDistance = _nextAttackIsWide ? BrawlerBalance.BossWideStopDistance : BrawlerBalance.BossStrikeStopDistance;
        if (Mathf.Abs(delta.x) > stopDistance || Mathf.Abs(delta.z) > BrawlerBalance.BossApproachLaneTolerance)
        {
            return delta.normalized;
        }
        if (CanStartEnemyAttack && _attackRestRemaining <= 0)
        {
            // 予告を開始した時点で種類と方向を固定し、途中で別の攻撃へ切り替えません。
            _isWideAttack = _nextAttackIsWide;
            _nextAttackIsWide = !_nextAttackIsWide;
            BeginAttackWindup(EnemyWindupDuration);
        }
        return Vector3.zero;
    }

    protected override void DealAttackDamage(int damage)
    {
        base.DealAttackDamage(damage);
        // 空振りやガードでも隙を作ります。予告を見て避ける行動に反撃の機会を与えます。
        _recoveryRemaining = BrawlerBalance.BossRecoverySeconds;
        // 共通の攻撃クールダウンは1.4秒の隙の間に終わります。
        // 実際の攻撃頻度に差を付けるため、隙の後にも短い待機時間を設けます。
        // この待機中は接近できますが、次の予兆はまだ開始しません。
        _attackRestRemaining = BrawlerBalance.BossRecoverySeconds + EnemyAttackInterval;
    }
}
