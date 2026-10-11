using System.Collections.Generic;
using UnityEngine;

// 試作ステージとキャラクターの見た目を生成します。戦闘や進行の判断は管理役に残します。
// 生成した共有素材はこのクラスが所有し、シーン終了時にまとめて解放します。
public sealed class BrawlerSceneFactory
{
    private readonly BeltBrawler _game;
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private Camera _camera;
    private Material _floorMaterial;
    private Material _playerMaterial;
    private Material _enemyMaterial;
    private Material _skinMaterial;
    private Material _rangedEnemyMaterial;
    private Material _heavyEnemyMaterial;
    private Material _undeadEnemyMaterial;
    private Material _potionMaterial;
    private Material _knifeMaterial;
    private Material _impactMaterial;
    private Material _guardImpactMaterial;
    private Material _counterImpactMaterial;
    private Material _warningMaterial;

    public Material FloorMaterial => _floorMaterial;
    public Material SkinMaterial => _skinMaterial;
    public Material PotionMaterial => _potionMaterial;
    public Material KnifeMaterial => _knifeMaterial;
    public Material ImpactMaterial => _impactMaterial;
    public Material GuardImpactMaterial => _guardImpactMaterial;
    public Material CounterImpactMaterial => _counterImpactMaterial;

    public BrawlerSceneFactory(BeltBrawler game)
    {
        _game = game;
    }

    public Camera BuildStage()
    {
        // 色別のマテリアルを作り、同じ色のパーツで共有します。
        _floorMaterial = CreateMaterial(new Color(0.10f, 0.15f, 0.23f));
        _playerMaterial = CreateMaterial(new Color(0.1f, 0.65f, 1));
        _enemyMaterial = CreateMaterial(new Color(1, 0.25f, 0.25f));
        _skinMaterial = CreateMaterial(new Color(0.95f, 0.73f, 0.52f));
        _rangedEnemyMaterial = CreateMaterial(new Color(0.8f, 0.35f, 1));
        _heavyEnemyMaterial = CreateMaterial(new Color(1, 0.55f, 0.15f));
        _undeadEnemyMaterial = CreateMaterial(new Color(0.8f, 0.9f, 0.7f));
        _potionMaterial = CreateMaterial(new Color(0.2f, 1, 0.4f));
        _knifeMaterial = CreateMaterial(new Color(0.8f, 0.85f, 0.95f));
        // 発光風の色を、照明に左右されない素材で描きます。
        _impactMaterial = CreateOwnedMaterial("Unlit/Color");
        _impactMaterial.color = new Color(1, 0.85f, 0.25f);
        // Unlit素材を共有し、暗い場所でも盾の火花と青白い反撃の十字が見えるようにします。
        _guardImpactMaterial = CreateOwnedMaterial("Unlit/Color");
        _guardImpactMaterial.color = new Color(1, 1, 0.75f);
        _counterImpactMaterial = CreateOwnedMaterial("Unlit/Color");
        _counterImpactMaterial.color = new Color(0.45f, 0.9f, 1);
        _warningMaterial = CreateOwnedMaterial("Unlit/Color");
        _warningMaterial.color = Color.white;
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
        // 実行時に作るカメラに耳を置きます。既存リスナーがある場合は重複させません。
        if (Object.FindAnyObjectByType<AudioListener>() == null)
        {
            _camera.gameObject.AddComponent<AudioListener>();
        }
        _camera.orthographic = true;
        // 映る範囲の縦方向の半分。大きくすると広い範囲が映ります。
        _camera.orthographicSize = 6;
        _camera.backgroundColor = new Color(0.035f, 0.055f, 0.1f);
        return _camera;
    }

    // Unityの素材は通常のC#オブジェクトとは異なるため、Destroyで明示的に解放します。
    public void Dispose()
    {
        foreach (Material material in _ownedMaterials)
        {
            Object.Destroy(material);
        }
        _ownedMaterials.Clear();
    }

    private Material CreateOwnedMaterial(string shaderName)
    {
        var material = new Material(Shader.Find(shaderName));
        _ownedMaterials.Add(material);
        return material;
    }

    private Material CreateMaterial(Color color)
    {
        var material = CreateOwnedMaterial("Standard");
        material.color = color;
        return material;
    }

    public GameObject CreateBox(string label, Vector3 position, Vector3 scale, Material material)
    {
        var boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.name = label;
        boxObject.transform.position = position;
        boxObject.transform.localScale = scale;
        boxObject.GetComponent<Renderer>().sharedMaterial = material;
        // この試作では物理衝突を使わず、移動範囲と攻撃距離をコードで判定します。
        // CreatePrimitive が自動で付ける Collider は不要なので取り除きます。
        Object.Destroy(boxObject.GetComponent<Collider>());
        return boxObject;
    }

    public Fighter CreateFighter(string label, Vector3 position, bool isPlayer, int wave, bool isRanged = false, bool isHeavy = false, bool isUndead = false)
    {
        var root = new GameObject(label);
        root.transform.position = position;
        Fighter fighter = isPlayer ? root.AddComponent<ClericFighter>()
            : isHeavy ? root.AddComponent<HeavyFighter>()
            : isRanged ? root.AddComponent<RangedFighter>()
            : isUndead ? root.AddComponent<UndeadFighter>() : root.AddComponent<Fighter>();
        // 三項演算子「条件 ? 真の場合 : 偽の場合」で初期 HP を選びます。
        int maxHealth = isPlayer ? BrawlerBalance.PlayerMaxHealth
            : isHeavy ? BrawlerBalance.HeavyBaseHealth + wave * BrawlerBalance.HeavyHealthPerWave
            : BrawlerBalance.EnemyBaseHealth + wave * BrawlerBalance.EnemyHealthPerWave;
        // 移動を担う親と、見た目を担う Model を分離します。
        // Model だけ反転・回転させれば、キャラクターの基準位置に影響しません。
        var visual = new GameObject("Model").transform;
        visual.SetParent(root.transform, false);
        var bodyMaterial = isPlayer ? _playerMaterial : isHeavy ? _heavyEnemyMaterial
            : isRanged ? _rangedEnemyMaterial : isUndead ? _undeadEnemyMaterial : _enemyMaterial;
        CreateBodyPart("Body", new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.75f, 0.4f), bodyMaterial, visual);
        CreateBodyPart("Head", new Vector3(0, 1.78f, 0), new Vector3(0.4f, 0.4f, 0.4f), _skinMaterial, visual);
        var attackArm = CreateBodyPart("Punch arm", new Vector3(0.4f, 1.2f, -0.1f), new Vector3(0.22f, 0.65f, 0.22f), _skinMaterial, visual);
        CreateBodyPart("Left arm", new Vector3(-0.4f, 1.2f, -0.1f), new Vector3(0.22f, 0.65f, 0.22f), _skinMaterial, visual);
        var rightLeg = CreateBodyPart("Right leg", new Vector3(0.17f, 0.4f, 0), new Vector3(0.23f, 0.8f, 0.28f), bodyMaterial, visual);
        var leftLeg = CreateBodyPart("Left leg", new Vector3(-0.17f, 0.4f, 0), new Vector3(0.23f, 0.8f, 0.28f), bodyMaterial, visual);
        if (isUndead)
        {
            // 胸の骨と頭の色で区別し、通常敵と同じ判定サイズを維持します。
            CreateBodyPart("Undead ribs", new Vector3(0, 1.15f, -0.25f), new Vector3(0.45f, 0.12f, 0.1f), bodyMaterial, visual);
            visual.Find("Head").GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        }
        if (isHeavy)
        {
            // 大きな胴鎧と肩当てで区別します。判定用の親や通常のリーチは拡大しません。
            CreateBodyPart("Heavy armor", new Vector3(0, 1.15f, 0), new Vector3(0.8f, 0.9f, 0.55f), bodyMaterial, visual);
            CreateBodyPart("Right pauldron", new Vector3(0.45f, 1.5f, 0), new Vector3(0.35f, 0.25f, 0.5f), bodyMaterial, visual);
            CreateBodyPart("Left pauldron", new Vector3(-0.45f, 1.5f, 0), new Vector3(0.35f, 0.25f, 0.5f), bodyMaterial, visual);
        }
        if (isPlayer)
        {
            // 仮の鈍器は腕の子にして、攻撃時に腕と一緒に振られるようにします。
            Transform mace = CreateBodyPart("Mace", new Vector3(0.4f, 1.65f, -0.1f), new Vector3(0.38f, 0.38f, 0.38f), _skinMaterial, visual);
            mace.SetParent(attackArm, true);
            CreateBodyPart("Shield", new Vector3(0.35f, 1.1f, -0.38f), new Vector3(0.16f, 0.65f, 0.5f), _floorMaterial, visual);
        }
        fighter.Initialize(_game, isPlayer, maxHealth, visual, attackArm, rightLeg, leftLeg);
        if (!isPlayer)
        {
            CreateAttackWarning(fighter);
        }
        return fighter;
    }

    private void CreateAttackWarning(Fighter fighter)
    {
        // 毎回の予告で生成せず、敵一体につき一つだけ作ってRendererを切り替えます。
        var marker = new GameObject("Attack Warning").transform;
        marker.SetParent(fighter.transform, false);
        marker.localPosition = Vector3.up * BrawlerBalance.WarningMarkerHeight;
        float width = BrawlerBalance.WarningMarkerWidth;
        float depth = BrawlerBalance.WarningMarkerDepth;
        float thickness = BrawlerBalance.WarningMarkerThickness;
        CreateBodyPart("Front edge", Vector3.forward * (depth / 2), new Vector3(width, thickness, thickness), _warningMaterial, marker);
        CreateBodyPart("Back edge", Vector3.back * (depth / 2), new Vector3(width, thickness, thickness), _warningMaterial, marker);
        CreateBodyPart("Right edge", Vector3.right * (width / 2), new Vector3(thickness, thickness, depth), _warningMaterial, marker);
        CreateBodyPart("Left edge", Vector3.left * (width / 2), new Vector3(thickness, thickness, depth), _warningMaterial, marker);
        Transform direction = CreateBodyPart("Attack direction", Vector3.zero,
            new Vector3(BrawlerBalance.WarningDirectionLength, thickness, thickness), _warningMaterial, marker);
        marker.gameObject.AddComponent<EnemyAttackWarning>().Initialize(fighter, direction);
    }

    public Transform CreateBodyPart(string label, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var part = CreateBox(label, Vector3.zero, scale, material).transform;
        // false は、親の変更時にワールド座標を維持しない指定です。
        part.SetParent(parent, false);
        part.localPosition = position;
        return part;
    }

}
