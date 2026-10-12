// 短剣を使うシーフ。速い移動と短い間合いを設定し、基本攻撃・所持品操作は共用します。
public sealed class ThiefFighter : ItemFighter
{
    // 着地ごとに空中で一度だけ跳び直せます。回避・被弾の状態判断は基底クラスへ委譲します。
    protected override int ExtraAirJumpCount => BrawlerBalance.ThiefExtraAirJumps;
    protected override float AirJumpVelocity => BrawlerBalance.ThiefAirJumpVelocity;
    // 端での跳ね返りは空中ジャンプと別枠にし、着地までの発動回数を制限します。
    protected override int WallJumpCount => BrawlerBalance.ThiefWallJumps;
    protected override float WallJumpMargin => BrawlerBalance.ThiefWallJumpMargin;
    protected override float WallJumpVelocity => BrawlerBalance.ThiefWallJumpVelocity;
    protected override float WallJumpDriftSpeed => BrawlerBalance.ThiefWallJumpDriftSpeed;
    private readonly BackstabCommandBuffer _backstabCommands = new BackstabCommandBuffer();

    protected override bool UpdateSpecialAction()
    {
        UnityEngine.Vector3 input = BrawlerInput.ReadMovement();
        _backstabCommands.Record(input, Facing, UnityEngine.Time.time);
        if (base.UpdateSpecialAction())
        {
            return true;
        }
        if (CommonActionUseStatus != BrawlerDefines.ActionUseStatus.Ready)
        {
            return false;
        }
        if (BrawlerInput.WasAttackPressed() && !BrawlerInput.WasJumpPressed()
            && _backstabCommands.TryConsume(input, UnityEngine.Time.time, out float direction))
        {
            Fighter target = Game.FindBackstabTarget(this, direction);
            if (target != null)
            {
                StartBackstab(target, direction);
                return true;
            }
        }
        return false;
    }

    public override PlayerClassSettings ClassSettings => PlayerClassCatalog.Thief;
}
