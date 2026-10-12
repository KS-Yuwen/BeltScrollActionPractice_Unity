using UnityEngine;

// 補助魔法の持続状態を表示します。HP・威力・使用回数・効果時間は変更しません。
public sealed class SupportSpellAura : MonoBehaviour
{
    private ClericFighter _owner;
    private BeltBrawler _game;
    private LineRenderer _protection;
    private LineRenderer _attackBoost;

    public void Initialize(ClericFighter owner, BeltBrawler game, Material material)
    {
        _owner = owner;
        _game = game;
        // モデルの向きとは独立した床上の表示。術者の移動に追従し、術者の破棄で一緒に消えます。
        transform.SetParent(owner.transform, false);
        _protection = CreateAura("Protection aura", BrawlerBalance.SupportSpellRingSegments,
            BrawlerBalance.ProtectionAuraRadius, BrawlerBalance.ProtectionAuraHeight, material);
        _attackBoost = CreateAura("Attack boost aura", BrawlerBalance.AttackBoostAuraSegments,
            BrawlerBalance.AttackBoostAuraRadius, BrawlerBalance.AttackBoostAuraHeight, material);
        LateUpdate();
    }

    private LineRenderer CreateAura(string label, int segments, float radius, float height, Material material)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = false;
        line.loop = true;
        line.sharedMaterial = material;
        line.startWidth = BrawlerBalance.SupportAuraLineWidth;
        line.endWidth = BrawlerBalance.SupportAuraLineWidth;
        line.positionCount = segments;
        // 防御は外側の丸い輪、攻撃強化は内側の菱形。併用しても形と色の両方で区別できます。
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2 * i / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
        return line;
    }

    private void LateUpdate()
    {
        // 術者が効果時間を更新した後に描画します。オーラ用のタイマーは持ちません。
        bool visible = _owner != null && _owner.IsPlayer && _owner.Health > 0 && _game != null && !_game.IsRunFinished;
        Refresh(_protection, visible && _owner.IsProtected, _owner != null ? _owner.ProtectionSecondsRemaining : 0,
            BrawlerBalance.ProtectionWarningSeconds, BrawlerBalance.ProtectionAuraColor);
        Refresh(_attackBoost, visible && _owner.IsAttackBoosted, _owner != null ? _owner.AttackBoostSecondsRemaining : 0,
            BrawlerBalance.AttackBoostWarningSeconds, BrawlerBalance.AttackBoostAuraColor);
    }

    private static void Refresh(LineRenderer line, bool visible, float remaining, float warningSeconds, Color color)
    {
        line.enabled = visible;
        if (!visible)
        {
            return;
        }
        color.a = BrawlerBalance.SupportAuraAlpha;
        if (remaining <= warningSeconds)
        {
            // 実時間やTime.timeを使わず、残り時間から点滅位相を計算します。
            // ヒットストップ中も残り時間が同じなら、点滅の見た目をそのまま保持します。
            float elapsed = warningSeconds - remaining;
            float pulse = (1 + Mathf.Cos(elapsed * BrawlerBalance.SupportAuraWarningPulsesPerSecond * Mathf.PI * 2)) / 2;
            color.a = Mathf.Lerp(BrawlerBalance.SupportAuraWarningMinAlpha, BrawlerBalance.SupportAuraWarningMaxAlpha, pulse);
        }
        line.startColor = color;
        line.endColor = color;
    }
}
