using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ゲーム全体の管理役。ステージ生成、敵の出現、攻撃判定、カメラ、画面表示を担当します。
// MonoBehaviour を継承すると、Unity が Start / Update などを自動的に呼び出します。
// 座標の約束：X = 左右、Y = 高さ、Z = 道路の奥行き。移動は XZ 平面で行います。
public sealed class BeltBrawler : MonoBehaviour
{
    // 生存中と撃破直後の敵を保持します。破棄された敵は Update で取り除きます。
    readonly List<Fighter> enemies = new List<Fighter>();
    Fighter player;
    Camera view;
    int wave; // 現在の敵グループの番号。出現のたびに増えます。
    float nextWave; // 全滅後、次の出現までに経過した秒数。
    Material floor, blue, red, skin;
    int score, hitChain;
    float chainTime, cameraShake;

    // 続けて命中させた回数。攻撃の「3段コンボ」とは別で、敵への命中を数えます。
    public int HitChain => hitChain;

    // このコンポーネントの最初の Update より前に、一度だけ呼ばれる初期化処理です。
    // シーンには管理役だけを配置し、実際のステージは実行時に作っています。
    void Start()
    {
        // 色別のマテリアルを作り、同じ色のパーツで共有します。
        floor = Paint(new Color(.10f, .15f, .23f));
        blue = Paint(new Color(.1f, .65f, 1));
        red = Paint(new Color(1, .25f, .25f));
        skin = Paint(new Color(.95f, .73f, .52f));
        // 環境光で全体を明るくし、平行光源で立体の陰影を付けます。
        RenderSettings.ambientLight = new Color(.55f, .6f, .7f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        // 床の上面が Y = 0 になるように、中心を高さの半分だけ下げています。
        Box("Street", new Vector3(0, -.2f, 0), new Vector3(48, .4f, 8), floor);
        for (int x = -24; x <= 24; x += 4)
        {
            Box("Road marking", new Vector3(x, .015f, 0), new Vector3(1.6f, .02f, .08f), skin);
            Box("Backdrop", new Vector3(x, 2, 5), new Vector3(3.6f, 4, 1), floor);
        }
        // 正投影カメラでは遠近によるサイズ変化がなく、横スクロールの距離感をつかみやすくなります。
        view = new GameObject("Side Camera").AddComponent<Camera>();
        view.tag = "MainCamera";
        view.orthographic = true;
        view.orthographicSize = 6; // 映る範囲の縦方向の半分。大きくすると広い範囲が映ります。
        view.backgroundColor = new Color(.035f, .055f, .1f);
        player = CreateFighter("Player", new Vector3(-5, 0, 0), true);
        SpawnWave();
    }

    // Built-in レンダーパイプラインの Standard シェーダーで、指定色の素材を作ります。
    Material Paint(Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    // 立方体を指定の位置・大きさ・素材で生成する共通処理です。
    GameObject Box(string label, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = label;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        // この試作では物理衝突を使わず、移動範囲と攻撃距離をコードで判定します。
        // CreatePrimitive が自動で付ける Collider は不要なので取り除きます。
        Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    // プレイヤーと敵は同じ Fighter を使い、isPlayer によって操作方法を切り替えます。
    Fighter CreateFighter(string label, Vector3 position, bool isPlayer)
    {
        var root = new GameObject(label);
        root.transform.position = position;
        var fighter = root.AddComponent<Fighter>();
        fighter.game = this; // Fighter から攻撃判定やプレイヤー参照を利用できるようにします。
        fighter.isPlayer = isPlayer;
        // 三項演算子「条件 ? 真の場合 : 偽の場合」で初期 HP を選びます。
        fighter.health = isPlayer ? 100 : 55 + wave * 5;
        fighter.maxHealth = fighter.health;
        // 移動を担う親と、見た目を担う Model を分離します。
        // Model だけ反転・回転させれば、キャラクターの基準位置に影響しません。
        fighter.visual = new GameObject("Model").transform;
        fighter.visual.SetParent(root.transform, false);
        var bodyMaterial = isPlayer ? blue : red;
        Part("Body", new Vector3(0, 1.15f, 0), new Vector3(.55f, .75f, .4f), bodyMaterial, fighter.visual);
        Part("Head", new Vector3(0, 1.78f, 0), new Vector3(.4f, .4f, .4f), skin, fighter.visual);
        fighter.arm = Part("Punch arm", new Vector3(.4f, 1.2f, -.1f), new Vector3(.22f, .65f, .22f), skin, fighter.visual);
        Part("Left arm", new Vector3(-.4f, 1.2f, -.1f), new Vector3(.22f, .65f, .22f), skin, fighter.visual);
        fighter.legA = Part("Right leg", new Vector3(.17f, .4f, 0), new Vector3(.23f, .8f, .28f), bodyMaterial, fighter.visual);
        fighter.legB = Part("Left leg", new Vector3(-.17f, .4f, 0), new Vector3(.23f, .8f, .28f), bodyMaterial, fighter.visual);
        return fighter;
    }

    // 各身体パーツの位置は、世界全体ではなく親 Model を基準に指定します。
    Transform Part(string label, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var part = Box(label, Vector3.zero, scale, material).transform;
        part.SetParent(parent, false); // false は、親の変更時にワールド座標を維持しない指定です。
        part.localPosition = position;
        return part;
    }

    // 敵の人数はウェーブごとに増えますが、最大 7 体で止めます。
    // X はプレイヤーの右側、Z はランダム。Clamp でステージ内に収めます。
    void SpawnWave()
    {
        wave++;
        // 全滅のご褒美として少量回復し、次の戦闘にも挑みやすくします。
        if (wave > 1) player.health = Mathf.Min(player.maxHealth, player.health + 15);
        for (int i = 0; i < Mathf.Min(2 + wave, 7); i++)
            enemies.Add(CreateFighter("Enemy", new Vector3(Mathf.Clamp(player.transform.position.x + 7 + i, -22, 22), 0, Random.Range(-2.5f, 2.5f)), false));
    }

    // Update は毎フレーム呼ばれます。ここでは戦闘全体の進行を管理します。
    void Update()
    {
        // Unity の Object は Destroy 後に == null と判定できるため、この条件で整理できます。
        enemies.RemoveAll(enemy => enemy == null);
        // 2 秒間命中がなければ連続ヒットをリセット。スコア自体は残ります。
        chainTime -= Time.deltaTime;
        if (chainTime <= 0) hitChain = 0;
        cameraShake = Mathf.Max(0, cameraShake - Time.deltaTime);
        if (player.health > 0 && enemies.Count == 0)
        {
            // deltaTime は前フレームからの経過秒数。FPS に依存せず 2 秒を計測できます。
            nextWave += Time.deltaTime;
            if (nextWave > 2) { nextWave = 0; SpawnWave(); }
        }
        // シーンを読み直すと Start から初期化されます。シーンのビルド設定への登録が必要です。
        if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // 全キャラクターの Update の後にカメラを動かし、そのフレームの最新位置を追います。
    void LateUpdate()
    {
        // カメラは左右だけ追従。端で Clamp するとステージ外が映りすぎるのを抑えられます。
        var focus = new Vector3(Mathf.Clamp(player.transform.position.x, -16, 16), 1, 0);
        view.transform.position = focus + new Vector3(0, 6, -13);
        view.transform.LookAt(focus);
        // 一瞬だけカメラをずらし、攻撃が命中した重さを表現します。
        // 毎フレーム基準位置から計算するため、揺れが積み重なって位置がずれることはありません。
        if (cameraShake > 0) view.transform.position += Random.insideUnitSphere * cameraShake * .7f;
    }

    public Fighter Target => player; // 敵 AI にプレイヤーを公開する読み取り専用プロパティです。

    // Damage が実際に受理された場合だけ呼びます。無敵中の攻撃はスコアに含めません。
    public void RegisterDamage(Fighter victim)
    {
        cameraShake = .16f;
        if (victim.isPlayer)
        {
            hitChain = 0;
            chainTime = 0;
            return;
        }
        hitChain++;
        chainTime = 2;
        score += 10 * Mathf.Min(hitChain, 10);
        if (victim.health == 0) score += 100;
    }

    // 攻撃者の陣営に応じて対象を選びます。プレイヤーの攻撃は範囲内の敵全員に当たります。
    public void Hit(Fighter attacker, int damage)
    {
        if (attacker.isPlayer)
        {
            foreach (var enemy in enemies) if (enemy != null && InReach(attacker, enemy)) enemy.Damage(damage, attacker.facing);
        }
        else if (InReach(attacker, player)) player.Damage(damage, attacker.facing);
    }

    // a が攻撃者、b が攻撃対象。コライダーではなく、位置の差から当たり判定を作ります。
    bool InReach(Fighter a, Fighter b)
    {
        Vector3 delta = b.transform.position - a.transform.position;
        // 生存・奥行き差・左右距離・向きの 4 条件をすべて満たすと命中します。
        // facing は右 +1 / 左 -1。差に掛けると、左右どちらでも前方が正になります。
        // -0.25 の余裕を持たせ、ほぼ重なった相手にも攻撃が当たるようにしています。
        return b.health > 0 && Mathf.Abs(delta.z) < .8f && Mathf.Abs(delta.x) < 1.65f && delta.x * a.facing > -.25f;
    }

    // Unity の簡易 GUI。画面左上を原点とするピクセル座標で文字や HP バーを描きます。
    // 1 フレームに複数回呼ばれることがあるため、ゲーム進行はここで更新しません。
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        GUI.Label(new Rect(24, 20, 700, 40), "NEON STREET  |  WAVE " + wave + "  |  ENEMIES " + enemies.Count, style);
        GUI.color = new Color(.1f, .7f, 1);
        // 現在 HP / 最大 HP の割合を、満タン時の幅 300 ピクセルに掛けます。
        GUI.Box(new Rect(24, 65, 300f * Mathf.Max(0, player.health) / player.maxHealth, 24), "HP " + player.health);
        GUI.color = Color.white;
        GUI.Label(new Rect(24, 100, 700, 40), "SCORE " + score + "   |   " + hitChain + " HITS", style);
        GUI.Label(new Rect(24, 138, 700, 40), player.DashReady ? "DASH READY — Shift" : "DASH RECHARGING", style);
        GUI.Label(new Rect(24, Screen.height - 48, 1100, 40), "WASD / Arrows: Move    J / Space: Attack    Shift: Dodge dash    R: Restart", style);
        // 敵の頭上に HP と攻撃予告を表示。ワールド座標を画面座標に変換します。
        // GUI の Y 軸は上から下、WorldToScreenPoint は下から上なので反転が必要です。
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.health <= 0) continue;
            Vector3 screen = view.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.3f);
            if (screen.z <= 0) continue;
            float y = Screen.height - screen.y;
            GUI.color = enemy.IsWindingUp ? Color.yellow : new Color(1, .35f, .35f);
            GUI.Box(new Rect(screen.x - 35, y, 70f * enemy.health / enemy.maxHealth, 10), "");
            if (enemy.IsWindingUp) GUI.Label(new Rect(screen.x - 12, y - 35, 40, 35), "!", style);
        }
        GUI.color = Color.white;
        if (player.health <= 0) GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2, 400, 50), "DEFEATED — Press R", style);
        else if (enemies.Count == 0) GUI.Label(new Rect(Screen.width / 2 - 120, 200, 400, 50), "WAVE CLEAR!  HP +15", style);
    }
}

// キャラクター単体の管理役。入力または AI、移動、攻撃演出、被ダメージを担当します。
public sealed class Fighter : MonoBehaviour
{
    public BeltBrawler game;
    public bool isPlayer;
    public int health, maxHealth;
    public float facing = 1; // 右向きは +1、左向きは -1。見た目と攻撃判定で共用します。
    public Transform visual, arm, legA, legB;
    // attackTime = 攻撃演出の残り秒数、cooldown = 次の攻撃までの待ち時間。
    // stun = 被ダメージによる硬直の残り秒数、lastAttack = 前回攻撃を開始した時刻。
    // walk = 足を振るための位相。lastAttack の初期値は最初の攻撃を 1 段目にするための値です。
    float attackTime, cooldown, stun, lastAttack = -10, walk;
    int combo; // 現在のコンボ段数（1～3）。
    bool dealtHit; // 1 回の攻撃でダメージ判定を繰り返さないためのフラグ。
    Vector3 knockback; // 被ダメージ時に押し戻される速度。
    // ダッシュ中だけ入力方向に高速移動し、ダメージを無効にします。
    // 再使用の待ち時間を設け、回避を連打するだけにならないようにしています。
    float dashTime, dashCooldown, windup, flashTime;
    Vector3 dashDirection;
    float attackBuffer;
    Renderer[] bodyRenderers;
    MaterialPropertyBlock flashProperties;
    public bool DashReady => dashCooldown <= 0;
    public bool IsWindingUp => windup > 0;

    void Start()
    {
        // Unity のネイティブ機能を使うオブジェクトはフィールド初期化では作らず、Start で作ります。
        flashProperties = new MaterialPropertyBlock();
        // パーツ生成後に Renderer を集めます。素材を複製せず、個体ごとに色を上書きします。
        bodyRenderers = visual.GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (health <= 0) return; // 倒れた後は移動や攻撃の更新を止めます。
        float dt = Time.deltaTime;
        cooldown -= dt;
        stun -= dt;
        dashCooldown -= dt;
        flashTime -= dt;
        attackBuffer = Mathf.Max(0, attackBuffer - dt);
        Vector3 input = Vector3.zero;
        if (isPlayer)
        {
            input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
            // 次の攻撃が可能になる直前の入力も 0.2 秒間覚え、コンボをつなぎやすくします。
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Space)) attackBuffer = .2f;
            // 攻撃の後隙からも回避できますが、被ダメージ硬直中は回避できません。
            if (Input.GetKeyDown(KeyCode.LeftShift) && DashReady && stun <= 0)
            {
                dashDirection = input.sqrMagnitude > 0 ? input : new Vector3(facing, 0, 0);
                dashTime = .2f;
                dashCooldown = .85f;
                attackTime = 0;
                attackBuffer = 0;
                knockback = Vector3.zero;
            }
        }
        // 敵は 0.55 秒の予告中に足を止め、攻撃方向を固定します。
        // プレイヤーが奥行き方向へ逃げれば、その場所に空振りさせられます。
        bool wasWindingUp = windup > 0;
        if (wasWindingUp)
        {
            windup -= dt;
            if (windup <= 0 && game.Target.health > 0) Attack();
        }
        // 速度に経過秒数を掛けて移動量へ変換し、ノックバック速度を徐々にゼロへ近づけます。
        transform.position += knockback * dt;
        knockback = Vector3.Lerp(knockback, Vector3.zero, dt * 12);
        Vector3 movement = Vector3.zero;
        // 攻撃中・被ダメージ硬直中は、新しい操作や AI の行動を受け付けません。
        if (stun <= 0 && attackTime <= 0 && dashTime <= 0 && !wasWindingUp)
        {
            if (isPlayer)
            {
                // 入力の縦方向は Y ではなく Z に対応させます。
                // normalized で長さを 1 にそろえ、斜め移動だけ速くなることを防ぎます。
                movement = input;
                // 記憶した入力を一度消費して攻撃します。押しっぱなしで自動連打にはなりません。
                if (cooldown <= 0 && attackBuffer > 0) { Attack(); attackBuffer = 0; movement = Vector3.zero; }
            }
            else if (game.Target.health > 0)
            {
                // 敵はプレイヤーへの差分ベクトルを求め、十分に近づいたら攻撃します。
                var delta = game.Target.transform.position - transform.position;
                facing = delta.x >= 0 ? 1 : -1;
                if (Mathf.Abs(delta.x) > 1.1f || Mathf.Abs(delta.z) > .5f) movement = delta.normalized;
                else if (cooldown <= 0) { windup = .55f; movement = Vector3.zero; }
            }
        }
        // 奥行きだけの移動では向きを維持し、左右に動いた場合だけ向きを変更します。
        if (Mathf.Abs(movement.x) > .01f) facing = Mathf.Sign(movement.x);
        transform.position += movement * (isPlayer ? 5 : 2.2f) * dt;
        // 最終フレームの移動量を残り時間で制限し、FPS が低いときの飛びすぎを防ぎます。
        if (dashTime > 0)
        {
            transform.position += dashDirection * 13 * Mathf.Min(dt, dashTime);
            dashTime = Mathf.Max(0, dashTime - dt);
        }
        Vector3 p = transform.position;
        // ステージの端で位置を制限し、高さは常に床の Y = 0 に固定します。
        transform.position = new Vector3(Mathf.Clamp(p.x, -22, 22), 0, Mathf.Clamp(p.z, -3, 3));
        // Animator の代わりに Sin で左右の足を逆方向に振る、仮の歩行演出です。
        walk += movement.magnitude * dt * 12;
        legA.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(walk) * movement.magnitude * 20);
        legB.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(walk) * movement.magnitude * 20);
        visual.localScale = new Vector3(facing, 1, 1); // X を負にすると Model が左右反転します。
        if (attackTime > 0)
        {
            attackTime -= dt;
            // 攻撃中は腕を横に伸ばします。角度から Quaternion を作って回転を指定します。
            arm.localRotation = Quaternion.Euler(0, 0, 90);
            arm.localPosition = new Vector3(.65f, 1.35f, -.1f);
            // 攻撃開始から約 0.14 秒後に一度だけ命中判定。3 段目はダメージを増やします。
            if (!dealtHit && attackTime < .16f) { dealtHit = true; game.Hit(this, isPlayer ? (combo == 3 ? 30 : 18) : 10); }
        }
        else
        {
            arm.localRotation = Quaternion.identity; // 回転なしの状態に戻します。
            arm.localPosition = new Vector3(.4f, 1.2f, -.1f);
        }
        // 被ダメージ中は見た目だけ少し浮かせます。攻撃距離を測る親の座標は変わりません。
        visual.localPosition = new Vector3(0, stun > 0 ? .08f : 0, 0);
        // 被ダメージは白、回避中は水色、敵の攻撃予告は黄色で見分けられるようにします。
        // SetPropertyBlock(null) で上書きを解除すると、元の各パーツの色に戻ります。
        foreach (var body in bodyRenderers)
        {
            if (flashTime > 0 || dashTime > 0 || windup > 0)
            {
                flashProperties.SetColor("_Color", flashTime > 0 ? Color.white : dashTime > 0 ? Color.cyan : Color.yellow);
                body.SetPropertyBlock(flashProperties);
            }
            else body.SetPropertyBlock(null);
        }
    }

    // 攻撃を開始し、演出時間・次の攻撃までの時間・コンボ段数を設定します。
    void Attack()
    {
        // 前回の開始から 0.85 秒未満なら次の段へ進み、それ以上なら 1 段目に戻します。
        // 剰余演算 % によって、3 段目の次は 1 段目になります。入力は Update で先行受付します。
        combo = Time.time - lastAttack < .85f ? combo % 3 + 1 : 1;
        lastAttack = Time.time;
        attackTime = .3f;
        // プレイヤーは 3 段目の後に長めの隙を作り、敵は攻撃間隔を長くして避けやすくします。
        cooldown = isPlayer ? (combo == 3 ? .55f : .32f) : 1.2f;
        dealtHit = false;
    }

    // 攻撃が当たった相手側で呼び出されます。direction は攻撃者の向きです。
    public void Damage(int amount, float direction)
    {
        // 硬直中は追加ダメージを受けないため、短い無敵時間も兼ねています。
        if (health <= 0 || stun > 0 || dashTime > 0) return;
        health = Mathf.Max(0, health - amount);
        stun = .25f;
        attackTime = 0; // 被ダメージによって進行中の攻撃を中断します。
        windup = 0; // 予告中に殴れば敵の攻撃を止められます。
        cooldown = Mathf.Max(cooldown, isPlayer ? .25f : .6f);
        flashTime = .12f;
        // コンボ最終段では大きく吹き飛ばし、敵との間合いを作ります。
        knockback = new Vector3(direction * (amount >= 30 ? 9 : 5), 0, 0);
        game.RegisterDamage(this);
        if (health == 0)
        {
            // 仮の倒れる演出。敵だけ 0.5 秒後に消し、プレイヤーは敗北表示のため残します。
            visual.localRotation = Quaternion.Euler(0, 0, 80);
            if (!isPlayer) Destroy(gameObject, .5f);
        }
    }
}
