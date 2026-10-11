// 重装型は近接型の AI を継承します。遅い移動・長い予告・重い一撃で位置取りを促します。
// ガード・予告中断・転倒・追撃は通常の敵と共通なので、覚えた操作がそのまま使えます。
public sealed class HeavyFighter : Fighter
{
    protected override float EnemyMovementSpeed => 1.3f;

    protected override float EnemyWindupDuration => 0.95f;

    protected override float EnemyAttackInterval => 1.8f;

    protected override int EnemyAttackDamage => 24;
}
