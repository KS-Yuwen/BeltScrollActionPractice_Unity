using UnityEngine;

// 遠距離型は Fighter の戦闘処理を継承し、間合いの判断と攻撃方法を上書きします。
// 予告中に向きと奥行きを固定するため、プレイヤーは奥行き移動で射線から逃げられます。
public sealed class RangedFighter : Fighter
{
    protected override float EnemySpawnWaitSeconds => EnemyAiSettings.Current.RangedSpawnWaitSeconds;
    public override bool UsesCoordinatedMeleeAttacks => false;
    // 弾の発射位置・飛距離・衝突半径から射線表示を求めます。
    public override float WarningRangeStart => BrawlerBalance.EnemyProjectileLaunchOffset;
    public override float WarningRangeEnd => WarningRangeStart
        + BrawlerBalance.EnemyProjectileSpeed * BrawlerBalance.EnemyProjectileLifetimeSeconds;
    public override float WarningLaneHalfWidth => BrawlerBalance.EnemyProjectileHitRadius;

    private bool _isApproaching;
    private bool _isRetreating;
    protected override float EnemyMovementSpeed => EnemyAiSettings.Current.RangedMoveSpeed;
    protected override float EnemyWindupDuration => EnemyAiSettings.Current.RangedWindupSeconds;
    protected override float EnemyAttackInterval => EnemyAiSettings.Current.RangedAttackIntervalSeconds;
    protected override float EnemyRecoverySeconds => EnemyAiSettings.Current.RangedRecoverySeconds;

    protected override Vector3 GetEnemyMovement()
    {
        EnemyAiSettings settings = EnemyAiSettings.Current;
        Vector3 target = Game.Target.transform.position;
        Vector3 delta = target - transform.position;
        Facing = delta.x >= 0 ? 1 : -1;
        float horizontalDistance = Mathf.Abs(delta.x);
        float approachStop = settings.RangedApproachDistance - settings.RangedSpacingHysteresis;
        float retreatStop = settings.RangedRetreatDistance + settings.RangedSpacingHysteresis;

        // 接近開始と終了で別の距離を使い、境界付近の小さな移動で足を止め続けないようにします。
        _isApproaching = horizontalDistance > settings.RangedApproachDistance
            || (_isApproaching && horizontalDistance > approachStop);
        if (_isApproaching)
        {
            _isRetreating = false;
            target.x -= Facing * approachStop;
            return GetMovementToward(target);
        }

        float retreatDirection = -Facing;
        float edge = BrawlerBalance.StageHalfWidth - settings.RangedEdgeMargin;
        bool canRetreat = transform.position.x * retreatDirection < edge;
        _isRetreating = canRetreat && (horizontalDistance < settings.RangedRetreatDistance
            || (_isRetreating && horizontalDistance < retreatStop));
        if (_isRetreating)
        {
            // 後退も少し余裕ができるまで続けます。床の端へ達したら射撃へ切り替えます。
            Vector3 retreatTarget = transform.position;
            retreatTarget.x = Mathf.Clamp(retreatTarget.x + retreatDirection * (retreatStop - horizontalDistance), -edge, edge);
            return GetMovementToward(retreatTarget);
        }
        if (Mathf.Abs(delta.z) > settings.RangedLaneTolerance)
        {
            target.x = transform.position.x;
            return GetMovementToward(target);
        }
        if (CanStartEnemyAttack)
        {
            // ここから先の向き・射線固定や中断処理はFighterと共用します。
            BeginAttackWindup(EnemyWindupDuration);
        }
        return Vector3.zero;
    }

    // 思考が返す移動の強さを制限し、低FPSでも間合いや端の余白を通り過ぎにくくします。
    private Vector3 GetMovementToward(Vector3 target)
    {
        Vector3 delta = target - transform.position;
        delta.y = 0;
        float travel = EnemyMovementSpeed * Time.deltaTime;
        return delta.normalized * (travel > 0 ? Mathf.Min(1, delta.magnitude / travel) : 1);
    }

    protected override void DealAttackDamage(int damage)
    {
        var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Enemy Projectile";
        projectileObject.transform.position = transform.position + new Vector3(Facing * BrawlerBalance.EnemyProjectileLaunchOffset, BrawlerBalance.EnemyProjectileHeight, 0);
        projectileObject.transform.localScale = Vector3.one * 0.3f;
        projectileObject.GetComponent<Renderer>().sharedMaterial = GetComponentInChildren<Renderer>().sharedMaterial;
        // 判定は移動区間と対象の距離で行います。物理コライダーは使いません。
        Destroy(projectileObject.GetComponent<Collider>());
        projectileObject.AddComponent<EnemyProjectile>().Initialize(Game.Target, Facing, Game);
        // 命中を待たず、発射時から足を止めます。弾はこの後も独立して飛び続けます。
        BeginEnemyRecovery();
    }
}
