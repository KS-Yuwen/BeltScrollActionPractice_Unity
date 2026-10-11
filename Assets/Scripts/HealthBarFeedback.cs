using UnityEngine;

// 表示専用のHP残像。実際のHPを変更せず、前回との差と経過時間だけで更新します。
public sealed class HealthBarFeedback
{
    private int _previousHealth;
    private float _delayRemaining;
    private bool _initialized;

    public float TrailHealth { get; private set; }

    public void Refresh(int health, int maxHealth, float deltaTime)
    {
        int current = Mathf.Clamp(health, 0, maxHealth);
        if (!_initialized || current > _previousHealth)
        {
            // 初期化・回復では残像を現在HPへ合わせ、回復後に古い被ダメージ量を残しません。
            TrailHealth = current;
            _delayRemaining = 0;
            _initialized = true;
        }
        else if (current < _previousHealth)
        {
            // 連続被弾では今見えている残像を保ち、最後の被弾から待機をやり直します。
            TrailHealth = Mathf.Max(TrailHealth, _previousHealth);
            _delayRemaining = BrawlerBalance.HealthTrailDelaySeconds;
        }
        _previousHealth = current;
        float elapsed = Mathf.Max(0, deltaTime);
        float waiting = Mathf.Min(_delayRemaining, elapsed);
        _delayRemaining -= waiting;
        elapsed -= waiting;
        TrailHealth = Mathf.MoveTowards(TrailHealth, current,
            maxHealth * BrawlerBalance.HealthTrailFractionPerSecond * elapsed);
    }
}
