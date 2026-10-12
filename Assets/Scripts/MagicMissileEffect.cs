using UnityEngine;

// ダメージを担当せず、術者の発動時間に同期して光の幅を細くする演出だけを管理します。
public sealed class MagicMissileEffect : MonoBehaviour
{
    private SpellFighter _caster;
    private LineRenderer _line;

    public void Initialize(SpellFighter caster, LineRenderer line)
    {
        _caster = caster;
        _line = line;
    }

    private void Update()
    {
        // 固定時間のDestroy予約にせず、終了・中断・死亡・破棄でも演出を確実に終了します。
        if (_caster == null || _caster.Health <= 0 || !_caster.IsReleasingSpell || !_caster.IsSpellEffectActive)
        {
            Destroy(gameObject);
            return;
        }
        float width = BrawlerBalance.MagicMissileTrailWidth * (1 - Mathf.Clamp01(_caster.SpellEffectProgress));
        _line.startWidth = width;
        _line.endWidth = width;
    }
}
