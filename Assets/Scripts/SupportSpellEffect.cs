using System.Collections.Generic;
using UnityEngine;

// 回復・防御・攻撃強化の発動演出です。効果や回数には触れず、術者の発動時間だけを参照します。
public sealed class SupportSpellEffect : MonoBehaviour
{
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();
    private SpellFighter _caster;
    private BrawlerDefines.UsableAction _action;
    private Color _color;
    private Light _light;
    private Transform _ring;
    private Transform _symbols;

    public void Initialize(SpellFighter caster, BrawlerDefines.UsableAction action, Material material, Color color)
    {
        _caster = caster;
        _action = action;
        _color = color;
        // キャラクターの向きはVisualだけに適用されます。術者を親にし、移動と破棄に追従します。
        transform.SetParent(caster.transform, false);
        _symbols = new GameObject("Spell symbols").transform;
        _symbols.SetParent(transform, false);
        switch (action)
        {
            case BrawlerDefines.UsableAction.Healing:
                CreateHealing(material);
                break;
            case BrawlerDefines.UsableAction.Protection:
                CreateShield(material);
                break;
            case BrawlerDefines.UsableAction.AttackBoost:
                CreateBoost(material);
                break;
        }
        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = color;
        _light.range = BrawlerBalance.SupportSpellLightRange;
        _light.intensity = 0;
        _light.shadows = LightShadows.None;
        Update();
    }

    private void CreateHealing(Material material)
    {
        // 床の輪はXZ平面。十字は画面から見えるXY平面に置きます。
        int segments = BrawlerBalance.SupportSpellRingSegments;
        var points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2 * i / segments;
            points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * BrawlerBalance.SupportSpellRingRadius;
        }
        _ring = CreateLine("Healing ring", transform, points, true, material).transform;
        _ring.localPosition = Vector3.up * BrawlerBalance.SupportSpellGroundHeight;
        float size = BrawlerBalance.SupportSpellSymbolSize;
        Vector3 center = new Vector3(0, BrawlerBalance.SupportSpellSymbolHeight, -BrawlerBalance.SupportSpellFrontOffset);
        CreateLine("Healing cross horizontal", _symbols,
            new[] { center + Vector3.left * size, center + Vector3.right * size }, false, material);
        CreateLine("Healing cross vertical", _symbols,
            new[] { center + Vector3.down * size, center + Vector3.up * size }, false, material);
    }

    private void CreateShield(Material material)
    {
        // 色だけでなく先端のある盾の輪郭でも、防御魔法であることを示します。
        float size = BrawlerBalance.SupportSpellShieldSize;
        Vector3 center = new Vector3(0, BrawlerBalance.SupportSpellSymbolHeight, -BrawlerBalance.SupportSpellFrontOffset);
        CreateLine("Protection shield", _symbols, new[]
        {
            center + new Vector3(-size, size, 0),
            center + new Vector3(size, size, 0),
            center + Vector3.right * size,
            center + Vector3.down * size,
            center + Vector3.left * size
        }, true, material);
    }

    private void CreateBoost(Material material)
    {
        // 上向きの山形を重ね、攻撃力の上昇を回復の十字と区別します。
        float size = BrawlerBalance.SupportSpellSymbolSize;
        for (int i = 0; i < BrawlerBalance.SupportSpellBoostSymbolCount; i++)
        {
            Vector3 center = new Vector3(0, BrawlerBalance.SupportSpellBoostBaseHeight
                + i * BrawlerBalance.SupportSpellBoostSymbolGap, -BrawlerBalance.SupportSpellFrontOffset);
            CreateLine("Attack boost chevron", _symbols, new[]
            {
                center + Vector3.left * size,
                center + Vector3.up * size,
                center + Vector3.right * size
            }, false, material);
        }
    }

    private LineRenderer CreateLine(string label, Transform parent, Vector3[] points, bool loop, Material material)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        // 共有素材を変更せず、線ごとの頂点色で透明度を管理します。Colliderも作りません。
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        _lines.Add(line);
        return line;
    }

    private void Update()
    {
        // タイマーを二重に持たず、敵停止と同じ発動状態に同期します。
        // 死亡・中断・ゲーム終了でも即座に非表示にし、フレーム末尾で破棄します。
        if (_caster == null || _caster.Health <= 0 || !_caster.IsReleasingSpell || !_caster.IsSpellEffectActive)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        float progress = Mathf.Clamp01(_caster.SpellEffectProgress);
        Color color = _color;
        color.a = 1 - progress;
        float width = BrawlerBalance.SupportSpellLineWidth * (1 - progress);
        foreach (LineRenderer line in _lines)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }
        _light.intensity = Mathf.Sin(progress * Mathf.PI) * BrawlerBalance.SupportSpellLightIntensity;
        if (_action == BrawlerDefines.UsableAction.Protection)
        {
            _symbols.localScale = Vector3.one * Mathf.Lerp(1, BrawlerBalance.SupportSpellShieldExpansion, progress);
        }
        else
        {
            _symbols.localPosition = Vector3.up * (progress * BrawlerBalance.SupportSpellSymbolRise);
            if (_ring != null)
            {
                _ring.localScale = Vector3.one * Mathf.Lerp(1, BrawlerBalance.SupportSpellRingExpansion, progress);
            }
        }
    }
}
