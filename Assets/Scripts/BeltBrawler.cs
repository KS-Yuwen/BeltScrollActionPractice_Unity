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
    // シーン再読み込みでも選択を維持します。検証・既存シーンはクレリックを既定にします。
    private static BrawlerDefines.PlayerClass s_playerClass = BrawlerDefines.PlayerClass.Cleric;
    public BrawlerDefines.PlayerClass SelectedPlayerClass => s_playerClass;

    public void StartAs(BrawlerDefines.PlayerClass playerClass)
    {
        s_playerClass = playerClass;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

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
    public bool IsPreparingBoss { get; private set; }
    private readonly List<CollectibleItem> _preparationSupplies = new List<CollectibleItem>();
    private static readonly BrawlerDefines.UsableAction[] s_preparationSpells =
    {
        BrawlerDefines.UsableAction.Healing,
        BrawlerDefines.UsableAction.Protection,
        BrawlerDefines.UsableAction.AttackBoost,
        BrawlerDefines.UsableAction.MagicMissile
    };
    private SpellFighter _spellEffectOwner;
    // 時間を進める役は術者です。死亡・破棄・終了時は所有者の状態から即座に停止を解除します。
    public bool IsSpellEffectActive => !IsRunFinished && _spellEffectOwner != null
        && _spellEffectOwner.Health > 0 && _spellEffectOwner.IsReleasingSpell;

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
    private bool _bossDefeatStarted;
    private float _bossDefeatRemaining;
    private float _bossSlowRemaining;
    private float _timeScaleBeforeBossSlow = 1f;
    public bool IsShowingBossDefeat => _bossDefeatRemaining > 0 && !IsRunFinished;
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
        _player = _factory.CreateFighter("Player", new Vector3(-5, 0, 0), true, _wave, playerClass: s_playerClass);
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
        if (IsRunFinished || IsPreparingBoss || _wave >= BrawlerBalance.StageWaveCount)
        {
            return;
        }
        _wave++;
        _waveClearAnnounced = false;
        _waveBanner.ShowStart(_wave);
        // 全滅のご褒美として少量回復し、次の戦闘にも挑みやすくします。
        if (_wave > 1 && _wave < BrawlerBalance.StageWaveCount)
        {
            _player.RestoreHealth(BrawlerBalance.WaveHealingAmount);
        }
        int enemyCount = _wave == BrawlerBalance.StageWaveCount ? BrawlerBalance.BossWaveEnemyCount
            : Mathf.Min(BrawlerBalance.WaveBaseEnemyCount + _wave, BrawlerBalance.WaveMaxEnemyCount);
        for (int i = 0; i < enemyCount; i++)
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
            if (_wave == BrawlerBalance.StageWaveCount)
            {
                _enemies.Add(_factory.CreateFighter("Warden Boss", _pendingSpawnPositions[i], false, _wave,
                    isBoss: true));
                continue;
            }
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
        UpdateBossDefeat(Time.unscaledDeltaTime);
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
        if (BrawlerInput.WasClericSelected())
        {
            StartAs(BrawlerDefines.PlayerClass.Cleric);
            return;
        }
        if (BrawlerInput.WasFighterSelected())
        {
            StartAs(BrawlerDefines.PlayerClass.Fighter);
            return;
        }
        if (BrawlerInput.WasDwarfSelected())
        {
            StartAs(BrawlerDefines.PlayerClass.Dwarf);
            return;
        }
        if (BrawlerInput.WasThiefSelected())
        {
            StartAs(BrawlerDefines.PlayerClass.Thief);
            return;
        }
        if (BrawlerInput.WasElfSelected())
        {
            StartAs(BrawlerDefines.PlayerClass.Elf);
            return;
        }
        bool wasPreparingBoss = IsPreparingBoss;
        UpdateWaveProgress(Time.deltaTime);
        // シーンを読み直すと Start から初期化されます。シーンのビルド設定への登録が必要です。
        if (BrawlerInput.WasRestartPressed(allowGamepad: !wasPreparingBoss))
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
        if (IsPreparingBoss)
        {
            if (BrawlerInput.WasPreparationConfirmPressed())
            {
                TryStartBossBattle();
            }
            return;
        }
        if (_enemies.Count == 0 && _pendingSpawnPositions.Count == 0)
        {
            if (_wave >= BrawlerBalance.StageWaveCount)
            {
                // 消滅と撃破表示の両方が終わってからリザルトへ進みます。
                if (IsShowingBossDefeat)
                {
                    return;
                }
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
                if (_wave == BrawlerBalance.StageWaveCount - 1)
                {
                    BeginBossPreparation();
                }
                else
                {
                    SpawnWave();
                }
            }
        }
    }

    private void BeginBossPreparation()
    {
        if (IsPreparingBoss || IsRunFinished || _player.Health <= 0)
        {
            return;
        }
        IsPreparingBoss = true;
        _waveBanner.Clear();
        _player.RestoreHealth(BrawlerBalance.BossPreparationHealingAmount);
        // 端にいても支給品が場外へ出ない位置に並べます。取得は通常の6スロット規則を使います。
        float halfSpan = (BrawlerBalance.BossPreparationSupplyCount - 1) * BrawlerBalance.BossPreparationSupplySpacing / 2;
        float center = Mathf.Clamp(_player.transform.position.x,
            -BrawlerBalance.StageHalfWidth + halfSpan, BrawlerBalance.StageHalfWidth - halfSpan);
        Vector3 first = new Vector3(center - halfSpan, 0, 0);
        _preparationSupplies.Add(DropPotion(first));
        _preparationSupplies.Add(DropHammer(first + Vector3.right * BrawlerBalance.BossPreparationSupplySpacing));
        for (int i = 0; i < s_preparationSpells.Length; i++)
        {
            BrawlerDefines.UsableAction action = s_preparationSpells[i];
            if (_player.CanUseAction(action))
            {
                _preparationSupplies.Add(DropMagicScroll(first + Vector3.right
                    * ((i + BrawlerBalance.BossPreparationSpellSupplyOffset) * BrawlerBalance.BossPreparationSupplySpacing), action));
            }
        }
        foreach (CollectibleItem supply in _preparationSupplies)
        {
            supply.SetLifetimePaused(true);
        }
    }

    public bool TryStartBossBattle()
    {
        if (!IsPreparingBoss || IsRunFinished || !_player.IsReadyForStageTransition)
        {
            return false;
        }
        IsPreparingBoss = false;
        ReleasePreparationSupplies();
        SpawnWave();
        return true;
    }

    private void ReleasePreparationSupplies()
    {
        foreach (CollectibleItem supply in _preparationSupplies)
        {
            if (supply != null)
            {
                supply.SetLifetimePaused(false);
            }
        }
        _preparationSupplies.Clear();
    }

    // 終了した瞬間の値を保存します。以後は呼び直しても結果を上書きしません。
    private void FinishRun(bool cleared)
    {
        if (IsRunFinished)
        {
            return;
        }
        RestoreCombatTime();
        IsStageCleared = cleared;
        IsDefeated = !cleared;
        IsPreparingBoss = false;
        ReleasePreparationSupplies();
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
        float height = BrawlerBalance.PickupNoticeHeight + (kind == BrawlerDefines.PickupKind.ThrowingHammer
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
        bool isStrongHit = kind == BrawlerDefines.DamageKind.Critical || attackDamage >= BrawlerBalance.KnockdownDamageThreshold;
        PlaySound(isStrongHit ? BrawlerSound.StrongHit : BrawlerSound.Hit);
        CreateHitImpact(victim.transform.position + Vector3.up * BrawlerBalance.HitImpactHeight, isStrongHit);
        if (!victim.IsPlayer)
        {
            if (actualDamage > 0)
            {
                var number = new GameObject("Damage Number");
                // 敵の子にしないことで、撃破・破棄後も表示を最後まで残します。
                number.transform.position = victim.transform.position + Vector3.up * BrawlerBalance.DamageNumberHeight;
                number.AddComponent<DamageNumber>().Initialize(_camera, actualDamage, kind);
            }
            // 魔法は専用の敵停止中も演出を進めます。全体のtimeScaleを0にする命中停止は付けません。
            if (kind != BrawlerDefines.DamageKind.Spell)
            {
                BeginHitStop(isStrongHit ? BrawlerBalance.StrongHitStopSeconds : BrawlerBalance.NormalHitStopSeconds);
            }
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
            if (victim is BossFighter && _wave == BrawlerBalance.StageWaveCount)
            {
                BeginBossDefeat();
            }
            _score += BrawlerBalance.DefeatScore;
            // 3 体倒すごとに確実に落とし、ウェーブをまたいでも撃破数を引き継ぎます。
            // RegisterDamage は受理されたダメージだけで呼ばれるため、同じ敵から二重に落ちません。
            _defeatedEnemyCount++;
            if (_player.CanUseAction(BrawlerDefines.UsableAction.ThrowingHammer)
                && _defeatedEnemyCount % BrawlerBalance.HammerDropInterval == 0)
            {
                DropHammer(victim.transform.position);
            }
            else if (_player.CanUseAction(BrawlerDefines.UsableAction.ThrowingKnife)
                && _defeatedEnemyCount % BrawlerBalance.KnifeDropInterval == 0)
            {
                DropKnife(victim.transform.position);
            }
            if (_defeatedEnemyCount % BrawlerBalance.PotionDropInterval == 0)
            {
                DropPotion(victim.transform.position);
            }
            if (_defeatedEnemyCount % BrawlerBalance.ScrollDropInterval == 0)
            {
                // 撃破数を通算し、回復・防御・攻撃強化のスクロールを順番に落とします。
                int dropIndex = _defeatedEnemyCount / BrawlerBalance.ScrollDropInterval;
                BrawlerDefines.UsableAction action = ((dropIndex - 1) % BrawlerBalance.ScrollActionCycleLength) switch
                {
                    0 => BrawlerDefines.UsableAction.Healing,
                    1 => BrawlerDefines.UsableAction.Protection,
                    _ => BrawlerDefines.UsableAction.AttackBoost
                };
                if (_player.CanUseAction(BrawlerDefines.UsableAction.MagicMissile))
                {
                    action = BrawlerDefines.UsableAction.MagicMissile;
                }
                if (_player.CanUseAction(action))
                {
                    DropMagicScroll(victim.transform.position, action);
                }
            }
        }
    }

    private void BeginHitStop(float duration)
    {
        if (_hitStopRemaining <= 0)
        {
            // 低速再生中の値を元の速度として保存しないようにします。
            _timeScaleBeforeHitStop = _bossSlowRemaining > 0 ? _timeScaleBeforeBossSlow : Time.timeScale;
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
            // 致命傷のヒットストップが終わったら、続けて撃破の低速再生へ入ります。
            Time.timeScale = _bossSlowRemaining > 0
                ? _timeScaleBeforeBossSlow * BrawlerBalance.BossDefeatSlowTimeScale : _timeScaleBeforeHitStop;
        }
    }

    private void BeginBossDefeat()
    {
        if (_bossDefeatStarted || IsRunFinished)
        {
            return;
        }
        _bossDefeatStarted = true;
        _bossDefeatRemaining = BrawlerBalance.BossDefeatNoticeSeconds;
        _bossSlowRemaining = BrawlerBalance.BossDefeatSlowSeconds;
        _timeScaleBeforeBossSlow = _hitStopRemaining > 0 ? _timeScaleBeforeHitStop : Time.timeScale;
        if (_hitStopRemaining <= 0)
        {
            Time.timeScale = _timeScaleBeforeBossSlow * BrawlerBalance.BossDefeatSlowTimeScale;
        }
    }

    private void UpdateBossDefeat(float unscaledDeltaTime)
    {
        float elapsed = Mathf.Max(0, unscaledDeltaTime);
        _bossDefeatRemaining = Mathf.Max(0, _bossDefeatRemaining - elapsed);
        // 最後の一撃の停止中には低速再生の時間を消費しません。
        if (_bossSlowRemaining <= 0 || _hitStopRemaining > 0)
        {
            return;
        }
        _bossSlowRemaining = Mathf.Max(0, _bossSlowRemaining - elapsed);
        if (_bossSlowRemaining <= 0)
        {
            Time.timeScale = _timeScaleBeforeBossSlow;
        }
    }

    private void RestoreCombatTime()
    {
        // リスタート・敗北・Play終了でも、次のシーンへ停止や低速再生を持ち越しません。
        if (_bossSlowRemaining > 0)
        {
            Time.timeScale = _timeScaleBeforeBossSlow;
        }
        else if (_hitStopRemaining > 0)
        {
            Time.timeScale = _timeScaleBeforeHitStop;
        }
        _bossSlowRemaining = 0;
        _hitStopRemaining = 0;
        _bossDefeatRemaining = 0;
    }

    private void OnDisable()
    {
        RestoreCombatTime();
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

    // 攻撃魔法・回復魔法から共通に呼びます。停止期間は術者の発動状態と常に同期します。
    public void BeginSpellEffect(SpellFighter caster)
    {
        if (!IsRunFinished && caster != null && caster.IsPlayer && caster.Health > 0 && caster.IsReleasingSpell)
        {
            _spellEffectOwner = caster;
        }
    }

    public void ShowSupportSpellEffect(SpellFighter caster, BrawlerDefines.UsableAction action, Color color)
    {
        if (caster == null || !caster.IsPlayer || caster.Health <= 0 || IsRunFinished || !caster.IsReleasingSpell)
        {
            return;
        }
        var effect = new GameObject("Support Spell Effect").AddComponent<SupportSpellEffect>();
        effect.Initialize(caster, action, _factory.SupportSpellMaterial, color);
    }

    // 魔法の対象は床上の左右・奥行きで選びます。近接のリーチや威力倍率から独立させます。
    public void CastMagicMissile(Fighter caster)
    {
        if (caster == null || !caster.IsPlayer || caster.Health <= 0 || IsRunFinished
            || !caster.CanUseAction(BrawlerDefines.UsableAction.MagicMissile))
        {
            return;
        }
        Fighter target = null;
        float nearest = BrawlerBalance.MagicMissileRange;
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            Vector3 offset = enemy.transform.position - caster.transform.position;
            float forward = offset.x * caster.Facing;
            if (forward > 0 && forward <= nearest && Mathf.Abs(offset.z) <= BrawlerBalance.MagicMissileDepth)
            {
                target = enemy;
                nearest = forward;
            }
        }
        Vector3 start = caster.transform.position + Vector3.up * BrawlerBalance.MagicMissileHeight;
        Vector3 end = target != null
            ? target.transform.position + Vector3.up * (BrawlerBalance.MagicMissileHeight + target.JumpHeight)
            : start + Vector3.right * (caster.Facing * BrawlerBalance.MagicMissileRange);
        // 即時命中の仮演出。Colliderを作らず、描画した光が再度ダメージを与えないようにします。
        var trail = new GameObject("Magic Missile Trail");
        var line = trail.AddComponent<LineRenderer>();
        line.sharedMaterial = _factory.ImpactMaterial;
        line.startColor = Color.cyan;
        line.endColor = Color.white;
        line.startWidth = BrawlerBalance.MagicMissileTrailWidth;
        line.endWidth = BrawlerBalance.MagicMissileTrailWidth;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        trail.AddComponent<MagicMissileEffect>().Initialize(caster as SpellFighter, line);
        if (target != null)
        {
            target.TakeDamage(BrawlerBalance.MagicMissileDamage, caster.Facing, BrawlerDefines.DamageKind.Spell);
        }
    }

    public void ThrowHammer(Fighter owner)
    {
        if (owner == null || !owner.IsPlayer || IsRunFinished || !owner.CanUseAction(BrawlerDefines.UsableAction.ThrowingHammer))
        {
            return;
        }
        Vector3 position = owner.transform.position + Vector3.up * BrawlerBalance.HammerHeight
            + Vector3.right * (owner.Facing * BrawlerBalance.HammerLaunchOffset);
        var hammer = new GameObject("Thrown Hammer");
        hammer.transform.position = position;
        CreateHammerModel(hammer.transform, Vector3.zero);
        hammer.AddComponent<ThrowingHammerProjectile>().Initialize(this, owner);
    }

    public void ThrowKnife(Fighter owner)
    {
        if (owner == null || !owner.IsPlayer || IsRunFinished || !owner.CanUseAction(BrawlerDefines.UsableAction.ThrowingKnife))
        {
            return;
        }
        var knife = new GameObject("Thrown Knife");
        knife.transform.position = owner.transform.position + Vector3.up * BrawlerBalance.KnifeHeight
            + Vector3.right * (owner.Facing * BrawlerBalance.KnifeLaunchOffset);
        CreateKnifeModel(knife.transform, Vector3.zero);
        knife.AddComponent<ThrowingKnifeProjectile>().Initialize(this, owner);
    }

    // ナイフは直進する刃と柄を持ち、ハンマーとは別の形状で生成します。
    private void CreateKnifeModel(Transform parent, Vector3 offset)
    {
        _factory.CreateBodyPart("Knife blade", offset,
            new Vector3(BrawlerBalance.KnifeBladeLength, BrawlerBalance.KnifeBladeThickness, BrawlerBalance.KnifeBladeDepth),
            _factory.HammerMaterial, parent);
        _factory.CreateBodyPart("Knife grip", offset + Vector3.left * BrawlerBalance.KnifeGripOffset,
            new Vector3(BrawlerBalance.KnifeGripLength, BrawlerBalance.KnifeGripThickness, BrawlerBalance.KnifeGripDepth),
            _factory.FloorMaterial, parent);
    }

    // 床の取得物と飛んでいるハンマーで、同じ頭部・柄の仮モデルを使います。
    private void CreateHammerModel(Transform parent, Vector3 offset)
    {
        _factory.CreateBodyPart("Hammer head", offset,
            new Vector3(BrawlerBalance.HammerHeadWidth, BrawlerBalance.HammerHeadHeight, BrawlerBalance.HammerHeadDepth),
            _factory.HammerMaterial, parent);
        _factory.CreateBodyPart("Hammer handle", offset + Vector3.up * BrawlerBalance.HammerHandleOffset,
            new Vector3(BrawlerBalance.HammerHandleThickness, BrawlerBalance.HammerHandleLength, BrawlerBalance.HammerHandleThickness),
            _factory.FloorMaterial, parent);
    }
    public Fighter FindThrownProjectileTarget(Vector3 start, Vector3 end, float hitRadius, float targetHeight,
        float targetVerticalHalfHeight = 0, System.Predicate<Fighter> canHit = null)
    {
        Fighter nearest = null;
        float nearestFraction = float.PositiveInfinity;
        Vector3 travel = end - start;
        foreach (Fighter enemy in _enemies)
        {
            if (enemy == null || enemy.Health <= 0 || (canHit != null && !canHit(enemy)))
            {
                continue;
            }
            Vector3 target = enemy.transform.position + Vector3.up * (targetHeight + enemy.JumpHeight);
            // 発射地点より後ろや射程末端より先の敵には当てません。
            float fraction = travel.sqrMagnitude > 0 ? Vector3.Dot(target - start, travel) / travel.sqrMagnitude : 0;
            if (targetVerticalHalfHeight <= 0 && (fraction < 0 || fraction > 1))
            {
                continue;
            }
            Vector3 closest = start + travel * fraction;
            // ハンマーは点ではなく縦に伸びた身体を対象にします。ナイフは従来どおり中心点です。
            // 飛行区間と縦線分の最短距離を調べ、放物線の上昇中も身体との接触を拾います。
            if (targetVerticalHalfHeight > 0)
            {
                // 身体に幅がある場合は、中心の射影が区間外でも端点との接触を調べます。
                // 中心だけで除外すると、接触が一瞬になり大型相手への2ヒットを見落とします。
                Vector3 horizontalTravel = new Vector3(travel.x, 0, travel.z);
                Vector3 horizontalOffset = target - start;
                horizontalOffset.y = 0;
                fraction = horizontalTravel.sqrMagnitude > 0
                    ? Mathf.Clamp01(Vector3.Dot(horizontalOffset, horizontalTravel) / horizontalTravel.sqrMagnitude) : fraction;
                closest = start + travel * fraction;
                target.y = Mathf.Clamp(closest.y, target.y - targetVerticalHalfHeight, target.y + targetVerticalHalfHeight);
                // 端点の丸い範囲だけでなく、区間内の斜め接触も判定します。
                fraction = travel.sqrMagnitude > 0
                    ? Mathf.Clamp01(Vector3.Dot(target - start, travel) / travel.sqrMagnitude) : 0;
                closest = start + travel * fraction;
            }
            // ハンマーだけ体格による横幅を加味します。直進ナイフの判定は維持します。
            float targetRadius = targetVerticalHalfHeight > 0 ? hitRadius + enemy.HammerBodyRadius : hitRadius;
            if ((target - closest).sqrMagnitude <= targetRadius * targetRadius && fraction < nearestFraction)
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
        knife.transform.position = new Vector3(position.x, 0, Mathf.Clamp(position.z + BrawlerBalance.KnifePickupLaneOffset,
            -BrawlerBalance.StageHalfDepth, BrawlerBalance.StageHalfDepth));
        CreateKnifeModel(knife.transform, Vector3.up * BrawlerBalance.KnifePickupHeight);
        knife.AddComponent<ThrowingKnifePickup>().Initialize(_player, _audio);
    }

    private ThrowingHammerPickup DropHammer(Vector3 position)
    {
        var hammer = new GameObject("Hammer Pickup");
        // ポーションと同時に落ちても重ならず、床に銀色のハンマー頭部が見えるよう少しずらします。
        hammer.transform.position = new Vector3(position.x, 0, Mathf.Clamp(position.z + BrawlerBalance.HammerPickupLaneOffset, -BrawlerBalance.StageHalfDepth, BrawlerBalance.StageHalfDepth));
        CreateHammerModel(hammer.transform, Vector3.up * BrawlerBalance.HammerPickupHeight);
        var pickup = hammer.AddComponent<ThrowingHammerPickup>();
        pickup.Initialize(_player, _audio);
        return pickup;
    }

    private MagicScrollPickup DropMagicScroll(Vector3 position, BrawlerDefines.UsableAction action)
    {
        var scroll = new GameObject("Magic Scroll Pickup");
        // 同時に落ちるポーション・ハンマーと重なりにくい位置に置きます。
        scroll.transform.position = new Vector3(position.x, 0,
            Mathf.Clamp(position.z + BrawlerBalance.ScrollLaneOffset,
                -BrawlerBalance.StageHalfDepth, BrawlerBalance.StageHalfDepth));
        Vector3 modelPosition = Vector3.up * BrawlerBalance.ScrollModelHeight;
        _factory.CreateBodyPart("Scroll parchment", modelPosition,
            new Vector3(BrawlerBalance.ScrollModelWidth, BrawlerBalance.ScrollModelHeight,
                BrawlerBalance.ScrollModelDepth), _factory.SkinMaterial, scroll.transform);
        Material bandMaterial = action switch
        {
            BrawlerDefines.UsableAction.Healing => _factory.HealingScrollMaterial,
            BrawlerDefines.UsableAction.AttackBoost => _factory.AttackBoostScrollMaterial,
            BrawlerDefines.UsableAction.MagicMissile => _factory.ImpactMaterial,
            _ => _factory.ProtectionScrollMaterial
        };
        _factory.CreateBodyPart("Scroll band", modelPosition,
            new Vector3(BrawlerBalance.ScrollBandWidth, BrawlerBalance.ScrollBandHeight,
                BrawlerBalance.ScrollBandDepth), bandMaterial, scroll.transform);
        var pickup = scroll.AddComponent<MagicScrollPickup>();
        pickup.Initialize(_player, _audio, action);
        return pickup;
    }

    private HealingPotion DropPotion(Vector3 position)
    {
        var potionObject = new GameObject("Healing Potion");
        potionObject.transform.position = new Vector3(position.x, 0, position.z);
        // 瓶の本体と口を作ります。見た目の高さと、床上の拾得位置は分離します。
        _factory.CreateBodyPart("Bottle", new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.4f, 0.35f),
            _factory.PotionMaterial, potionObject.transform);
        _factory.CreateBodyPart("Bottle neck", new Vector3(0, 0.57f, 0), new Vector3(0.16f, 0.15f, 0.16f),
            _factory.SkinMaterial, potionObject.transform);
        var pickup = potionObject.AddComponent<HealingPotion>();
        pickup.Initialize(_player, _audio);
        return pickup;
    }

    // 攻撃者の陣営に応じて対象を選びます。プレイヤーの攻撃は範囲内の敵全員に当たります。
    public Fighter FindBackstabTarget(Fighter attacker, float direction)
    {
        Fighter nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Fighter target in _enemies)
        {
            if (CanBackstabTarget(attacker, target, direction))
            {
                float distance = (target.transform.position - attacker.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }
        }
        return nearest;
    }

    private bool CanBackstabTarget(Fighter attacker, Fighter target, float direction)
    {
        if (IsRunFinished || target == null || !target.CanBeBackstabbed || target.Health <= 0 || target.IsDowned)
        {
            return false;
        }
        Vector3 delta = target.transform.position - attacker.transform.position;
        // 相手の向きを基準に背後を判定し、自分の前にいる近距離の敵だけを対象にします。
        return delta.x * target.Facing > BrawlerBalance.BackstabBehindMinimum
            && delta.x * direction > 0 && Mathf.Abs(delta.x) < BrawlerBalance.BackstabReach
            && Mathf.Abs(delta.z) < BrawlerBalance.BackstabLaneHalfWidth;
    }

    public void HitBackstabTarget(Fighter attacker, Fighter target, int damage)
    {
        // 対象の移動・振り向き・撃破を命中時にも確認し、選んだ一体以外へは当てません。
        if (CanBackstabTarget(attacker, target, attacker.Facing))
        {
            target.TakeMeleeDamage(attacker.CalculateMeleeDamage(damage), attacker, BrawlerDefines.DamageKind.Normal);
        }
    }

    public void Hit(Fighter attacker, int damage)
    {
        damage = attacker.CalculateMeleeHitDamage(damage);
        if (attacker.IsPlayer)
        {
            bool didHit = false;
            foreach (var enemy in _enemies)
            {
                if (enemy != null && IsInAttackRange(attacker, enemy))
                {
                    int healthBeforeHit = enemy.Health;
                    if (attacker.IsPushAttacking)
                    {
                        enemy.TakeMeleeDamage(damage, attacker, BrawlerDefines.DamageKind.Normal);
                        if (enemy.Health < healthBeforeHit)
                        {
                            enemy.ApplyPushKnockback(attacker.Facing);
                        }
                    }
                    else
                    {
                        BrawlerDefines.DamageKind kind = attacker.IsCriticalAttack ? BrawlerDefines.DamageKind.Critical
                            : attacker.IsCounterAttacking ? BrawlerDefines.DamageKind.Counter
                            : attacker.IsHeavyStriking ? BrawlerDefines.DamageKind.HeavyStrike : BrawlerDefines.DamageKind.Normal;
                        enemy.TakeMeleeDamage(damage, attacker, kind);
                    }
                    if (enemy.Health < healthBeforeHit)
                    {
                        didHit = true;
                    }
                    // 無敵・硬直・撃破済みで拒否された命中や空振りでは、反撃専用の演出を出しません。
                    if (attacker.IsCounterAttacking && enemy.Health < healthBeforeHit)
                    {
                        CreateDefenseImpact(enemy.transform.position + Vector3.up * BrawlerBalance.CounterImpactHeight,
                            BrawlerDefines.ImpactKind.Counter);
                    }
                }
            }
            // 全対象へ同じ技・範囲を適用してから通知し、途中の技変更で判定が変わることを防ぎます。
            if (didHit)
            {
                attacker.NotifyMeleeHit();
            }
        }
        else if (IsInAttackRange(attacker, _player))
        {
            _player.TakeDamage(damage, attacker.Facing);
        }
        else if (!IsRunFinished && _player.Health > 0 && attacker.Health > 0)
        {
            // 範囲外へ避けたときだけ空振り音を出します。
            // ガード・無敵・硬直でダメージが拒否された接触を空振りとして扱いません。
            PlaySound(attacker.EnemyMissSound);
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
            target.TryTakeGroundHit(attacker.CalculateMeleeDamage(damage), attacker.Facing);
        }
    }

    private static bool IsInGroundAttackRange(Fighter attacker, Fighter target)
    {
        Vector3 delta = target.transform.position - attacker.transform.position;
        // 追撃は通常攻撃より短いリーチで、向いている側の倒れた敵を狙います。
        return attacker.IsPlayer && attacker.Health > 0 && Mathf.Abs(delta.x) < attacker.GroundAttackReach
            && Mathf.Abs(delta.z) < BrawlerBalance.AttackLaneHalfWidth && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance;
    }

    // コライダーではなく、攻撃者と対象の位置の差から当たり判定を作ります。
    private bool IsInAttackRange(Fighter attacker, Fighter target)
    {
        Vector3 delta = target.transform.position - attacker.transform.position;
        // プレイヤーは職業設定、敵は派生クラスの宣言するリーチを使います。
        float attackReach = attacker.AttackReach;
        // 生存・奥行き差・左右距離・向きの 4 条件をすべて満たすと命中します。
        // facing は右 +1 / 左 -1。差に掛けると、左右どちらでも前方が正になります。
        // -0.25 の余裕を持たせ、ほぼ重なった相手にも攻撃が当たるようにしています。
        return target.Health > 0
            && Mathf.Abs(delta.z) < (attacker.IsPlayer ? BrawlerBalance.AttackLaneHalfWidth : attacker.MeleeLaneHalfWidth)
            && Mathf.Abs(delta.x) < attackReach
            && delta.x * attacker.Facing > -BrawlerBalance.AttackBehindAllowance;
    }

    // Unity の簡易 GUI。画面左上を原点とするピクセル座標で文字や HP バーを描きます。
    // 1 フレームに複数回呼ばれることがあるため、ゲーム進行はここで更新しません。
    private void OnGUI()
    {
        float margin = BrawlerBalance.ClassPanelMargin;
        // 職業ボタンは狭い画面でも収まる幅にそろえます。
        float width = Mathf.Min(BrawlerBalance.ClassButtonWidth,
            Mathf.Max(0, Screen.width - margin * 2) / BrawlerBalance.ClassSelectionCount);
        float height = BrawlerBalance.ClassButtonHeight;
        if (GUI.Button(new Rect(Screen.width - width * 5 - margin, margin, width, height), "CLERIC / F1"))
        {
            StartAs(BrawlerDefines.PlayerClass.Cleric);
        }
        if (GUI.Button(new Rect(Screen.width - width * 4 - margin, margin, width, height), "FIGHTER / F2"))
        {
            StartAs(BrawlerDefines.PlayerClass.Fighter);
        }
        if (GUI.Button(new Rect(Screen.width - width * 3 - margin, margin, width, height), "DWARF / F3"))
        {
            StartAs(BrawlerDefines.PlayerClass.Dwarf);
        }
        if (GUI.Button(new Rect(Screen.width - width * 2 - margin, margin, width, height), "THIEF / F4"))
        {
            StartAs(BrawlerDefines.PlayerClass.Thief);
        }
        if (GUI.Button(new Rect(Screen.width - width - margin, margin, width, height), "ELF / F5"))
        {
            StartAs(BrawlerDefines.PlayerClass.Elf);
        }
        _hud.Draw(this, _player, _enemies, _camera, _wave, _score, _hitChain, _healthBarFeedback,
            _pendingSpawnPositions, _spawnWarningRemaining, _pickupNoticeFont, _waveBanner, _hitDirectionIndicator);
    }
}
