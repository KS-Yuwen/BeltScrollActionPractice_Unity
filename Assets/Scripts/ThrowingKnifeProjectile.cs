using UnityEngine;

// プレイヤーの直進ナイフ。発射時の向きを固定し、一体に当たるか射程まで飛ぶと消えます。
public sealed class ThrowingKnifeProjectile : MonoBehaviour
{
    private BeltBrawler _game;
    private Fighter _owner;
    private float _direction;
    private float _remainingRange;
    private bool _hasFinished;

    public void Initialize(BeltBrawler game, Fighter owner)
    {
        _game = game;
        _owner = owner;
        _direction = owner.Facing;
        _remainingRange = BrawlerBalance.KnifeRange;
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    // 経過秒数を渡して移動量を求めるため、低FPSでも速度・射程が変わりません。
    public void Advance(float deltaTime)
    {
        if (_hasFinished)
        {
            return;
        }
        if (_owner == null || _owner.Health <= 0)
        {
            Finish();
            return;
        }
        float distance = Mathf.Min(_remainingRange, BrawlerBalance.KnifeSpeed * Mathf.Max(0, deltaTime));
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.right * (_direction * distance);
        // 移動区間全体で調べ、一度の更新で敵を飛び越えても最初の相手に当てます。
        Fighter target = _game.FindKnifeTarget(start, end);
        transform.position = end;
        _remainingRange -= distance;
        if (target != null)
        {
            target.TakeDamage(BrawlerBalance.KnifeDamage, _direction);
            Finish();
        }
        else if (_remainingRange <= 0)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _hasFinished = true;
        Destroy(gameObject);
    }
}
