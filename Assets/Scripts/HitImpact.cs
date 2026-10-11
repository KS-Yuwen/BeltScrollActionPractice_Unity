using UnityEngine;

// 打撃エフェクトは実時間で広がって消えます。ヒットストップ中も見た目だけは動かします。
public sealed class HitImpact : MonoBehaviour
{
    private float _elapsed;
    private float _size = 1;
    private float _duration = BrawlerBalance.NormalImpactSeconds;

    public BrawlerDefines.ImpactKind Kind { get; private set; }

    public void Initialize(float size, float duration = BrawlerBalance.NormalImpactSeconds,
        BrawlerDefines.ImpactKind kind = BrawlerDefines.ImpactKind.Normal)
    {
        _size = size;
        _duration = Mathf.Max(duration, Mathf.Epsilon);
        Kind = kind;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        Advance(Time.unscaledDeltaTime);
    }

    // 実時間を受け取る窓口を分け、停止中も演出が進むことを検証可能にします。
    public void Advance(float unscaledDeltaTime)
    {
        _elapsed += Mathf.Max(0, unscaledDeltaTime);
        // 一瞬広がってから縮み、0.18秒で消します。素材を個別に複製する必要はありません。
        float fraction = Mathf.Clamp01(_elapsed / _duration);
        float scale = (1 + fraction) * (1 - fraction) * _size;
        transform.localScale = Vector3.one * scale;
        if (fraction >= 1)
        {
            Destroy(gameObject);
        }
    }
}
