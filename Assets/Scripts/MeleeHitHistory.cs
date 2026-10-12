using System.Collections.Generic;

// 被弾硬直が続く間に命中した近接技を記録します。転倒や無敵の可否はFighterが判定します。
public sealed class MeleeHitHistory
{
    private Fighter _attacker;
    private readonly HashSet<(BrawlerDefines.AttackKind Kind, int ComboStep)> _receivedAttacks = new();

    public bool CanReceiveDifferentAttack(Fighter attacker, BrawlerDefines.AttackKind kind, int comboStep)
    {
        return attacker != null && _attacker == attacker && !_receivedAttacks.Contains(GetIdentity(kind, comboStep));
    }

    public void Record(Fighter attacker, BrawlerDefines.AttackKind kind, int comboStep)
    {
        // 別の攻撃者の履歴を混ぜず、この対象にダメージが成立した技だけを記録します。
        if (_attacker != attacker)
        {
            Reset();
            _attacker = attacker;
        }
        _receivedAttacks.Add(GetIdentity(kind, comboStep));
    }

    public void Reset()
    {
        _attacker = null;
        _receivedAttacks.Clear();
    }

    private static (BrawlerDefines.AttackKind Kind, int ComboStep) GetIdentity(BrawlerDefines.AttackKind kind, int comboStep)
    {
        // 通常攻撃だけは段ごとに区別します。他の技に残ったコンボ段数は判定に影響させません。
        return (kind, kind == BrawlerDefines.AttackKind.Normal ? comboStep : BrawlerDefines.NoComboStep);
    }
}
