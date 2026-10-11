using UnityEngine;

// 敵の足元の攻撃予告。戦闘状態はFighterから読み、演出だけを担当します。
public sealed class EnemyAttackWarning : MonoBehaviour
{
    private Fighter _owner;
    private Renderer[] _renderers;
    private Transform _directionMarker;
    private MaterialPropertyBlock _properties;
    private Transform _frontEdge;
    private Transform _backEdge;
    private Transform _rightEdge;
    private Transform _leftEdge;

    public bool IsVisible { get; private set; }

    public Color CurrentColor { get; private set; }

    public void Initialize(Fighter owner, Transform directionMarker)
    {
        _owner = owner;
        _directionMarker = directionMarker;
        _renderers = GetComponentsInChildren<Renderer>();
        _properties = new MaterialPropertyBlock();
        _frontEdge = transform.Find("Front edge");
        _backEdge = transform.Find("Back edge");
        _rightEdge = transform.Find("Right edge");
        _leftEdge = transform.Find("Left edge");
        Refresh();
    }

    private void LateUpdate()
    {
        // 戦闘のUpdateが終わった後に読み、攻撃開始・中断・死亡したフレームで非表示にします。
        Refresh();
    }

    public void Refresh()
    {
        IsVisible = _owner != null && !_owner.IsPlayer && _owner.Health > 0 && _owner.IsWindingUp;
        CurrentColor = Color.Lerp(Color.yellow, Color.red, IsVisible ? _owner.AttackWindupProgress : 0);
        _properties.SetColor("_Color", CurrentColor);
        foreach (Renderer marker in _renderers)
        {
            marker.enabled = IsVisible;
            marker.SetPropertyBlock(_properties);
        }
        if (_owner != null)
        {
            // Modelの鏡像反転に巻き込まれないよう、床上の親座標で向きを表します。
            float start = _owner.WarningRangeStart;
            float end = _owner.WarningRangeEnd;
            float center = (start + end) / 2;
            float width = end - start;
            float lane = _owner.WarningLaneHalfWidth;
            float thickness = BrawlerBalance.WarningMarkerThickness;
            // 枠は床上の攻撃範囲／射線です。左右反転しても奥行きの幅は変えません。
            _frontEdge.localPosition = new Vector3(_owner.Facing * center, 0, lane);
            _backEdge.localPosition = new Vector3(_owner.Facing * center, 0, -lane);
            _frontEdge.localScale = _backEdge.localScale = new Vector3(width, thickness, thickness);
            _rightEdge.localPosition = Vector3.right * (_owner.Facing * end);
            _leftEdge.localPosition = Vector3.right * (_owner.Facing * start);
            _rightEdge.localScale = _leftEdge.localScale = new Vector3(thickness, thickness, lane * 2);
            _directionMarker.localPosition = Vector3.right * (_owner.Facing * (end + BrawlerBalance.WarningDirectionLength));
        }
    }
}
