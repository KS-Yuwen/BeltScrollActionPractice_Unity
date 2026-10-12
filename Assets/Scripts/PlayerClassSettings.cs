using System.Collections.Generic;

// 職業ごとの移動・武器の間合い・使用許可をまとめます。魔法の発動処理は派生Fighterが担当します。
public sealed class PlayerClassSettings
{
    private readonly HashSet<BrawlerDefines.UsableAction> _allowedActions;
    private readonly float _weaponReachMultiplier;

    public float WalkSpeed { get; }
    public float RunSpeed { get; }
    public float NormalAttackReach { get; }

    public PlayerClassSettings(float walkSpeed, float runSpeed, float normalAttackReach,
        float weaponReachMultiplier, params BrawlerDefines.UsableAction[] allowedActions)
    {
        WalkSpeed = walkSpeed;
        RunSpeed = runSpeed;
        NormalAttackReach = normalAttackReach;
        _weaponReachMultiplier = weaponReachMultiplier;
        // 渡された配列はコピーし、共有設定が外から変更されないようにします。
        _allowedActions = new HashSet<BrawlerDefines.UsableAction>(allowedActions);
    }

    public bool CanUseAction(BrawlerDefines.UsableAction action)
    {
        return action != BrawlerDefines.UsableAction.None && _allowedActions.Contains(action);
    }

    public float GetAttackReach(BrawlerDefines.AttackKind kind)
    {
        // CSV値を生成時に固定せず、参照するたびに取得します。調整値の読み替えにも追従します。
        float reach = kind switch
        {
            BrawlerDefines.AttackKind.HeavyStrike => BrawlerBalance.HeavyStrikeReach,
            BrawlerDefines.AttackKind.Push => BrawlerBalance.PushReach,
            BrawlerDefines.AttackKind.Uppercut => BrawlerBalance.UppercutReach,
            BrawlerDefines.AttackKind.Ground => BrawlerBalance.GroundAttackReach,
            _ => NormalAttackReach
        };
        // 通常リーチは職業別の絶対値、特殊技は共通調整値に職業別の武器倍率を適用します。
        return kind == BrawlerDefines.AttackKind.HeavyStrike || kind == BrawlerDefines.AttackKind.Push
            || kind == BrawlerDefines.AttackKind.Uppercut || kind == BrawlerDefines.AttackKind.Ground
            ? reach * _weaponReachMultiplier : reach;
    }
}
