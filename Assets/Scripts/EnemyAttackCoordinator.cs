using System.Collections.Generic;
using UnityEngine;

// 通常の近接敵の攻撃順を管理します。敵の移動・命中・被弾の処理はFighterへ任せます。
public sealed class EnemyAttackCoordinator
{
    private readonly EnemyAiSettings _settings;
    // 順番とは別に位置番号を保持し、先頭が攻撃するたびに他の敵の待機位置が反転しないようにします。
    private sealed class WaitingEntry
    {
        public Fighter Fighter { get; }
        public float Side { get; }
        public int Position { get; }

        public WaitingEntry(Fighter fighter, float side, int position)
        {
            Fighter = fighter;
            Side = side;
            Position = position;
        }
    }

    private readonly List<WaitingEntry> _waiting = new List<WaitingEntry>();
    private readonly List<Fighter> _active = new List<Fighter>();
    private float _startDelayRemaining;

    public EnemyAttackCoordinator(EnemyAiSettings settings)
    {
        _settings = settings;
    }

    public void Advance(float deltaTime)
    {
        _startDelayRemaining = Mathf.Max(0, _startDelayRemaining - Mathf.Max(0, deltaTime));
    }

    public bool TryRequest(Fighter fighter)
    {
        // 死亡・被弾・転倒・参加距離からの離脱で、待ち列の先頭が詰まらないようにします。
        _waiting.RemoveAll(waiting => waiting.Fighter == null || !waiting.Fighter.CanWaitForMeleeAttack);
        _active.RemoveAll(active => active == null || !active.IsEnemyAttackActive);
        if (fighter == null || !fighter.CanWaitForMeleeAttack)
        {
            return false;
        }
        if (_waiting.FindIndex(waiting => waiting.Fighter == fighter) < 0)
        {
            int position = 0;
            while (_waiting.Exists(waiting => waiting.Position == position))
            {
                position++;
            }
            float side = fighter.transform.position.x - fighter.EnemyTargetPosition.x;
            // 左右が同位置なら現在の向きから元の側を決め、毎フレーム左右を往復させません。
            side = side == 0 ? -fighter.Facing : Mathf.Sign(side);
            _waiting.Add(new WaitingEntry(fighter, side, position));
        }
        // 更新順が早い敵だけが攻撃を取り続けないよう、待ち始めた順番を守ります。
        if (_waiting[0].Fighter != fighter || !fighter.IsWithinMeleeAttackRange || _active.Count >= _settings.MaxSimultaneousAttackers || _startDelayRemaining > 0)
        {
            return false;
        }
        _waiting.RemoveAt(0);
        _active.Add(fighter);
        _startDelayRemaining = _settings.AttackStartIntervalSeconds;
        return true;
    }

    public bool IsFirstWaiting(Fighter fighter) => _waiting.Count > 0 && _waiting[0].Fighter == fighter;

    public Vector3 GetWaitingVelocity(Fighter fighter, float deltaTime)
    {
        WaitingEntry entry = _waiting.Find(waiting => waiting.Fighter == fighter);
        if (entry == null || IsFirstWaiting(fighter) || !fighter.CanWaitForMeleeAttack || deltaTime <= 0)
        {
            return Vector3.zero;
        }
        // 左右の側を保持し、奥行きは上下交互に配置します。床の範囲外へは目標を置きません。
        int pair = entry.Position / BrawlerBalance.WaitingPositionsPerDepthPair + 1;
        float depthSign = entry.Position % BrawlerBalance.WaitingPositionsPerDepthPair == 0 ? 1 : -1;
        Vector3 target = fighter.EnemyTargetPosition;
        target.x = Mathf.Clamp(target.x + entry.Side * _settings.WaitingDistance,
            -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth);
        target.z = Mathf.Clamp(target.z + depthSign * pair * _settings.WaitingDepthOffset,
            -BrawlerBalance.StageHalfDepth, BrawlerBalance.StageHalfDepth);
        target.y = 0;
        Vector3 delta = target - fighter.transform.position;
        delta.y = 0;
        if (delta.magnitude <= _settings.WaitingPositionTolerance)
        {
            return Vector3.zero;
        }
        // 低FPSでも目標を通り越さない速度に制限します。0を設定すれば待機移動だけ無効にできます。
        return delta.normalized * Mathf.Min(_settings.WaitingMoveSpeed, delta.magnitude / deltaTime);
    }

    // 敵を直接押さず、自分が避ける速度だけを返します。予告・攻撃・被弾中の相手を動かしません。
    public Vector3 GetSeparationVelocity(Fighter fighter, IReadOnlyList<Fighter> neighbors, float deltaTime)
    {
        float radius = _settings.SeparationDistance;
        if (fighter == null || !fighter.CanAdjustEnemySpacing || deltaTime <= 0
            || radius <= 0 || _settings.SeparationStrength <= 0)
        {
            return Vector3.zero;
        }
        // 一覧の相対順は移動では変わりません。Unity固有のID APIにも依存させません。
        int fighterIndex = -1;
        for (int i = 0; i < neighbors.Count; i++)
        {
            if (neighbors[i] == fighter)
            {
                fighterIndex = i;
                break;
            }
        }
        if (fighterIndex < 0)
        {
            return Vector3.zero;
        }
        Vector3 direction = Vector3.zero;
        float maxOverlap = 0;
        for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
        {
            Fighter neighbor = neighbors[neighborIndex];
            if (neighbor == null || neighbor == fighter || neighbor.IsPlayer || neighbor.Health <= 0 || neighbor.IsDowned)
            {
                continue;
            }
            Vector3 offset = fighter.transform.position - neighbor.transform.position;
            offset.y = 0;
            float distance = offset.magnitude;
            if (distance >= radius)
            {
                continue;
            }
            float overlap = radius - distance;
            maxOverlap = Mathf.Max(maxOverlap, overlap);
            // 完全に同じ位置でも乱数を使わず、敵一覧の相対順で逆方向へ避けます。
            Vector3 away = distance > Mathf.Epsilon ? offset / distance
                : fighterIndex > neighborIndex ? Vector3.forward : Vector3.back;
            direction += away * (overlap / radius);
        }
        // 人数が増えても回避速度を上げず、低FPSでも一度に離れすぎないよう半分の重なりまでに制限します。
        float speed = Mathf.Min(_settings.SeparationStrength, maxOverlap / (deltaTime * 2));
        return Vector3.ClampMagnitude(direction, 1) * speed;
    }

    public void Reset()
    {
        _waiting.Clear();
        _active.Clear();
        _startDelayRemaining = 0;
    }
}
