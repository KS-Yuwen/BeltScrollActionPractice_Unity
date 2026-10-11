using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 撃破された敵だけが持つ演出。生存中のダウンでは追加しません。
public sealed class EnemyDefeatFade : MonoBehaviour
{
    private Fighter _owner;
    private Renderer[] _renderers;
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private MaterialPropertyBlock _properties;
    private float _elapsed;

    public float Alpha => 1 - Mathf.Clamp01(_elapsed / BrawlerBalance.EnemyRemovalSeconds);

    public void Initialize(Fighter owner, Transform visual)
    {
        _owner = owner;
        _renderers = visual.GetComponentsInChildren<Renderer>();
        _properties = new MaterialPropertyBlock();
        var fadeMaterials = new Dictionary<Material, Material>();
        foreach (Renderer body in _renderers)
        {
            Material source = body.sharedMaterial;
            if (!fadeMaterials.TryGetValue(source, out Material fade))
            {
                // 元の素材は他の敵とも共有しているので、撃破した個体の分だけ複製します。
                fade = new Material(source);
                fade.SetFloat("_Mode", BrawlerDefines.StandardShaderFadeMode);
                fade.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                fade.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                fade.SetInt("_ZWrite", 0);
                fade.DisableKeyword("_ALPHATEST_ON");
                fade.EnableKeyword("_ALPHABLEND_ON");
                fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                fade.renderQueue = (int)RenderQueue.Transparent;
                fadeMaterials.Add(source, fade);
                _ownedMaterials.Add(fade);
            }
            body.sharedMaterial = fade;
        }
        ApplyOpacity();
    }

    private void Update()
    {
        // 既存の撃破後0.5秒と同じゲーム内時間。ヒットストップ中には消滅を進めません。
        Advance(Time.deltaTime);
    }

    public void Advance(float deltaTime)
    {
        _elapsed = Mathf.Min(BrawlerBalance.EnemyRemovalSeconds, _elapsed + Mathf.Max(0, deltaTime));
        ApplyOpacity();
        if (Alpha <= 0)
        {
            Destroy(_owner.gameObject);
        }
    }

    private void ApplyOpacity()
    {
        foreach (Renderer body in _renderers)
        {
            // 被ダメージの白点滅を残さず、元の体色から透明へ変化させます。
            _properties.Clear();
            Color color = body.sharedMaterial.color;
            color.a *= Alpha;
            _properties.SetColor("_Color", color);
            body.SetPropertyBlock(_properties);
        }
    }

    private void OnDestroy()
    {
        // この演出で作った素材だけを解放し、元の共有素材を変更・破棄しません。
        foreach (Material material in _ownedMaterials)
        {
            Destroy(material);
        }
    }
}
