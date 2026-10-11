// 重装型は近接型の AI を継承します。遅い移動・長い予告・重い一撃で位置取りを促します。
// ガード・予告中断・転倒・追撃は通常の敵と共通なので、覚えた操作がそのまま使えます。
public sealed class HeavyFighter : Fighter
{
    protected override float EnemyMovementSpeed => BrawlerBalance.HeavySpeed;

    protected override float EnemyWindupDuration => BrawlerBalance.HeavyWindupSeconds;

    protected override float EnemyAttackInterval => BrawlerBalance.HeavyAttackIntervalSeconds;

    protected override int EnemyAttackDamage => BrawlerBalance.HeavyDamage;
}
