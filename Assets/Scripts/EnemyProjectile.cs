using UnityEngine;

// 敵の弾。発射後は追尾しないため、盾で受けるか奥行き方向に避けるかを選べます。
public sealed class EnemyProjectile : MonoBehaviour
{
    private Fighter _target;
    private BeltBrawler _game;
    private bool _isFinished;
    private float _direction;
    private float _remainingLifetime = BrawlerBalance.EnemyProjectileLifetimeSeconds;

    public void Initialize(Fighter target, float direction, BeltBrawler game = null)
    {
        _target = target;
        _game = game;
        _direction = direction;
    }

    private void Update()
    {
        if (OptionsMenu.BlocksGameplay)
        {
            return;
        }
        // Destroyはフレーム末に行われるため、終了済みの弾が二重に音やダメージを出すのを防ぎます。
        if (_isFinished)
        {
            return;
        }
        // すでに発射された弾も位置・寿命・接触判定を保存し、停止中に術者へ届かないようにします。
        if ((_game != null && _game.IsSpellEffectActive) || (_target != null && _target.IsSpellEffectActive))
        {
            return;
        }
        _remainingLifetime -= Time.deltaTime;
        if (_target == null || _target.Health <= 0 || _remainingLifetime <= 0)
        {
            // 射線を避けられて弾が寿命を迎えた場合に限り空振り音を出します。
            // 命中やガード時は既存の接触音を使い、終了後は効果音を追加しません。
            if (_remainingLifetime <= 0 && _target != null && _target.Health > 0 && _game != null && !_game.IsRunFinished)
            {
                _game.PlaySound(BrawlerSound.EnemyMiss);
            }
            _isFinished = true;
            Destroy(gameObject);
            return;
        }
        Vector3 previousPosition = transform.position;
        transform.position += Vector3.right * _direction * BrawlerBalance.EnemyProjectileSpeed * Time.deltaTime;
        Vector3 targetPosition = _target.transform.position + Vector3.up * (BrawlerBalance.EnemyProjectileHeight + _target.JumpHeight);
        if (TouchesTarget(previousPosition, transform.position, targetPosition))
        {
            // ダメージの可否は Fighter が判断するため、ガードと回避の共通ルールを使えます。
            _target.TakeDamage(BrawlerBalance.EnemyProjectileDamage, _direction);
            _isFinished = true;
            Destroy(gameObject);
        }
    }

    // 点だけで判定すると低 FPS 時に敵を飛び越えてしまうため、移動区間全体を調べます。
    public static bool TouchesTarget(Vector3 start, Vector3 end, Vector3 target)
    {
        Vector3 travel = end - start;
        float fraction = travel.sqrMagnitude > 0
            ? Mathf.Clamp01(Vector3.Dot(target - start, travel) / travel.sqrMagnitude) : 0;
        Vector3 closestPoint = start + travel * fraction;
        return (target - closestPoint).sqrMagnitude <= BrawlerBalance.EnemyProjectileHitRadius * BrawlerBalance.EnemyProjectileHitRadius;
    }
}
