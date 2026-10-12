using System.Collections.Generic;
using UnityEngine;

// ハンマー固有の放物線・回転・気絶効果だけを定義します。
public sealed class ThrowingHammerProjectile : PlayerThrownProjectile
{
    private readonly Dictionary<Fighter, int> _hitCounts = new Dictionary<Fighter, int>();
    private readonly Dictionary<Fighter, float> _lastHitTimes = new Dictionary<Fighter, float>();

    protected override float Speed => BrawlerBalance.HammerSpeed;
    protected override bool FinishOnHit => false;
    // 発射位置の高さを含め、y=0となる正の解を寿命にします。水平射程では消しません。
    protected override float FlightSeconds => (BrawlerBalance.HammerVerticalSpeed
        + Mathf.Sqrt(BrawlerBalance.HammerVerticalSpeed * BrawlerBalance.HammerVerticalSpeed
            + 2 * BrawlerBalance.HammerGravity * LaunchPosition.y)) / BrawlerBalance.HammerGravity;
    protected override float HitRadius => BrawlerBalance.HammerHitRadius;
    protected override float TargetHeight => BrawlerBalance.HammerHeight;
    protected override float TargetVerticalHalfHeight => BrawlerBalance.HammerTargetVerticalHalfHeight;

    protected override Vector3 CalculatePosition(float seconds)
    {
        // 現在の設定は0.5秒で頂点へ達し、発射位置から3.0上昇します。
        // ナイフとの差が見える弧を描き、頂点の後は重力で下降します。
        // 高い区間では地上の敵の頭上を通過し、下降中に再び命中できる高さへ戻ります。
        float height = BrawlerBalance.HammerVerticalSpeed * seconds
            - BrawlerBalance.HammerGravity * seconds * seconds / 2;
        return LaunchPosition + Vector3.right * (Direction * Speed * seconds) + Vector3.up * height;
    }

    protected override void UpdateRotation(float seconds)
    {
        // 見た目の回転は命中判定に影響しません。
        transform.rotation = Quaternion.Euler(0, 0, -Direction * BrawlerBalance.HammerSpinDegreesPerSecond * seconds);
    }

    protected override void ApplyHit(Fighter target)
    {
        _hitCounts.TryGetValue(target, out int count);
        int previousHealth = target.Health;
        target.TakeHammerDamage(BrawlerBalance.HammerDamage, Direction, count > 0);
        // 無敵などでHPが減らなかった接触は、命中回数に数えません。
        if (target.Health < previousHealth)
        {
            _hitCounts[target] = count + 1;
            _lastHitTimes[target] = ElapsedSeconds;
        }
    }

    protected override bool CanHit(Fighter target, float seconds)
    {
        _hitCounts.TryGetValue(target, out int count);
        return count < target.HammerHitLimit
            && (!_lastHitTimes.TryGetValue(target, out float previousHitTime)
                || seconds - previousHitTime >= BrawlerBalance.HammerRepeatHitIntervalSeconds);
    }
}
