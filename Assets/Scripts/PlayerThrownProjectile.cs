using UnityEngine;

// プレイヤーの投擲物に共通する、発射方向の固定・飛行時間・命中・消滅を担当します。
// 職業は判定しません。使用許可と所持品消費はFighter、軌道と命中効果は派生クラスの責務です。
public abstract class PlayerThrownProjectile : MonoBehaviour
{
    protected BeltBrawler Game { get; private set; }
    protected float Direction { get; private set; }
    protected Vector3 LaunchPosition { get; private set; }
    private Fighter _owner;
    private float _elapsed;
    protected float ElapsedSeconds => _elapsed;
    private bool _hasFinished;

    protected abstract float Speed { get; }
    protected abstract float FlightSeconds { get; }
    protected virtual bool FinishOnHit => true;
    protected virtual bool CanHit(Fighter target, float seconds) => true;
    protected abstract float HitRadius { get; }
    protected abstract float TargetHeight { get; }
    protected virtual float TargetVerticalHalfHeight => 0;
    protected abstract Vector3 CalculatePosition(float seconds);
    protected abstract void ApplyHit(Fighter target);
    protected virtual void UpdateRotation(float seconds)
    {
    }

    public void Initialize(BeltBrawler game, Fighter owner)
    {
        Game = game;
        _owner = owner;
        Direction = owner.Facing;
        LaunchPosition = transform.position;
    }

    protected virtual void Update()
    {
        Advance(Time.deltaTime);
    }

    // 長いフレームでも寿命を超えず、曲線の途中にいる相手を見落とさないよう小区間で調べます。
    public void Advance(float deltaTime)
    {
        if (_hasFinished)
        {
            return;
        }
        if (_owner == null || _owner.Health <= 0 || Game.IsRunFinished)
        {
            Finish();
            return;
        }
        float flightSeconds = FlightSeconds;
        float endTime = Mathf.Min(flightSeconds, _elapsed + Mathf.Max(0, deltaTime));
        while (!_hasFinished && _elapsed < endTime)
        {
            float nextTime = Mathf.Min(endTime, _elapsed + BrawlerBalance.ThrownProjectileCollisionStepSeconds);
            Vector3 start = CalculatePosition(_elapsed);
            Vector3 end = CalculatePosition(nextTime);
            Fighter target = Game.FindThrownProjectileTarget(start, end, HitRadius, TargetHeight,
                TargetVerticalHalfHeight, candidate => CanHit(candidate, nextTime));
            _elapsed = nextTime;
            transform.position = end;
            UpdateRotation(_elapsed);
            if (target != null)
            {
                ApplyHit(target);
                if (FinishOnHit)
                {
                    Finish();
                }
            }
            if (end.y <= 0 || _elapsed >= flightSeconds)
            {
                // 放物線の寿命を着地時刻に合わせると、地面の下まで進めずに消滅できます。
                if (end.y < 0)
                {
                    transform.position = new Vector3(end.x, 0, end.z);
                }
                Finish();
            }
        }
    }

    private void Finish()
    {
        // Destroyはフレーム末尾なので、その前の再更新も拒否して二重命中を防ぎます。
        _hasFinished = true;
        Destroy(gameObject);
    }
}
