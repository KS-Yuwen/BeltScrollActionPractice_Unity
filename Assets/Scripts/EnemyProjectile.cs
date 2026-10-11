using UnityEngine;

// 敵の弾。発射後は追尾しないため、盾で受けるか奥行き方向に避けるかを選べます。
public sealed class EnemyProjectile : MonoBehaviour
{
    private Fighter _target;
    private float _direction;
    private float _remainingLifetime = 4;

    public void Initialize(Fighter target, float direction)
    {
        _target = target;
        _direction = direction;
    }

    private void Update()
    {
        _remainingLifetime -= Time.deltaTime;
        if (_target == null || _target.Health <= 0 || _remainingLifetime <= 0)
        {
            Destroy(gameObject);
            return;
        }
        Vector3 previousPosition = transform.position;
        transform.position += Vector3.right * _direction * 8 * Time.deltaTime;
        Vector3 targetPosition = _target.transform.position + Vector3.up * (1.2f + _target.JumpHeight);
        if (TouchesTarget(previousPosition, transform.position, targetPosition))
        {
            // ダメージの可否は Fighter が判断するため、ガードと回避の共通ルールを使えます。
            _target.TakeDamage(8, _direction);
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
        return (target - closestPoint).sqrMagnitude <= 0.45f * 0.45f;
    }
}
