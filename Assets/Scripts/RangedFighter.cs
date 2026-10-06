using UnityEngine;

// 遠距離型は Fighter の戦闘処理を継承し、間合いの判断と攻撃方法を上書きします。
// 予告中に向きと奥行きを固定するため、プレイヤーは奥行き移動で射線から逃げられます。
public sealed class RangedFighter : Fighter
{
    protected override float EnemyAttackInterval => 2;

    protected override Vector3 GetEnemyMovement()
    {
        Vector3 delta = Game.Target.transform.position - transform.position;
        Facing = delta.x >= 0 ? 1 : -1;
        float horizontalDistance = Mathf.Abs(delta.x);
        if (horizontalDistance > 7)
        {
            return delta.normalized;
        }

        // 接近されたら退きます。ただしステージ端では逃げ続けず、その場で攻撃します。
        float retreatDirection = -Facing;
        bool canRetreat = transform.position.x * retreatDirection < 21.5f;
        if (horizontalDistance < 3 && canRetreat)
        {
            return new Vector3(retreatDirection, 0, 0);
        }
        if (Mathf.Abs(delta.z) > 0.4f)
        {
            return new Vector3(0, 0, Mathf.Sign(delta.z));
        }
        if (CanStartEnemyAttack)
        {
            BeginAttackWindup(0.8f);
        }
        return Vector3.zero;
    }

    protected override void DealAttackDamage(int damage)
    {
        var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Enemy Projectile";
        projectileObject.transform.position = transform.position + new Vector3(Facing * 0.7f, 1.2f, 0);
        projectileObject.transform.localScale = Vector3.one * 0.3f;
        projectileObject.GetComponent<Renderer>().sharedMaterial = GetComponentInChildren<Renderer>().sharedMaterial;
        // 判定は移動区間と対象の距離で行います。物理コライダーは使いません。
        Destroy(projectileObject.GetComponent<Collider>());
        projectileObject.AddComponent<EnemyProjectile>().Initialize(Game.Target, Facing);
    }
}
