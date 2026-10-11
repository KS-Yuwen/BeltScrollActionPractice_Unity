using UnityEngine;

// ウェーブの区切りを知らせる中央表示。表示時間だけを管理し、敵の生成や回復は行いません。
public sealed class WaveBanner
{
    private GUIStyle _style;
    private Color _color;
    public string Text { get; private set; } = string.Empty;
    public float RemainingSeconds { get; private set; }
    public float Alpha => Mathf.Clamp01(RemainingSeconds / BrawlerBalance.WaveBannerFadeSeconds);

    public void ShowStart(int wave)
    {
        Text = "WAVE " + wave;
        _color = Color.cyan;
        RemainingSeconds = BrawlerBalance.WaveBannerSeconds;
    }

    public void ShowClear()
    {
        Text = "WAVE CLEAR";
        _color = Color.yellow;
        RemainingSeconds = BrawlerBalance.WaveBannerSeconds;
    }

    public void Advance(float deltaTime)
    {
        RemainingSeconds = Mathf.Max(0, RemainingSeconds - Mathf.Max(0, deltaTime));
        if (RemainingSeconds <= 0)
        {
            Clear();
        }
    }

    public void Clear()
    {
        Text = string.Empty;
        RemainingSeconds = 0;
    }

    public void Draw()
    {
        if (RemainingSeconds <= 0) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = BrawlerBalance.WaveBannerFontSize
            };
        }
        // 画面中央に置き、既存の左上HUDや下部の選択操作と分けます。
        float width = Mathf.Min(Screen.width, BrawlerBalance.WaveBannerWidth);
        var area = new Rect((Screen.width - width) / 2, (Screen.height - BrawlerBalance.WaveBannerHeight) / 2,
            width, BrawlerBalance.WaveBannerHeight);
        Color previousColor = GUI.color;
        Color color = _color;
        color.a = Alpha;
        GUI.color = color;
        GUI.Label(area, Text, _style);
        GUI.color = previousColor;
    }
}
