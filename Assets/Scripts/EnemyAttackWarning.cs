using UnityEngine;

// 敵の足元の攻撃予告。戦闘状態はFighterから読み、演出だけを担当します。
public sealed class EnemyAttackWarning : MonoBehaviour
{
    private Fighter _owner;
    private Renderer[] _renderers;
    private Transform _directionMarker;
    private Renderer _directionRenderer;
    private MaterialPropertyBlock _properties;
    private Transform _frontEdge;
    private Transform _backEdge;
    private Transform _rightEdge;
    private Transform _leftEdge;

    public bool IsVisible { get; private set; }

    public Color CurrentColor { get; private set; }

    // 緑の枠は攻撃範囲ではなく、反撃できる相手の足元を示します。
    public bool IsShowingOpening { get; private set; }

    public void Initialize(Fighter owner, Transform directionMarker)
    {
        _owner = owner;
        _directionMarker = directionMarker;
        _directionRenderer = directionMarker.GetComponent<Renderer>();
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
        bool canShow = _owner != null && !_owner.IsPlayer && _owner.Health > 0;
        IsShowingOpening = canShow && _owner.OpeningSecondsRemaining > 0 && !_owner.IsWindingUp;
        IsVisible = canShow && (_owner.IsWindingUp || IsShowingOpening);
        CurrentColor = IsShowingOpening ? BrawlerBalance.BossOpeningColor
            : _owner != null ? Color.Lerp(_owner.WarningStartColor, _owner.WarningEndColor,
                _owner.IsWindingUp ? _owner.AttackWindupProgress : 0) : Color.yellow;
        _properties.SetColor("_Color", CurrentColor);
        foreach (Renderer marker in _renderers)
        {
            marker.enabled = IsVisible;
            marker.SetPropertyBlock(_properties);
        }
        if (_owner != null)
        {
            // Modelの鏡像反転に巻き込まれないよう、床上の親座標で向きを表します。
            // 隙の表示は左右対称の小さな枠にし、直前の攻撃範囲と混同しないようにします。
            float start = IsShowingOpening ? -BrawlerBalance.OpeningMarkerHalfSize : _owner.WarningRangeStart;
            float end = IsShowingOpening ? BrawlerBalance.OpeningMarkerHalfSize : _owner.WarningRangeEnd;
            float center = (start + end) / 2;
            float width = end - start;
            float lane = IsShowingOpening ? BrawlerBalance.OpeningMarkerHalfSize : _owner.WarningLaneHalfWidth;
            float thickness = BrawlerBalance.WarningMarkerThickness;
            // 枠は床上の攻撃範囲／射線です。左右反転しても奥行きの幅は変えません。
            _frontEdge.localPosition = new Vector3(_owner.Facing * center, 0, lane);
            _backEdge.localPosition = new Vector3(_owner.Facing * center, 0, -lane);
            _frontEdge.localScale = _backEdge.localScale = new Vector3(width, thickness, thickness);
            _rightEdge.localPosition = Vector3.right * (_owner.Facing * end);
            _leftEdge.localPosition = Vector3.right * (_owner.Facing * start);
            _rightEdge.localScale = _leftEdge.localScale = new Vector3(thickness, thickness, lane * 2);
            // 反撃の隙には攻撃方向がないため、矢印部分だけ非表示にします。
            _directionRenderer.enabled = IsVisible && !IsShowingOpening;
            _directionMarker.localPosition = Vector3.right * (_owner.Facing * (end + BrawlerBalance.WarningDirectionLength));
        }
    }
}
