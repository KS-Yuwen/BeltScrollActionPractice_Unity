using UnityEngine;

// ナイフ固有の直進軌道と命中効果です。ハンマーとは別の所持品として残します。
public sealed class ThrowingKnifeProjectile : PlayerThrownProjectile
{
    protected override float Speed => BrawlerBalance.KnifeSpeed;
    protected override float FlightSeconds => BrawlerBalance.KnifeRange / Speed;
    protected override float HitRadius => BrawlerBalance.KnifeHitRadius;
    protected override float TargetHeight => BrawlerBalance.KnifeHeight;

    protected override Vector3 CalculatePosition(float seconds)
    {
        return LaunchPosition + Vector3.right * (Direction * Speed * seconds);
    }

    protected override void ApplyHit(Fighter target)
    {
        // ダガーもガード不能ですが、ハンマーの気絶効果は付けません。
        target.TakeThrownKnifeDamage(BrawlerBalance.KnifeDamage, Direction);
    }
}
