using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ゲーム進行と戦闘対象を管理します。生成とHUD描画は専用クラスへ委譲します。
// MonoBehaviour を継承すると、Unity が Start / Update などを自動的に呼び出します。
// 座標の約束：X = 左右、Y = 高さ、Z = 道路の奥行き。移動は XZ 平面で行います。
public sealed class BeltBrawler : MonoBehaviour
{
    // 生存中と撃破直後の敵を保持します。破棄された敵は Update で取り除きます。
    private readonly List<Fighter> _enemies = new List<Fighter>();
    private Fighter _player;
    private BrawlerSceneFactory _factory;
    private readonly BrawlerHud _hud = new BrawlerHud();
    private readonly HitDirectionIndicator _hitDirectionIndicator = new HitDirectionIndicator();

    public void ShowHitDirection(float attackDirection)
    {
        _hitDirectionIndicator.Show(attackDirection);
    }
    private readonly HealthBarFeedback _healthBarFeedback = new HealthBarFeedback();
    private Camera _camera;
    // 現在の敵グループの番号。出現のたびに増えます。
    private int _wave;
    public bool IsStageCleared { get; private set; }
    // 終了理由を分けつつ、操作や得点の停止条件は共通にします。
    public bool IsDefeated { get; private set; }
    public bool IsRunFinished => IsStageCleared || IsDefeated;
    public int ResultWave { get; private set; }
    public int ResultScore { get; private set; }
    public int ResultDefeatedEnemies { get; private set; }
    // 全滅後、次の出現までに経過した秒数。
    private float _nextWaveElapsed;
    private readonly WaveBanner _waveBanner = new WaveBanner();
    private bool _waveClearAnnounced;
    private int _defeatedEnemyCount;
    private int _score;
    private int _hitChain;
    private float _hitChainRemaining;
    private float _cameraShakeRemaining;
    // 予告開始時の位置を固定し、プレイヤーが動いても出現位置を追従させません。
    private readonly List<Vector3> _pendingSpawnPositions = new List<Vector3>();
    private float _spawnWarningRemaining;
    private float _hitStopRemaining;
    private float _timeScaleBeforeHitStop = 1;
    private BrawlerAudio _audio;
    private Font _pickupNoticeFont;

    public void PlaySound(BrawlerSound sound)
    {
        _audio.Play(sound);
    }

    // 続けて命中させた回数。攻撃の「3段コンボ」とは別で、敵への命中を数えます。
    public int HitChain => _hitChain;

    // このコンポーネントの最初の Update より前に、一度だけ呼ばれる初期化処理です。
    // シーンには管理役だけを配置し、実際のステージは実行時に作っています。
    private void Start()
    {
        _audio = gameObject.AddComponent<BrawlerAudio>();
        // 日本語の取得名を描画できるOSフォントを一度だけ作り、通知間で共有します。
        _pickupNoticeFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, BrawlerBalance.PickupNoticeFontSize);
        _factory = new BrawlerSceneFactory(this);
        _camera = _factory.BuildStage();
        _player = CreateFighter("Player", new Vector3(-5, 0, 0), true);
        _healthBarFeedback.Refresh(_player.Health, _player.MaxHealth, 0);
        SpawnWave();
    }

    // プレイヤーと敵は同じ Fighter を使い、isPlayer によって操作方法を切り替えます。
    private Fighter CreateFighter(string label, Vector3 position, bool isPlayer, bool isRanged = false, bool isHeavy = false, bool isUndead = false)
    {
        return _factory.CreateFighter(label, position, isPlayer, _wave, isRanged, isHeavy, isUndead);
    }

    // 敵の人数はウェーブごとに増えますが、最大 7 体で止めます。
    // 第1ウェーブは右側、第2ウェーブ以降は左右交互に出現位置を予約します。
    private void SpawnWave()
    {
        if (IsRunFinished || _wave >= BrawlerBalance.StageWaveCount)
        {
            return;
        }
        _wave++;
        _waveClearAnnounced = false;
        _waveBanner.ShowStart(_wave);
        // 全滅のご褒美として少量回復し、次の戦闘にも挑みやすくします。
        if (_wave > 1)
        {
            _player.RestoreHealth(BrawlerBalance.WaveHealingAmount);
        }
        for (int i = 0; i < Mathf.Min(BrawlerBalance.WaveBaseEnemyCount + _wave, BrawlerBalance.WaveMaxEnemyCount); i++)
        {
            _pendingSpawnPositions.Add(CalculateSpawnPosition(_player.transform.position, i, _wave));
        }
        _spawnWarningRemaining = BrawlerBalance.SpawnWarningSeconds;
    }

    private static Vector3 CalculateSpawnPosition(Vector3 playerPosition, int index, int wave)
    {
        float side = wave >= BrawlerBalance.MixedEnemiesStartWave && index % 2 == 1 ? -1 : 1;
        float x = Mathf.Clamp(playerPosition.x + side * (BrawlerBalance.SpawnBaseDistance + index), -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth);
        // 端では反対側に回し、プレイヤーのすぐ上に突然出現することを防ぎます。
        if (Mathf.Abs(x - playerPosition.x) < BrawlerBalance.SpawnMinimumDistance)
        {
            x = Mathf.Clamp(playerPosition.x - side * (BrawlerBalance.SpawnBaseDistance + index), -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth);
        }
        return new Vector3(x, 0, Random.Range(-BrawlerBalance.SpawnLaneHalfWidth, BrawlerBalance.SpawnLaneHalfWidth));
    }

    private void UpdateSpawnWarning(float deltaTime)
    {
        if (IsRunFinished || _pendingSpawnPositions.Count == 0 || _player.Health <= 0)
        {
            return;
        }
        _spawnWarningRemaining = Mathf.Max(0, _spawnWarningRemaining - deltaTime);
        if (_spawnWarningRemaining > 0)
        {
            return;
        }
        for (int i = 0; i < _pendingSpawnPositions.Count; i++)
        {
            // 3 体に 1 体を遠距離型にして、近接型の背後から射線を作る混成戦にします。
            bool isRanged = i % BrawlerBalance.RangedSpawnInterval == BrawlerBalance.RangedSpawnOffset;
            // 第2ウェーブ以降、4体目を重装型にします。人数と遠距離型の比率は維持します。
            bool isHeavy = _wave >= BrawlerBalance.MixedEnemiesStartWave && i == BrawlerBalance.HeavySpawnIndex;
            bool isUndead = i == BrawlerBalance.UndeadSpawnIndex;
            _enemies.Add(CreateFighter(isHeavy ? "Heavy Enemy" : isRanged ? "Ranged Enemy" : isUndead ? "Undead Enemy" : "Melee Enemy",
                _pendingSpawnPositions[i], false, isRanged, isHeavy, isUndead));
        }
        _pendingSpawnPositions.Clear();
    }

    // Update は毎フレーム呼ばれます。ここでは戦闘全体の進行を管理します。
    private void Update()
    {
        // timeScale が0でも停止を解除できるよう、実時間で残り秒数を減らします。
        UpdateHitStop(Time.unscaledDeltaTime);
        _hitDirectionIndicator.Advance(Time.unscaledDeltaTime);
        _healthBarFeedback.Refresh(_player.Health, _player.MaxHealth, Time.deltaTime);
        _waveBanner.Advance(Time.deltaTime);
        // Unity の Object は Destroy 後に == null と判定できるため、この条件で整理できます。
        _enemies.RemoveAll(enemy => enemy == null);
        UpdateSpawnWarning(Time.deltaTime);
        // 2 秒間命中がなければ連続ヒットをリセット。スコア自体は残ります。
        _hitChainRemaining -= Time.deltaTime;
        if (_hitChainRemaining <= 0)
        {
            _hitChain = 0;
        }
        _cameraShakeRemaining = Mathf.Max(0, _cameraShakeRemaining - Time.deltaTime);
        UpdateWaveProgress(Time.deltaTime);
        // シーンを読み直すと Start から初期化されます。シーンのビルド設定への登録が必要です。
        if (BrawlerInput.WasRestartPressed())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void UpdateWaveProgress(float deltaTime)
    {
        if (IsRunFinished)
        {
            return;
        }
        if (_player.Health <= 0)
        {
            FinishRun(false);
            return;
        }
        if (_enemies.Count == 0 && _pendingSpawnPositions.Count == 0)
        {
            if (_wave >= BrawlerBalance.StageWaveCount)
            {
                // 最終ウェーブの敵が消えてから結果を確定し、六つ目のウェーブは予約しません。
                FinishRun(true);
                return;
            }
            // 空の状態が続いても通知を再生成せず、このウェーブにつき一度だけ表示します。
            if (!_waveClearAnnounced)
            {
                _waveClearAnnounced = true;
                _waveBanner.ShowClear();
            }
            // deltaTime は前フレームからの経過秒数。FPS に依存せず 2 秒を計測できます。
            _nextWaveElapsed += deltaTime;
            if (_nextWaveElapsed > BrawlerBalance.NextWaveDelaySeconds)
            {
                _nextWaveElapsed = 0;
                SpawnWave();
            }
        }
    }

    // 終了した瞬間の値を保存します。以後は呼び直しても結果を上書きしません。
    private void FinishRun(bool cleared)
    {
        if (IsRunFinished)
        {
            return;
        }
        IsStageCleared = cleared;
        IsDefeated = !cleared;
        ResultWave = Mathf.Clamp(_wave, 1, BrawlerBalance.StageWaveCount);
        ResultScore = _score;
        ResultDefeatedEnemies = _defeatedEnemyCount;
        _waveBanner.Clear();
        // 敗北時の出現予約も破棄し、リザルトの背後で次の敵が出ないようにします。
        _pendingSpawnPositions.Clear();
    }

    // 全キャラクターの Update の後にカメラを動かし、そのフレームの最新位置を追います。
    private void LateUpdate()
    {
        // カメラは左右だけ追従。端で Clamp するとステージ外が映りすぎるのを抑えられます。
        var focus = new Vector3(Mathf.Clamp(_player.transform.position.x, -BrawlerBalance.CameraFollowHalfWidth, BrawlerBalance.CameraFollowHalfWidth), 1, 0);
        _camera.transform.position = focus + new Vector3(0, 6, -13);
        _camera.transform.LookAt(focus);
        // 一瞬だけカメラをずらし、攻撃が命中した重さを表現します。
        // 毎フレーム基準位置から計算するため、揺れが積み重なって位置がずれることはありません。
        if (_cameraShakeRemaining > 0)
        {
            _camera.transform.position += Random.insideUnitSphere * _cameraShakeRemaining * BrawlerBalance.CameraShakeStrength;
        }
    }

    // 敵 AI にプレイヤーを公開する読み取り専用プロパティです。
    public Fighter Target => _player;

    public void ShowPickupNotice(Fighter target, BrawlerDefines.PickupKind kind)
    {
        var notice = new GameObject("Pickup Notice");
        // 二種類を同時に拾っても、通知が同じ行に重ならないよう高さを分けます。
        float height = BrawlerBalance.PickupNoticeHeight + (kind == BrawlerDefines.PickupKind.ThrowingKnife
            ? BrawlerBalance.PickupNoticeItemSpacing : 0);
        notice.transform.position = target.transform.position + Vector3.up * height;
        notice.AddComponent<PickupNotice>().Initialize(_camera, kind, _pickupNoticeFont);
    }

    private void OnDestroy()
    {
        _factory?.Dispose();
        if (_pickupNoticeFont != null)
        {
            Destroy(_pickupNoticeFont);
        }
    }

    public void ShowHealingNumber(Fighter target, int actualAmount)
    {
        if (actualAmount <= 0)
        {
            return;
        }
        // ダメージ表示と同じ上昇・フェードを共用し、回復は緑色の＋数値にします。
        var number = new GameObject("Healing Number");
        number.transform.position = target.transform.position + Vector3.up * BrawlerBalance.DamageNumberHeight;
        number.AddComponent<DamageNumber>().Initialize(_camera, actualAmount, BrawlerDefines.DamageKind.Healing);
    }

    public void HitUndeadTargets(ClericFighter caster)
    {
        // 対象一覧は管理役が保持します。床上のXZ距離で判定し、背後にも効果を与えます。
        CreateHitImpact(caster.transform.position + Vector3.up * BrawlerBalance.HitImpactHeight, true);
        float radiusSquared = BrawlerBalance.TurnUndeadRadius * BrawlerBalance.TurnUndeadRadius;
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0 || !enemy.IsUndead)
            {
                continue;
            }
            Vector3 offset = enemy.transform.position - caster.transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude <= radiusSquared)
            {
                // 共通被ダメージ処理に渡し、転倒・得点・撃破報酬・重複命中防止を維持します。
                enemy.TakeDamage(BrawlerBalance.TurnUndeadDamage, offset.x >= 0 ? 1 : -1);
            }
        }
    }

    // Damage が実際に受理された場合だけ呼びます。無敵中の攻撃はスコアに含めません。
    public void RegisterDamage(Fighter victim, int attackDamage = BrawlerBalance.NormalDamage, int actualDamage = 0,
        BrawlerDefines.DamageKind kind = BrawlerDefines.DamageKind.Normal)
    {
        if (IsRunFinished)
        {
            return;
        }
        PlaySound(attackDamage >= BrawlerBalance.KnockdownDamageThreshold ? BrawlerSound.StrongHit : BrawlerSound.Hit);
        CreateHitImpact(victim.transform.position + Vector3.up * BrawlerBalance.HitImpactHeight, attackDamage >= BrawlerBalance.KnockdownDamageThreshold);
        if (!victim.IsPlayer)
        {
            if (actualDamage > 0)
            {
                var number = new GameObject("Damage Number");
                // 敵の子にしないことで、撃破・破棄後も表示を最後まで残します。
                number.transform.position = victim.transform.position + Vector3.up * BrawlerBalance.DamageNumberHeight;
                number.AddComponent<DamageNumber>().Initialize(_camera, actualDamage, kind);
            }
            BeginHitStop(attackDamage >= BrawlerBalance.KnockdownDamageThreshold ? BrawlerBalance.StrongHitStopSeconds : BrawlerBalance.NormalHitStopSeconds);
        }
        _cameraShakeRemaining = BrawlerBalance.CameraShakeSeconds;
        if (victim.IsPlayer)
        {
            _hitChain = 0;
            _hitChainRemaining = 0;
            // Update の順序によらず、致命傷を受けた時点で全員の操作を止めます。
            if (victim.Health <= 0) FinishRun(false);
            return;
        }
        _hitChain++;
        _hitChainRemaining = BrawlerBalance.HitChainSeconds;
        _score += BrawlerBalance.HitScore * Mathf.Min(_hitChain, BrawlerBalance.HitScoreMultiplierLimit);
        if (victim.Health == 0)
        {
            _score += BrawlerBalance.DefeatScore;
            // 3 体倒すごとに確実に落とし、ウェーブをまたいでも撃破数を引き継ぎます。
            // RegisterDamage は受理されたダメージだけで呼ばれるため、同じ敵から二重に落ちません。
            _defeatedEnemyCount++;
            if (_defeatedEnemyCount % BrawlerBalance.KnifeDropInterval == 0)
            {
                DropKnife(victim.transform.position);
            }
            if (_defeatedEnemyCount % BrawlerBalance.PotionDropInterval == 0)
            {
                DropPotion(victim.transform.position);
            }
        }
    }

    private void BeginHitStop(float duration)
    {
        if (_hitStopRemaining <= 0)
        {
            _timeScaleBeforeHitStop = Time.timeScale;
        }
        // 複数の敵に同時命中しても秒数を足しません。最も長い停止だけを採用します。
        _hitStopRemaining = Mathf.Max(_hitStopRemaining, duration);
        Time.timeScale = 0;
    }

    private void UpdateHitStop(float deltaTime)
    {
        if (_hitStopRemaining <= 0)
        {
            return;
        }
        _hitStopRemaining = Mathf.Max(0, _hitStopRemaining - deltaTime);
        if (_hitStopRemaining <= 0)
        {
            Time.timeScale = _timeScaleBeforeHitStop;
        }
    }

    private void OnDisable()
    {
        // リスタートやPlay終了が停止中でも、次のシーンに timeScale=0 を残しません。
        if (_hitStopRemaining > 0)
        {
            Time.timeScale = _timeScaleBeforeHitStop;
            _hitStopRemaining = 0;
        }
    }

    public void CreateGuardImpact(Fighter defender)
    {
        CreateDefenseImpact(defender.GuardImpactPosition, BrawlerDefines.ImpactKind.Guard);
    }

    private void CreateDefenseImpact(Vector3 position, BrawlerDefines.ImpactKind kind)
    {
        bool isGuard = kind == BrawlerDefines.ImpactKind.Guard;
        var impact = new GameObject(isGuard ? "Guard Sparks" : "Counter Cross");
        impact.transform.position = position;
        int count = isGuard ? BrawlerBalance.GuardSparkCount : BrawlerBalance.CounterRayCount;
        Material material = isGuard ? _factory.GuardImpactMaterial : _factory.CounterImpactMaterial;
        for (int i = 0; i < count; i++)
        {
            float angle = i * BrawlerBalance.ImpactFullCircleDegrees / count;
            Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Transform ray = _factory.CreateBodyPart("Ray", direction * BrawlerBalance.DefenseRayOffset,
                new Vector3(BrawlerBalance.DefenseRayLength, BrawlerBalance.DefenseRayThickness,
                    BrawlerBalance.DefenseRayThickness), material, impact.transform);
            ray.localRotation = Quaternion.Euler(0, 0, angle);
        }
        impact.AddComponent<HitImpact>().Initialize(
            isGuard ? BrawlerBalance.GuardImpactSize : BrawlerBalance.CounterImpactSize,
            isGuard ? BrawlerBalance.GuardImpactSeconds : BrawlerBalance.CounterImpactSeconds, kind);
    }

    private void CreateHitImpact(Vector3 position, bool isStrong)
    {
        var impactObject = new GameObject("Hit Impact");
        impactObject.transform.position = position;
        // 横向きカメラから見える XY 平面に、6 本の短い光線を並べます。
        for (int i = 0; i < BrawlerBalance.HitImpactRayCount; i++)
        {
            float angle = i * BrawlerBalance.ImpactFullCircleDegrees / BrawlerBalance.HitImpactRayCount;
            Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Transform ray = _factory.CreateBodyPart("Impact ray", direction * BrawlerBalance.HitImpactRayOffset,
                new Vector3(BrawlerBalance.HitImpactRayLength, BrawlerBalance.HitImpactRayThickness, BrawlerBalance.HitImpactRayThickness), _factory.ImpactMaterial, impactObject.transform);
            ray.localRotation = Quaternion.Euler(0, 0, angle);
        }
        impactObject.AddComponent<HitImpact>().Initialize(isStrong ? BrawlerBalance.StrongHitImpactSize : BrawlerBalance.NormalHitImpactSize);
    }

    public void ThrowKnife(Fighter owner)
    {
        Vector3 position = owner.transform.position + Vector3.up * BrawlerBalance.KnifeHeight
            + Vector3.right * (owner.Facing * BrawlerBalance.KnifeLaunchOffset);
        var knife = new GameObject("Thrown Knife");
        knife.transform.position = position;
        _factory.CreateBodyPart("Knife blade", Vector3.zero, new Vector3(0.65f, 0.08f, 0.12f), _factory.KnifeMaterial, knife.transform);
        _factory.CreateBodyPart("Knife grip", Vector3.left * (owner.Facing * 0.4f), new Vector3(0.2f, 0.12f, 0.14f), _factory.FloorMaterial, knife.transform);
        knife.AddComponent<ThrowingKnifeProjectile>().Initialize(this, owner);
    }

    public Fighter FindKnifeTarget(Vector3 start, Vector3 end)
    {
        Fighter nearest = null;
        float nearestFraction = float.PositiveInfinity;
        Vector3 travel = end - start;
        float hitRadiusSquared = BrawlerBalance.KnifeHitRadius * BrawlerBalance.KnifeHitRadius;
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            Vector3 target = enemy.transform.position + Vector3.up * (BrawlerBalance.KnifeHeight + enemy.JumpHeight);
            // 発射地点より後ろや射程末端より先の敵には当てません。
            float fraction = travel.sqrMagnitude > 0 ? Vector3.Dot(target - start, travel) / travel.sqrMagnitude : 0;
            if (fraction < 0 || fraction > 1)
            {
                continue;
            }
            Vector3 closest = start + travel * fraction;
            if ((target - closest).sqrMagnitude <= hitRadiusSquared && fraction < nearestFraction)
            {
                nearest = enemy;
                nearestFraction = fraction;
            }
        }
        return nearest;
    }

    private void DropKnife(Vector3 position)
    {
        var knife = new GameObject("Knife Pickup");
        // ポーションと同時に落ちても重ならず、床に銀色の刃が見えるよう少しずらします。
        knife.transform.position = new Vector3(position.x, 0, Mathf.Clamp(position.z + 0.4f, -3, 3));
        _factory.CreateBodyPart("Blade", new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.08f, 0.18f), _factory.KnifeMaterial, knife.transform);
        _factory.CreateBodyPart("Grip", new Vector3(-0.4f, 0.2f, 0), new Vector3(0.2f, 0.12f, 0.2f), _factory.FloorMaterial, knife.transform);
        knife.AddComponent<ThrowingKnifePickup>().Initialize(_player, _audio);
    }

    private void DropPotion(Vector3 position)
    {
        var potionObject = new GameObject("Healing Potion");
        potionObject.transform.position = new Vector3(position.x, 0, position.z);
        // 瓶の本体と口を作ります。見た目の高さと、床上の拾得位置は分離します。
        _factory.CreateBodyPart("Bottle", new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.4f, 0.35f),
            _factory.PotionMaterial, potionObject.transform);
        _factory.CreateBodyPart("Bottle neck", new Vector3(0, 0.57f, 0), new Vector3(0.16f, 0.15f, 0.16f),
            _factory.SkinMaterial, potionObject.transform);
        potionObject.AddComponent<HealingPotion>().Initialize(_player, _audio);
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
                    int healthBeforeHit = enemy.Health;
                    if (attacker.IsPushAttacking)
                    {
                        enemy.TakePushDamage(damage, attacker.Facing);
                    }
                    else
                    {
                        BrawlerDefines.DamageKind kind = attacker.IsCounterAttacking ? BrawlerDefines.DamageKind.Counter
                            : attacker.IsHeavyStriking ? BrawlerDefines.DamageKind.HeavyStrike : BrawlerDefines.DamageKind.Normal;
                        enemy.TakeDamage(damage, attacker.Facing, kind);
                    }
                    // 無敵・硬直・撃破済みで拒否された命中や空振りでは、反撃専用の演出を出しません。
                    if (attacker.IsCounterAttacking && enemy.Health < healthBeforeHit)
                    {
                        CreateDefenseImpact(enemy.transform.position + Vector3.up * BrawlerBalance.CounterImpactHeight,
                            BrawlerDefines.ImpactKind.Counter);
                    }
                }
            }
        }
        else if (IsInAttackRange(attacker, _player))
        {
            _player.TakeDamage(damage, attacker.Facing);
        }
    }

    public void HitRunningTargets(Fighter attacker)
    {
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            Vector3 delta = enemy.transform.position - attacker.transform.position;
            // 体当たりは通常攻撃より近い、前方への接触だけを判定します。
            if (Mathf.Abs(delta.x) < BrawlerBalance.BodyCheckReach && Mathf.Abs(delta.z) < BrawlerBalance.ContactLaneHalfWidth && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance)
            {
                attacker.TryBodyCheck(enemy);
            }
        }
    }

    public void HitSlidingTargets(Fighter attacker)
    {
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            Vector3 delta = enemy.transform.position - attacker.transform.position;
            if (Mathf.Abs(delta.x) < BrawlerBalance.SlideHitReach && Mathf.Abs(delta.z) < BrawlerBalance.ContactLaneHalfWidth && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance)
            {
                attacker.TrySlideHit(enemy);
            }
        }
    }

    public Fighter FindGroundAttackTarget(Fighter attacker)
    {
        Fighter closest = null;
        float closestDistance = float.MaxValue;
        foreach (Fighter enemy in _enemies)
        {
            if (enemy != null && enemy.CanReceiveGroundHit && IsInGroundAttackRange(attacker, enemy))
            {
                float distance = (enemy.transform.position - attacker.transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }
        }
        return closest;
    }

    public void HitGroundTarget(Fighter attacker, Fighter target, int damage)
    {
        if (target != null && IsInGroundAttackRange(attacker, target))
        {
            target.TryTakeGroundHit(damage, attacker.Facing);
        }
    }

    private static bool IsInGroundAttackRange(Fighter attacker, Fighter target)
    {
        Vector3 delta = target.transform.position - attacker.transform.position;
        // 追撃は通常攻撃より短いリーチで、向いている側の倒れた敵を狙います。
        return attacker.IsPlayer && attacker.Health > 0 && Mathf.Abs(delta.x) < BrawlerBalance.GroundAttackReach
            && Mathf.Abs(delta.z) < BrawlerBalance.AttackLaneHalfWidth && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance;
    }

    // コライダーではなく、攻撃者と対象の位置の差から当たり判定を作ります。
    private bool IsInAttackRange(Fighter attacker, Fighter target)
    {
        Vector3 delta = target.transform.position - attacker.transform.position;
        // プレイヤーの左右方向のリーチだけを従来の 1.5 倍にします（1.65 × 1.5 = 2.475）。
        // 敵は 1.65 のままにして、鈍器の間合いを活かせるようにします。
        float attackReach = attacker.IsHeavyStriking ? BrawlerBalance.HeavyStrikeReach
            : attacker.IsPushAttacking ? BrawlerBalance.PushReach : attacker.IsUppercutAttacking ? BrawlerBalance.UppercutReach
            : attacker.IsPlayer ? BrawlerBalance.PlayerAttackReach : BrawlerBalance.EnemyAttackReach;
        // 生存・奥行き差・左右距離・向きの 4 条件をすべて満たすと命中します。
        // facing は右 +1 / 左 -1。差に掛けると、左右どちらでも前方が正になります。
        // -0.25 の余裕を持たせ、ほぼ重なった相手にも攻撃が当たるようにしています。
        return target.Health > 0
            && Mathf.Abs(delta.z) < BrawlerBalance.AttackLaneHalfWidth
            && Mathf.Abs(delta.x) < attackReach
            && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance;
    }

    // Unity の簡易 GUI。画面左上を原点とするピクセル座標で文字や HP バーを描きます。
    // 1 フレームに複数回呼ばれることがあるため、ゲーム進行はここで更新しません。
    private void OnGUI()
    {
        _hud.Draw(this, _player, _enemies, _camera, _wave, _score, _hitChain, _healthBarFeedback,
            _pendingSpawnPositions, _spawnWarningRemaining, _pickupNoticeFont, _waveBanner, _hitDirectionIndicator);
    }
}
