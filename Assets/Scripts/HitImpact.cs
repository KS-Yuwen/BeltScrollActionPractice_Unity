using UnityEngine;

// 打撃エフェクトは実時間で広がって消えます。ヒットストップ中も見た目だけは動かします。
public sealed class HitImpact : MonoBehaviour
{
    private float _elapsed;
    private float _size = 1;

    public void Initialize(float size)
    {
        _size = size;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        // 一瞬広がってから縮み、0.18秒で消します。素材を個別に複製する必要はありません。
        float fraction = Mathf.Clamp01(_elapsed / 0.18f);
        float scale = (1 + fraction) * (1 - fraction) * _size;
        transform.localScale = Vector3.one * scale;
        if (fraction >= 1)
        {
            Destroy(gameObject);
        }
    }
}
