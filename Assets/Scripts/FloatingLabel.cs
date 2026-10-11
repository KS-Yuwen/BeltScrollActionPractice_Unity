using UnityEngine;

// 頭上表示の共通処理。ダメージ・回復・取得通知で上昇とフェードを共用します。
public abstract class FloatingLabel : MonoBehaviour
{
    private Camera _camera;
    private float _elapsed;
    private GUIStyle _style;
    private string _text;

    private float _duration;
    private float _width;
    private int _fontSize;
    private Font _font;

    public string DisplayText => _text;
    public Color DisplayColor { get; private set; }
    public float Alpha => Mathf.Clamp01(1 - _elapsed / Mathf.Max(_duration, Mathf.Epsilon));

    protected void InitializeLabel(Camera camera, string text, Color color, int fontSize,
        float duration, float width, Font font = null)
    {
        _camera = camera;
        _text = text;
        DisplayColor = color;
        _fontSize = fontSize;
        _duration = duration;
        _width = width;
        _font = font;
    }

    protected virtual void Update()
    {
        Advance(Time.unscaledDeltaTime);
    }

    public void Advance(float unscaledDeltaTime)
    {
        float step = Mathf.Min(Mathf.Max(0, unscaledDeltaTime),
            Mathf.Max(0, _duration - _elapsed));
        _elapsed += step;
        transform.position += Vector3.up * (BrawlerBalance.DamageNumberRiseSpeed * step);
        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnGUI()
    {
        if (_camera == null || Alpha <= 0)
        {
            return;
        }
        Vector3 screen = _camera.WorldToScreenPoint(transform.position);
        if (screen.z <= 0)
        {
            return;
        }
        // GUI.skinはOnGUI内で参照します。スタイル・文字列は毎描画で作り直しません。
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = _fontSize,
                font = _font != null ? _font : GUI.skin.label.font
            };
        }
        Color previousColor = GUI.color;
        Color color = DisplayColor;
        color.a = Alpha;
        GUI.color = color;
        GUI.Label(new Rect(screen.x - _width / 2,
            Screen.height - screen.y - BrawlerBalance.DamageNumberLabelHeight / 2,
            _width, BrawlerBalance.DamageNumberLabelHeight), _text, _style);
        GUI.color = previousColor;
    }
}
