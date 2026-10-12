using UnityEngine;

// 重装型の被弾・気絶・転倒を共用し、ボスの攻撃選択と反撃できる隙だけを追加します。
public sealed class BossFighter : HeavyFighter
{
    private bool _nextAttackIsWide;
    private bool _isWideAttack;
    private float _recoveryRemaining;

    public bool IsRecovering => Health > 0 && _recoveryRemaining > 0;
    public float RecoverySecondsRemaining => _recoveryRemaining;
    public bool IsWideAttack => _isWideAttack;

    // 床の予告と実際の命中判定は、同じ範囲を参照します。
    public override float MeleeAttackReach => _isWideAttack ? BrawlerBalance.BossWideReach : BrawlerBalance.BossStrikeReach;
    public override float MeleeLaneHalfWidth => _isWideAttack ? BrawlerBalance.BossWideLaneHalfWidth : BrawlerBalance.BossStrikeLaneHalfWidth;
    public override float WarningLaneHalfWidth => MeleeLaneHalfWidth;
    protected override float EnemyMovementSpeed => BrawlerBalance.BossSpeed;
    protected override float EnemyWindupDuration => _isWideAttack ? BrawlerBalance.BossWideWindupSeconds : BrawlerBalance.BossStrikeWindupSeconds;
    protected override int EnemyAttackDamage => _isWideAttack ? BrawlerBalance.BossWideDamage : BrawlerBalance.BossStrikeDamage;
    protected override float EnemyAttackInterval => BrawlerBalance.BossAttackIntervalSeconds;

    protected override void Update()
    {
        if (Health > 0 && !Game.IsRunFinished)
        {
            UpdateRecovery(Time.deltaTime);
        }
        base.Update();
    }

    private void UpdateRecovery(float deltaTime)
    {
        _recoveryRemaining = Mathf.Max(0, _recoveryRemaining - Mathf.Max(0, deltaTime));
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
        if (CanStartEnemyAttack)
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
    }
}
