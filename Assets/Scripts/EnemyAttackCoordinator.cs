using System.Collections.Generic;
using UnityEngine;

// 通常の近接敵の攻撃順を管理します。敵の移動・命中・被弾の処理はFighterへ任せます。
public sealed class EnemyAttackCoordinator
{
    private readonly EnemyAiSettings _settings;
    private readonly List<Fighter> _waiting = new List<Fighter>();
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
        // 死亡・被弾・転倒・間合いからの離脱で、待ち列の先頭が詰まらないようにします。
        _waiting.RemoveAll(waiting => waiting == null || !waiting.CanWaitForMeleeAttack);
        _active.RemoveAll(active => active == null || !active.IsEnemyAttackActive);
        if (fighter == null || !fighter.CanWaitForMeleeAttack)
        {
            return false;
        }
        if (!_waiting.Contains(fighter))
        {
            _waiting.Add(fighter);
        }
        // 更新順が早い敵だけが攻撃を取り続けないよう、待ち始めた順番を守ります。
        if (_waiting[0] != fighter || _active.Count >= _settings.MaxSimultaneousAttackers || _startDelayRemaining > 0)
        {
            return false;
        }
        _waiting.RemoveAt(0);
        _active.Add(fighter);
        _startDelayRemaining = _settings.AttackStartIntervalSeconds;
        return true;
    }

    public void Reset()
    {
        _waiting.Clear();
        _active.Clear();
        _startDelayRemaining = 0;
    }
}
