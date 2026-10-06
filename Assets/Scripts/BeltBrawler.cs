using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ゲーム全体の管理役。ステージ生成、敵の出現、攻撃判定、カメラ、画面表示を担当します。
// MonoBehaviour を継承すると、Unity が Start / Update などを自動的に呼び出します。
// 座標の約束：X = 左右、Y = 高さ、Z = 道路の奥行き。移動は XZ 平面で行います。
public sealed class BeltBrawler : MonoBehaviour
{
    // 生存中と撃破直後の敵を保持します。破棄された敵は Update で取り除きます。
    private readonly List<Fighter> _enemies = new List<Fighter>();
    private Fighter _player;
    private Camera _camera;
    // 現在の敵グループの番号。出現のたびに増えます。
    private int _wave;
    // 全滅後、次の出現までに経過した秒数。
    private float _nextWaveElapsed;
    private Material _floorMaterial;
    private Material _playerMaterial;
    private Material _enemyMaterial;
    private Material _skinMaterial;
    private Material _rangedEnemyMaterial;
    private int _score;
    private int _hitChain;
    private float _hitChainRemaining;
    private float _cameraShakeRemaining;

    // 続けて命中させた回数。攻撃の「3段コンボ」とは別で、敵への命中を数えます。
    public int HitChain => _hitChain;

    // このコンポーネントの最初の Update より前に、一度だけ呼ばれる初期化処理です。
    // シーンには管理役だけを配置し、実際のステージは実行時に作っています。
    private void Start()
    {
        // 色別のマテリアルを作り、同じ色のパーツで共有します。
        _floorMaterial = CreateMaterial(new Color(0.10f, 0.15f, 0.23f));
        _playerMaterial = CreateMaterial(new Color(0.1f, 0.65f, 1));
        _enemyMaterial = CreateMaterial(new Color(1, 0.25f, 0.25f));
        _skinMaterial = CreateMaterial(new Color(0.95f, 0.73f, 0.52f));
        _rangedEnemyMaterial = CreateMaterial(new Color(0.8f, 0.35f, 1));
        // 環境光で全体を明るくし、平行光源で立体の陰影を付けます。
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        // 床の上面が Y = 0 になるように、中心を高さの半分だけ下げています。
        CreateBox("Street", new Vector3(0, -0.2f, 0), new Vector3(48, 0.4f, 8), _floorMaterial);
        for (int x = -24; x <= 24; x += 4)
        {
            CreateBox("Road marking", new Vector3(x, 0.015f, 0), new Vector3(1.6f, 0.02f, 0.08f), _skinMaterial);
            CreateBox("Backdrop", new Vector3(x, 2, 5), new Vector3(3.6f, 4, 1), _floorMaterial);
        }
        // 正投影カメラでは遠近によるサイズ変化がなく、横スクロールの距離感をつかみやすくなります。
        _camera = new GameObject("Side Camera").AddComponent<Camera>();
        _camera.tag = "MainCamera";
        _camera.orthographic = true;
        // 映る範囲の縦方向の半分。大きくすると広い範囲が映ります。
        _camera.orthographicSize = 6;
        _camera.backgroundColor = new Color(0.035f, 0.055f, 0.1f);
        _player = CreateFighter("Player", new Vector3(-5, 0, 0), true);
        SpawnWave();
    }

    // Built-in レンダーパイプラインの Standard シェーダーで、指定色の素材を作ります。
    private Material CreateMaterial(Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    // 立方体を指定の位置・大きさ・素材で生成する共通処理です。
    private GameObject CreateBox(string label, Vector3 position, Vector3 scale, Material material)
    {
        var boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.name = label;
        boxObject.transform.position = position;
        boxObject.transform.localScale = scale;
        boxObject.GetComponent<Renderer>().sharedMaterial = material;
        // この試作では物理衝突を使わず、移動範囲と攻撃距離をコードで判定します。
        // CreatePrimitive が自動で付ける Collider は不要なので取り除きます。
        Destroy(boxObject.GetComponent<Collider>());
        return boxObject;
    }

    // プレイヤーと敵は同じ Fighter を使い、isPlayer によって操作方法を切り替えます。
    private Fighter CreateFighter(string label, Vector3 position, bool isPlayer, bool isRanged = false)
    {
        var root = new GameObject(label);
        root.transform.position = position;
        Fighter fighter = isRanged ? root.AddComponent<RangedFighter>() : root.AddComponent<Fighter>();
        // 三項演算子「条件 ? 真の場合 : 偽の場合」で初期 HP を選びます。
        int maxHealth = isPlayer ? 100 : 55 + _wave * 5;
        // 移動を担う親と、見た目を担う Model を分離します。
        // Model だけ反転・回転させれば、キャラクターの基準位置に影響しません。
        var visual = new GameObject("Model").transform;
        visual.SetParent(root.transform, false);
        var bodyMaterial = isPlayer ? _playerMaterial : isRanged ? _rangedEnemyMaterial : _enemyMaterial;
        CreateBodyPart("Body", new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.75f, 0.4f), bodyMaterial, visual);
        CreateBodyPart("Head", new Vector3(0, 1.78f, 0), new Vector3(0.4f, 0.4f, 0.4f), _skinMaterial, visual);
        var attackArm = CreateBodyPart("Punch arm", new Vector3(0.4f, 1.2f, -0.1f), new Vector3(0.22f, 0.65f, 0.22f), _skinMaterial, visual);
        CreateBodyPart("Left arm", new Vector3(-0.4f, 1.2f, -0.1f), new Vector3(0.22f, 0.65f, 0.22f), _skinMaterial, visual);
        var rightLeg = CreateBodyPart("Right leg", new Vector3(0.17f, 0.4f, 0), new Vector3(0.23f, 0.8f, 0.28f), bodyMaterial, visual);
        var leftLeg = CreateBodyPart("Left leg", new Vector3(-0.17f, 0.4f, 0), new Vector3(0.23f, 0.8f, 0.28f), bodyMaterial, visual);
        if (isPlayer)
        {
            // 仮の鈍器は腕の子にして、攻撃時に腕と一緒に振られるようにします。
            Transform mace = CreateBodyPart("Mace", new Vector3(0.4f, 1.65f, -0.1f), new Vector3(0.38f, 0.38f, 0.38f), _skinMaterial, visual);
            mace.SetParent(attackArm, true);
            CreateBodyPart("Shield", new Vector3(0.35f, 1.1f, -0.38f), new Vector3(0.16f, 0.65f, 0.5f), _floorMaterial, visual);
        }
        fighter.Initialize(this, isPlayer, maxHealth, visual, attackArm, rightLeg, leftLeg);
        return fighter;
    }

    // 各身体パーツの位置は、世界全体ではなく親 Model を基準に指定します。
    private Transform CreateBodyPart(string label, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var part = CreateBox(label, Vector3.zero, scale, material).transform;
        // false は、親の変更時にワールド座標を維持しない指定です。
        part.SetParent(parent, false);
        part.localPosition = position;
        return part;
    }

    // 敵の人数はウェーブごとに増えますが、最大 7 体で止めます。
    // X はプレイヤーの右側、Z はランダム。Clamp でステージ内に収めます。
    private void SpawnWave()
    {
        _wave++;
        // 全滅のご褒美として少量回復し、次の戦闘にも挑みやすくします。
        if (_wave > 1)
        {
            _player.RestoreHealth(15);
        }
        for (int i = 0; i < Mathf.Min(2 + _wave, 7); i++)
        {
            // 3 体に 1 体を遠距離型にして、近接型の背後から射線を作る混成戦にします。
            bool isRanged = i % 3 == 2;
            _enemies.Add(CreateFighter(isRanged ? "Ranged Enemy" : "Melee Enemy",
                new Vector3(Mathf.Clamp(_player.transform.position.x + 7 + i, -22, 22), 0, Random.Range(-2.5f, 2.5f)), false, isRanged));
        }
    }

    // Update は毎フレーム呼ばれます。ここでは戦闘全体の進行を管理します。
    private void Update()
    {
        // Unity の Object は Destroy 後に == null と判定できるため、この条件で整理できます。
        _enemies.RemoveAll(enemy => enemy == null);
        // 2 秒間命中がなければ連続ヒットをリセット。スコア自体は残ります。
        _hitChainRemaining -= Time.deltaTime;
        if (_hitChainRemaining <= 0)
        {
            _hitChain = 0;
        }
        _cameraShakeRemaining = Mathf.Max(0, _cameraShakeRemaining - Time.deltaTime);
        if (_player.Health > 0 && _enemies.Count == 0)
        {
            // deltaTime は前フレームからの経過秒数。FPS に依存せず 2 秒を計測できます。
            _nextWaveElapsed += Time.deltaTime;
            if (_nextWaveElapsed > 2)
            {
                _nextWaveElapsed = 0;
                SpawnWave();
            }
        }
        // シーンを読み直すと Start から初期化されます。シーンのビルド設定への登録が必要です。
        if (BrawlerInput.WasRestartPressed())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // 全キャラクターの Update の後にカメラを動かし、そのフレームの最新位置を追います。
    private void LateUpdate()
    {
        // カメラは左右だけ追従。端で Clamp するとステージ外が映りすぎるのを抑えられます。
        var focus = new Vector3(Mathf.Clamp(_player.transform.position.x, -16, 16), 1, 0);
        _camera.transform.position = focus + new Vector3(0, 6, -13);
        _camera.transform.LookAt(focus);
        // 一瞬だけカメラをずらし、攻撃が命中した重さを表現します。
        // 毎フレーム基準位置から計算するため、揺れが積み重なって位置がずれることはありません。
        if (_cameraShakeRemaining > 0)
        {
            _camera.transform.position += Random.insideUnitSphere * _cameraShakeRemaining * 0.7f;
        }
    }

    // 敵 AI にプレイヤーを公開する読み取り専用プロパティです。
    public Fighter Target => _player;

    // Damage が実際に受理された場合だけ呼びます。無敵中の攻撃はスコアに含めません。
    public void RegisterDamage(Fighter victim)
    {
        _cameraShakeRemaining = 0.16f;
        if (victim.IsPlayer)
        {
            _hitChain = 0;
            _hitChainRemaining = 0;
            return;
        }
        _hitChain++;
        _hitChainRemaining = 2;
        _score += 10 * Mathf.Min(_hitChain, 10);
        if (victim.Health == 0)
        {
            _score += 100;
        }
    }

    // 攻撃者の陣営に応じて対象を選びます。プレイヤーの攻撃は範囲内の敵全員に当たります。
    public void Hit(Fighter attacker, int damage)
    {
        if (attacker.IsPlayer)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null && IsInAttackRange(attacker, enemy))
                {
                    enemy.TakeDamage(damage, attacker.Facing);
                }
            }
        }
        else if (IsInAttackRange(attacker, _player))
        {
            _player.TakeDamage(damage, attacker.Facing);
        }
    }

    // コライダーではなく、攻撃者と対象の位置の差から当たり判定を作ります。
    private bool IsInAttackRange(Fighter attacker, Fighter target)
    {
        Vector3 delta = target.transform.position - attacker.transform.position;
        // プレイヤーの左右方向のリーチだけを従来の 1.5 倍にします（1.65 × 1.5 = 2.475）。
        // 敵は 1.65 のままにして、鈍器の間合いを活かせるようにします。
        float attackReach = attacker.IsPlayer ? 2.475f : 1.65f;
        // 生存・奥行き差・左右距離・向きの 4 条件をすべて満たすと命中します。
        // facing は右 +1 / 左 -1。差に掛けると、左右どちらでも前方が正になります。
        // -0.25 の余裕を持たせ、ほぼ重なった相手にも攻撃が当たるようにしています。
        return target.Health > 0
            && Mathf.Abs(delta.z) < 0.8f
            && Mathf.Abs(delta.x) < attackReach
            && delta.x * attacker.Facing > -0.25f;
    }

    // Unity の簡易 GUI。画面左上を原点とするピクセル座標で文字や HP バーを描きます。
    // 1 フレームに複数回呼ばれることがあるため、ゲーム進行はここで更新しません。
    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        GUI.Label(new Rect(24, 20, 700, 40), "NEON STREET  |  WAVE " + _wave + "  |  ENEMIES " + _enemies.Count, style);
        GUI.color = new Color(0.1f, 0.7f, 1);
        // 現在 HP / 最大 HP の割合を、満タン時の幅 300 ピクセルに掛けます。
        GUI.Box(new Rect(24, 65, 300f * Mathf.Max(0, _player.Health) / _player.MaxHealth, 24), "HP " + _player.Health);
        GUI.color = Color.white;
        GUI.Label(new Rect(24, 100, 700, 40), "SCORE " + _score + "   |   " + _hitChain + " HITS", style);
        string dashHint = BrawlerInput.IsGamepadConnected ? "A / Cross / RB: Dash" : "Shift: Dash";
        GUI.Label(new Rect(24, 138, 700, 40), _player.DashReady ? $"DASH READY — {dashHint}" : "DASH RECHARGING", style);
        if (_player.CanCounter)
        {
            GUI.Label(new Rect(24, 175, 700, 40), "COUNTER READY — Press Attack", style);
        }
        string controls = BrawlerInput.IsGamepadConnected
            ? "Stick: Move  X / Square: Attack  A / RB: Dash  LB / LT: Guard  Start: Restart"
            : "WASD / Arrows: Move   J / Space: Attack   Shift: Dash   K: Guard   R: Restart";
        GUI.Label(new Rect(24, Screen.height - 48, 1100, 40), controls, style);
        // 敵の頭上に HP と攻撃予告を表示。ワールド座標を画面座標に変換します。
        // GUI の Y 軸は上から下、WorldToScreenPoint は下から上なので反転が必要です。
        foreach (var enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            Vector3 screen = _camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.3f);
            if (screen.z <= 0)
            {
                continue;
            }
            float y = Screen.height - screen.y;
            GUI.color = enemy.IsWindingUp ? Color.yellow : new Color(1, 0.35f, 0.35f);
            GUI.Box(new Rect(screen.x - 35, y, 70f * enemy.Health / enemy.MaxHealth, 10), "");
            if (enemy.IsWindingUp)
            {
                GUI.Label(new Rect(screen.x - 12, y - 35, 40, 35), "!", style);
            }
        }
        GUI.color = Color.white;
        if (!BrawlerInput.HasInputDevice)
        {
            GUI.Label(new Rect(24, 180, 1100, 40), "No input devices — Apply Input Settings and Restart from Tools / Neon Street", style);
        }
        if (_player.Health <= 0)
        {
            string restartHint = BrawlerInput.IsGamepadConnected ? "Start" : "R";
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2, 450, 50), $"DEFEATED — Press {restartHint}", style);
        }
        else if (_enemies.Count == 0)
        {
            GUI.Label(new Rect(Screen.width / 2 - 120, 200, 400, 50), "WAVE CLEAR!  HP +15", style);
        }
    }
}
