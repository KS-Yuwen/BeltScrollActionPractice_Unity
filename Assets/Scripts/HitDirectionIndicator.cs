using UnityEngine;

// プレイヤーへの有効な被弾だけを受け取る方向表示。向きや移動には影響しません。
public sealed class HitDirectionIndicator
{
    private GUIStyle _style;
    public float SourceSide { get; private set; }
    public float RemainingSeconds { get; private set; }
    public float Alpha => Mathf.Clamp01(RemainingSeconds / BrawlerBalance.HitDirectionFadeSeconds);

    public void Show(float attackDirection)
    {
        // 攻撃が右へ進んだなら、攻撃元は左。キャラクターの向きとは独立して固定します。
        SourceSide = -Mathf.Sign(attackDirection);
        RemainingSeconds = BrawlerBalance.HitDirectionSeconds;
    }

    public void Advance(float unscaledDeltaTime)
    {
        RemainingSeconds = Mathf.Max(0, RemainingSeconds - Mathf.Max(0, unscaledDeltaTime));
    }

    public void Draw(Fighter player, Camera camera)
    {
        if (RemainingSeconds <= 0 || player == null || camera == null) return;
        Vector3 position = player.transform.position + Vector3.up * BrawlerBalance.HitDirectionWorldHeight
            + Vector3.right * (SourceSide * BrawlerBalance.HitDirectionWorldOffset);
        Vector3 screen = camera.WorldToScreenPoint(position);
        if (screen.z <= 0) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = BrawlerBalance.HitDirectionFontSize
            };
        }
        Color previousColor = GUI.color;
        GUI.color = new Color(1, 0.15f, 0.15f, Alpha);
        float size = BrawlerBalance.HitDirectionLabelSize;
        GUI.Label(new Rect(screen.x - size / 2, Screen.height - screen.y - size / 2, size, size),
            SourceSide < 0 ? "<" : ">", _style);
        GUI.color = previousColor;
    }
}
