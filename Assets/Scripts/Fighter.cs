using UnityEngine;

// キャラクター単体の管理役。入力または AI、移動、攻撃演出、被ダメージを担当します。
public sealed class Fighter : MonoBehaviour
{
    private BeltBrawler _game;
    private Transform _visual;
    private Transform _attackArm;
    private Transform _rightLeg;
    private Transform _leftLeg;

    // _attackRemaining = 攻撃演出の残り秒数、_attackCooldown = 次の攻撃までの待ち時間。
    // _stunRemaining = 被ダメージによる硬直の残り秒数、_lastAttackTime = 前回攻撃を開始した時刻。
    // _walkPhase = 足を振るための位相。_lastAttackTime の初期値は最初の攻撃を 1 段目にするための値です。
    private float _attackRemaining;
    private float _attackCooldown;
    private float _stunRemaining;
    private float _lastAttackTime = -10;
    private float _walkPhase;
    // 現在のコンボ段数（1～3）。
    private int _comboStep;
    // 1 回の攻撃でダメージ判定を繰り返さないためのフラグ。
    private bool _hasDealtHit;
    // 被ダメージ時に押し戻される速度。
    private Vector3 _knockbackVelocity;
    // ダッシュ中だけ入力方向に高速移動し、ダメージを無効にします。
    // 再使用の待ち時間を設け、回避を連打するだけにならないようにしています。
    private float _dashRemaining;
    private float _dashCooldown;
    private float _windupRemaining;
    private float _flashRemaining;
    private Vector3 _dashDirection;
    private float _attackBufferRemaining;
    private Renderer[] _bodyRenderers;
    private MaterialPropertyBlock _flashProperties;

    // 状態の読み取りは公開し、変更はこのクラスのメソッドを通して行います。
    // private set により、他クラスが HP や向きを直接書き換えることを防ぎます。
    public bool IsPlayer { get; private set; }

    public int Health { get; private set; }

    public int MaxHealth { get; private set; }

    // 右向きは +1、左向きは -1。見た目と攻撃判定で共用します。
    public float Facing { get; private set; } = 1;

    public bool DashReady => _dashCooldown <= 0;

    public bool IsWindingUp => _windupRemaining > 0;

    // 生成直後に管理役から一度だけ呼び、戦闘に必要な状態とパーツをまとめて渡します。
    // Start より前に初期化を完了するため、Start で安全に Renderer を取得できます。
    public void Initialize(BeltBrawler game, bool isPlayer, int maxHealth,
        Transform visual, Transform attackArm, Transform rightLeg, Transform leftLeg)
    {
        _game = game;
        IsPlayer = isPlayer;
        MaxHealth = maxHealth;
        Health = maxHealth;
        _visual = visual;
        _attackArm = attackArm;
        _rightLeg = rightLeg;
        _leftLeg = leftLeg;
    }

    // HP の変更を Fighter 内に集約し、最大 HP を超えないという約束を守ります。
    public void RestoreHealth(int amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
    }

    private void Start()
    {
        // Unity のネイティブ機能を使うオブジェクトはフィールド初期化では作らず、Start で作ります。
        _flashProperties = new MaterialPropertyBlock();
        // パーツ生成後に Renderer を集めます。素材を複製せず、個体ごとに色を上書きします。
        _bodyRenderers = _visual.GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        // 倒れた後は移動や攻撃の更新を止めます。
        if (Health <= 0)
        {
            return;
        }
        float deltaTime = Time.deltaTime;
        // 更新順もゲームの挙動の一部です。元の順序を維持し、担当する処理ごとに分けます。
        UpdateTimers(deltaTime);
        Vector3 input = ReadPlayerInput();
        bool wasWindingUp = UpdateAttackWindup(deltaTime);
        ApplyKnockback(deltaTime);
        Vector3 movement = GetMovement(input, wasWindingUp);
        Move(movement, deltaTime);
        UpdateWalkAnimation(movement, deltaTime);
        UpdateAttackAnimation(deltaTime);
        UpdateVisualFeedback();
    }

    // 残り時間は経過秒数で減らし、フレームレートが違っても同じ時間になるようにします。
    private void UpdateTimers(float deltaTime)
    {
        _attackCooldown -= deltaTime;
        _stunRemaining -= deltaTime;
        _dashCooldown -= deltaTime;
        _flashRemaining -= deltaTime;
        _attackBufferRemaining = Mathf.Max(0, _attackBufferRemaining - deltaTime);
    }

    // 入力の読み取りと先行受付だけを担当し、通常移動に使う方向を返します。
    private Vector3 ReadPlayerInput()
    {
        Vector3 input = Vector3.zero;
        if (IsPlayer)
        {
            input = BrawlerInput.ReadMovement();
            // 次の攻撃が可能になる直前の入力も 0.2 秒間覚え、コンボをつなぎやすくします。
            if (BrawlerInput.WasAttackPressed())
            {
                _attackBufferRemaining = 0.2f;
            }
            // 攻撃の後隙からも回避できますが、被ダメージ硬直中は回避できません。
            if (BrawlerInput.WasDashPressed() && DashReady && _stunRemaining <= 0)
            {
                // 回避距離はスティックの倒し具合によらず一定にするため、回避方向だけ正規化します。
                _dashDirection = input.sqrMagnitude > 0 ? input.normalized : new Vector3(Facing, 0, 0);
                _dashRemaining = 0.2f;
                _dashCooldown = 0.85f;
                _attackRemaining = 0;
                _attackBufferRemaining = 0;
                _knockbackVelocity = Vector3.zero;
            }
        }
        return input;
    }

    private bool UpdateAttackWindup(float deltaTime)
    {
        // 敵は 0.55 秒の予告中に足を止め、攻撃方向を固定します。
        // プレイヤーが奥行き方向へ逃げれば、その場所に空振りさせられます。
        bool wasWindingUp = _windupRemaining > 0;
        if (wasWindingUp)
        {
            _windupRemaining -= deltaTime;
            if (_windupRemaining <= 0 && _game.Target.Health > 0)
            {
                Attack();
            }
        }
        // このフレーム開始時に予告中だったかを返し、予告終了フレームにも足を止めます。
        return wasWindingUp;
    }

    private void ApplyKnockback(float deltaTime)
    {
        // 速度に経過秒数を掛けて移動量へ変換し、ノックバック速度を徐々にゼロへ近づけます。
        transform.position += _knockbackVelocity * deltaTime;
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, deltaTime * 12);
    }

    // プレイヤー入力か敵 AI のどちらかから、そのフレームの通常移動方向を決めます。
    private Vector3 GetMovement(Vector3 input, bool wasWindingUp)
    {
        Vector3 movement = Vector3.zero;
        // 攻撃中・被ダメージ硬直中は、新しい操作や AI の行動を受け付けません。
        if (_stunRemaining <= 0 && _attackRemaining <= 0 && _dashRemaining <= 0 && !wasWindingUp)
        {
            if (IsPlayer)
            {
                // 入力の縦方向は Y ではなく Z に対応させます。
                // 入力側で長さを最大 1 に制限し、アナログの倒し具合も維持しています。
                movement = input;
                // 記憶した入力を一度消費して攻撃します。押しっぱなしで自動連打にはなりません。
                if (_attackCooldown <= 0 && _attackBufferRemaining > 0)
                {
                    Attack();
                    _attackBufferRemaining = 0;
                    movement = Vector3.zero;
                }
            }
            else if (_game.Target.Health > 0)
            {
                // 敵はプレイヤーへの差分ベクトルを求め、十分に近づいたら攻撃します。
                Vector3 delta = _game.Target.transform.position - transform.position;
                Facing = delta.x >= 0 ? 1 : -1;
                if (Mathf.Abs(delta.x) > 1.1f || Mathf.Abs(delta.z) > 0.5f)
                {
                    movement = delta.normalized;
                }
                else if (_attackCooldown <= 0)
                {
                    _windupRemaining = 0.55f;
                    movement = Vector3.zero;
                }
            }
        }
        return movement;
    }

    private void Move(Vector3 movement, float deltaTime)
    {
        // 奥行きだけの移動では向きを維持し、左右に動いた場合だけ向きを変更します。
        if (Mathf.Abs(movement.x) > 0.01f)
        {
            Facing = Mathf.Sign(movement.x);
        }
        transform.position += movement * (IsPlayer ? 5 : 2.2f) * deltaTime;
        // 最終フレームの移動量を残り時間で制限し、FPS が低いときの飛びすぎを防ぎます。
        if (_dashRemaining > 0)
        {
            transform.position += _dashDirection * 13 * Mathf.Min(deltaTime, _dashRemaining);
            _dashRemaining = Mathf.Max(0, _dashRemaining - deltaTime);
        }
        Vector3 position = transform.position;
        // ステージの端で位置を制限し、高さは常に床の Y = 0 に固定します。
        transform.position = new Vector3(Mathf.Clamp(position.x, -22, 22), 0, Mathf.Clamp(position.z, -3, 3));
    }

    private void UpdateWalkAnimation(Vector3 movement, float deltaTime)
    {
        // Animator の代わりに Sin で左右の足を逆方向に振る、仮の歩行演出です。
        _walkPhase += movement.magnitude * deltaTime * 12;
        _rightLeg.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_walkPhase) * movement.magnitude * 20);
        _leftLeg.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(_walkPhase) * movement.magnitude * 20);
        // X を負にすると Model が左右反転します。
        _visual.localScale = new Vector3(Facing, 1, 1);
    }

    // 腕の演出に合わせて命中判定を一度だけ発生させます。
    private void UpdateAttackAnimation(float deltaTime)
    {
        if (_attackRemaining > 0)
        {
            _attackRemaining -= deltaTime;
            // 攻撃中は腕を横に伸ばします。角度から Quaternion を作って回転を指定します。
            _attackArm.localRotation = Quaternion.Euler(0, 0, 90);
            _attackArm.localPosition = new Vector3(0.65f, 1.35f, -0.1f);
            // 攻撃開始から約 0.14 秒後に一度だけ命中判定。3 段目はダメージを増やします。
            if (!_hasDealtHit && _attackRemaining < 0.16f)
            {
                _hasDealtHit = true;
                _game.Hit(this, IsPlayer ? (_comboStep == 3 ? 30 : 18) : 10);
            }
        }
        else
        {
            // 回転なしの状態に戻します。
            _attackArm.localRotation = Quaternion.identity;
            _attackArm.localPosition = new Vector3(0.4f, 1.2f, -0.1f);
        }
    }

    private void UpdateVisualFeedback()
    {
        // 被ダメージ中は見た目だけ少し浮かせます。攻撃距離を測る親の座標は変わりません。
        _visual.localPosition = new Vector3(0, _stunRemaining > 0 ? 0.08f : 0, 0);
        // 被ダメージは白、回避中は水色、敵の攻撃予告は黄色で見分けられるようにします。
        // SetPropertyBlock(null) で上書きを解除すると、元の各パーツの色に戻ります。
        foreach (var body in _bodyRenderers)
        {
            if (_flashRemaining > 0 || _dashRemaining > 0 || _windupRemaining > 0)
            {
                _flashProperties.SetColor("_Color", _flashRemaining > 0 ? Color.white : _dashRemaining > 0 ? Color.cyan : Color.yellow);
                body.SetPropertyBlock(_flashProperties);
            }
            else
            {
                body.SetPropertyBlock(null);
            }
        }
    }

    // 攻撃を開始し、演出時間・次の攻撃までの時間・コンボ段数を設定します。
    private void Attack()
    {
        // 前回の開始から 0.85 秒未満なら次の段へ進み、それ以上なら 1 段目に戻します。
        // 剰余演算 % によって、3 段目の次は 1 段目になります。入力は Update で先行受付します。
        _comboStep = Time.time - _lastAttackTime < 0.85f ? _comboStep % 3 + 1 : 1;
        _lastAttackTime = Time.time;
        _attackRemaining = 0.3f;
        // プレイヤーは 3 段目の後に長めの隙を作り、敵は攻撃間隔を長くして避けやすくします。
        _attackCooldown = IsPlayer ? (_comboStep == 3 ? 0.55f : 0.32f) : 1.2f;
        _hasDealtHit = false;
    }

    // 攻撃が当たった相手側で呼び出されます。direction は攻撃者の向きです。
    public void TakeDamage(int amount, float direction)
    {
        // 硬直中は追加ダメージを受けないため、短い無敵時間も兼ねています。
        if (Health <= 0 || _stunRemaining > 0 || _dashRemaining > 0)
        {
            return;
        }
        Health = Mathf.Max(0, Health - amount);
        _stunRemaining = 0.25f;
        // 被ダメージによって進行中の攻撃を中断します。
        _attackRemaining = 0;
        // 予告中に殴れば敵の攻撃を止められます。
        _windupRemaining = 0;
        _attackCooldown = Mathf.Max(_attackCooldown, IsPlayer ? 0.25f : 0.6f);
        _flashRemaining = 0.12f;
        // コンボ最終段では大きく吹き飛ばし、敵との間合いを作ります。
        _knockbackVelocity = new Vector3(direction * (amount >= 30 ? 9 : 5), 0, 0);
        _game.RegisterDamage(this);
        if (Health == 0)
        {
            // 仮の倒れる演出。敵だけ 0.5 秒後に消し、プレイヤーは敗北表示のため残します。
            _visual.localRotation = Quaternion.Euler(0, 0, 80);
            if (!IsPlayer)
            {
                Destroy(gameObject, 0.5f);
            }
        }
    }
}
