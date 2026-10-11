using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity のバッチモードで戦闘ルールを確認するエディター専用ツールです。
// 実行例：-batchmode -nographics -executeMethod CombatValidation.Run
// シーンの保存は行わず、Play モード中のオブジェクトだけを変更します。
public static class CombatValidation
{
    private static double s_deadline;
    private static double s_readyAt;

    // Play 開始時のドメインリロードでは static 状態が初期化されます。
    // SessionState に実行中フラグを残し、リロード後に検証処理を再登録します。
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool("CombatValidation.Running", false))
        {
            return;
        }
        s_deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= Check;
        EditorApplication.update += Check;
        Application.logMessageReceived -= WatchErrors;
        Application.logMessageReceived += WatchErrors;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/NeonStreet.unity");
        SessionState.SetBool("CombatValidation.Running", true);
        s_deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Check;
        Application.logMessageReceived += WatchErrors;
        EditorApplication.isPlaying = true;
    }

    private static void Check()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > s_deadline)
            {
                throw new TimeoutException("Play モードの開始がタイムアウトしました。");
            }
            if (!EditorApplication.isPlaying)
            {
                return;
            }
            var game = UnityEngine.Object.FindAnyObjectByType<BeltBrawler>();
            if (game == null || game.Target == null)
            {
                return;
            }
            // 撃破検証で、後の追撃・射撃検証に使う遠距離型を倒してしまわないようにします。
            var enemy = Array.Find(UnityEngine.Object.FindObjectsByType<Fighter>(), f => !f.IsPlayer && !(f is RangedFighter));
            if (enemy == null)
            {
                return;
            }
            // Start だけでなく通常の Update も走らせ、初期化後の例外を検出します。
            if (s_readyAt == 0)
            {
                s_readyAt = EditorApplication.timeSinceStartup + 0.5;
                return;
            }
            if (EditorApplication.timeSinceStartup < s_readyAt)
            {
                return;
            }
            var player = game.Target;
            GamepadValidation.RunChecks(player);

            // 回避中の無敵と、回避終了後にダメージを受けることを両方確認します。
            SetPrivateField(player, "_dashRemaining", 0.2f);
            player.TakeDamage(10, 1);
            Require(player.Health == 100, "回避中はダメージを無効化");
            SetPrivateField(player, "_dashRemaining", 0f);
            player.TakeDamage(10, 1);
            Require(player.Health == 90, "回避終了後はダメージを受理");

            // 無敵で無効になった命中は、連続ヒットを水増ししないことを確認します。
            SetPrivateField(enemy, "_windupRemaining", 0.5f);
            enemy.TakeDamage(18, 1);
            Require(!enemy.IsWindingUp && game.HitChain == 1, "被ダメージで予告を中断し命中を加算");
            enemy.TakeDamage(18, 1);
            Require(game.HitChain == 1, "硬直中の重複ダメージを拒否");
            SetPrivateField(enemy, "_stunRemaining", 0f);
            enemy.TakeDamage(30, 1);
            Require(enemy.IsDowned, "強い攻撃で敵が転倒");
            Require(((Vector3)GetPrivateField(enemy, "_knockbackVelocity")).x == 9, "最終段の強い吹き飛ばし");
            SetPrivateField(player, "_stunRemaining", 0f);
            player.TakeDamage(10, 1);
            Require(game.HitChain == 0, "被ダメージで連続ヒットをリセット");

            // 公開フィールドへの直接代入を廃止した後も、回復量と上限が同じことを確認します。
            player.RestoreHealth(15);
            Require(player.Health == 95, "指定量の HP 回復");
            player.RestoreHealth(15);
            Require(player.Health == player.MaxHealth, "最大 HP を超えない回復");

            // ダメージ量が HP を超えても負にならず、一度だけ撃破ボーナスを加算します。
            SetPrivateField(enemy, "_stunRemaining", 0f);
            int previousScore = (int)GetPrivateField(game, "_score");
            SetPrivateField(game, "_defeatedEnemyCount", 2);
            enemy.TakeDamage(1000, 1);
            var potion = UnityEngine.Object.FindAnyObjectByType<HealingPotion>();
            Require(potion != null, "3体目の撃破でポーションを生成");
            Require(enemy.Health == 0 && (int)GetPrivateField(game, "_score") == previousScore + 110, "撃破時のスコアと HP 下限");
            enemy.TakeDamage(1000, 1);
            Require((int)GetPrivateField(game, "_score") == previousScore + 110, "撃破スコアの二重加算を防止");
            Require(UnityEngine.Object.FindObjectsByType<HealingPotion>().Length == 1, "同じ敵の撃破ではドロップを増やさない");

            // 低 FPS でも弾がすり抜けず、奥行き移動では射線から逃げられることを確認します。
            Require(EnemyProjectile.TouchesTarget(Vector3.left * 3, Vector3.right * 3, Vector3.zero), "高速の弾の通過を検出");
            Require(!EnemyProjectile.TouchesTarget(Vector3.left * 3, Vector3.right * 3, Vector3.forward), "射線の奥行きから離れて回避");
            var ranged = UnityEngine.Object.FindAnyObjectByType<RangedFighter>();
            Require(ranged != null, "初期ウェーブに遠距離型が出現");
            int healthBeforeShot = player.Health;
            typeof(RangedFighter).GetMethod("DealAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ranged, new object[] { 8 });
            Require(UnityEngine.Object.FindAnyObjectByType<EnemyProjectile>() != null
                && player.Health == healthBeforeShot, "遠距離攻撃は発射時に即時ダメージを与えない");

            // 回復魔法の回数制限・上限・行動制限を、実際のプレイヤーで確認します。
            var cleric = player as ClericFighter;
            Require(cleric != null, "プレイヤーはクレリックを使用");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(!cleric.TryHeal() && cleric.HealingUsesRemaining == 3, "満タンでは回数を消費しない");
            player.TakeDamage(45, player.Facing);
            Require(!cleric.TryHeal(), "被ダメージ硬直中は回復できない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(cleric.TryHeal() && player.Health == 85 && cleric.HealingUsesRemaining == 2, "HP を30回復し1回消費");
            Require(!cleric.TryHeal(), "回復後の隙では再使用不可");
            player.TakeDamage(5, player.Facing);
            Require(player.Health == 80, "回復中も無敵にはならない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            Require(cleric.TryHeal() && player.Health == 100, "回復は最大HPで止まる");
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            player.TakeDamage(40, player.Facing);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(cleric.TryHeal() && cleric.HealingUsesRemaining == 0, "3回目で残り0回");
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            Require(!cleric.TryHeal(), "回数を使い切ると発動できない");

            // 回復の使用回数を使い切っても、補助魔法は独立した回数で使えます。
            Require(cleric.TryProtection() && cleric.ProtectionUsesRemaining == 1, "補助魔法は回復と独立した回数");
            Require(!cleric.TryProtection() && cleric.ProtectionUsesRemaining == 1, "効果中の再使用は消費しない");
            int beforeProtectionHit = player.Health;
            player.TakeDamage(9, player.Facing);
            Require(player.Health == beforeProtectionHit - 5, "奇数ダメージは半減して切り上げ");
            SetPrivateField(player, "_stunRemaining", 0f);
            player.TakeDamage(8, player.Facing);
            Require(player.Health == beforeProtectionHit - 9, "弾の8ダメージも4に軽減");
            SetPrivateField(player, "_stunRemaining", 0f);
            // エディターの検証コールバックでは deltaTime が0の場合があるため、経過時間を明示します。
            typeof(ClericFighter).GetMethod("UpdateProtection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cleric, new object[] { 8.1f });
            Require(!cleric.IsProtected, "通常更新で効果時間が終了");
            int beforeExpiredHit = player.Health;
            player.TakeDamage(8, player.Facing);
            Require(player.Health == beforeExpiredHit - 8, "効果終了後は通常ダメージ");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            Require(cleric.TryProtection() && cleric.ProtectionUsesRemaining == 0, "2回目で補助魔法を使い切る");
            SetPrivateField(cleric, "_protectionRemaining", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            Require(!cleric.TryProtection(), "回数0では補助魔法を使用できない");

            player.RestoreHealth(100);
            potion.transform.position = player.transform.position;
            Require(!potion.TryCollect(), "満タンではポーションを残す");
            player.TakeDamage(35, player.Facing);
            potion.transform.position = player.transform.position + Vector3.forward;
            Require(!potion.TryCollect(), "奥行きが離れたポーションを拾わない");
            potion.transform.position = player.transform.position;
            Require(potion.TryCollect() && player.Health == 85, "近づくとHPを20回復");
            Require(!potion.TryCollect() && player.Health == 85, "同フレームの二重取得を防止");
            Require(cleric.HealingUsesRemaining == 0, "ポーションは魔法の回数を変更しない");

            // 第2ウェーブを予約し、予告中に敵が出ず、予告位置から出現することを確認します。
            player.transform.position = Vector3.zero;
            typeof(BeltBrawler).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            var pending = (System.Collections.Generic.List<Vector3>)GetPrivateField(game, "_pendingSpawnPositions");
            Require(pending.Exists(p => p.x < 0) && pending.Exists(p => p.x > 0), "第2ウェーブは左右に出現を予約");
            Vector3 reservedPosition = pending[0];
            int beforeSpawn = UnityEngine.Object.FindObjectsByType<Fighter>().Length;
            MethodInfo updateWarning = typeof(BeltBrawler).GetMethod("UpdateSpawnWarning", BindingFlags.Instance | BindingFlags.NonPublic);
            updateWarning.Invoke(game, new object[] { 0.5f });
            Require(UnityEngine.Object.FindObjectsByType<Fighter>().Length == beforeSpawn, "予告中には出現しない");
            player.transform.position = Vector3.left * 5;
            updateWarning.Invoke(game, new object[] { 1.1f });
            Require(pending.Count == 0 && UnityEngine.Object.FindObjectsByType<Fighter>().Length == beforeSpawn + 4,
                "予告終了で4体を一度だけ生成");
            Require(Array.Exists(UnityEngine.Object.FindObjectsByType<Fighter>(), f => !f.IsPlayer && f.transform.position == reservedPosition),
                "移動後も予約した位置に出現");
            updateWarning.Invoke(game, new object[] { 2f });
            Require(UnityEngine.Object.FindObjectsByType<Fighter>().Length == beforeSpawn + 4, "予告終了後は重複生成しない");
            MethodInfo calculatePosition = typeof(BeltBrawler).GetMethod("CalculateSpawnPosition", BindingFlags.Static | BindingFlags.NonPublic);
            Vector3 edgeSpawn = (Vector3)calculatePosition.Invoke(null, new object[] { Vector3.right * 22, 0, 2 });
            Require(edgeSpawn.x <= 18 && edgeSpawn.x >= -22, "ステージ端で近すぎる出現を避ける");

            // 実時間で停止が解除され、複数命中やリスタートで停止が残らないことを検証します。
            MethodInfo updateStop = typeof(BeltBrawler).GetMethod("UpdateHitStop", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo beginStop = typeof(BeltBrawler).GetMethod("BeginHitStop", BindingFlags.Instance | BindingFlags.NonPublic);
            updateStop.Invoke(game, new object[] { 1f });
            Require(Time.timeScale == 1, "実時間が経過するとヒットストップ解除");
            Require(UnityEngine.Object.FindAnyObjectByType<HitImpact>() != null, "受理された命中で打撃エフェクトを生成");
            beginStop.Invoke(game, new object[] { 0.04f });
            Require(Time.timeScale == 0, "命中時にゲーム時間を停止");
            beginStop.Invoke(game, new object[] { 0.08f });
            beginStop.Invoke(game, new object[] { 0.04f });
            Require((float)GetPrivateField(game, "_hitStopRemaining") == 0.08f, "同時命中の停止時間を加算しない");
            updateStop.Invoke(game, new object[] { 0.09f });
            Require(Time.timeScale == 1, "強い命中後も停止を解除");
            beginStop.Invoke(game, new object[] { 0.08f });
            game.enabled = false;
            Require(Time.timeScale == 1, "停止中の無効化でゲーム時間を復元");
            game.enabled = true;

            // バッチモードでは聴感は確認できないため、生成された音声の有効性を検証します。
            var audio = UnityEngine.Object.FindAnyObjectByType<BrawlerAudio>();
            Require(audio != null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null, "音声再生役とリスナーを生成");
            var clips = (System.Collections.Generic.Dictionary<BrawlerSound, AudioClip>)GetPrivateField(audio, "_clips");
            Require(clips.Count == 6, "6種類の効果音を生成");
            foreach (AudioClip clip in clips.Values)
            {
                var samples = new float[clip.samples];
                Require(clip.GetData(samples, 0), "音声サンプルを読み取れる");
                Require(Array.Exists(samples, sample => Mathf.Abs(sample) > 0.01f), "効果音が無音ではない");
                Require(Array.TrueForAll(samples, sample => !float.IsNaN(sample) && !float.IsInfinity(sample) && Mathf.Abs(sample) <= 1),
                    "音声に非数や範囲外の振幅がない");
            }
            var source = (AudioSource)GetPrivateField(audio, "_source");
            Require(source.spatialBlend == 0 && source.ignoreListenerPause, "2D音声として停止から独立して再生");

            // 専用追撃は通常の入力受付から開始し、ダウン中の硬直を越えて一度だけ命中します。
            updateStop.Invoke(game, new object[] { 1f });
            player.transform.position = Vector3.zero;
            typeof(Fighter).GetProperty("Facing").SetValue(player, 1f);
            ranged.transform.position = Vector3.right;
            ranged.RestoreHealth(100);
            SetPrivateField(ranged, "_stunRemaining", 0f);
            SetPrivateField(ranged, "_downRemaining", 0f);
            ranged.TakeDamage(30, 1);
            int downedHealth = ranged.Health;
            ranged.TakeDamage(18, 1);
            Require(ranged.Health == downedHealth, "通常攻撃はダウン中の硬直無敵を無視しない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(player, "_attackBufferRemaining", 0.2f);
            typeof(Fighter).GetMethod("GetMovement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { Vector3.zero, false });
            Require(player.IsGroundAttacking, "攻撃受付から近くのダウン敵への追撃を開始");
            float beforeGroundDownTime = (float)GetPrivateField(ranged, "_downRemaining");
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { 0.13f });
            Require(ranged.Health == downedHealth - 24 && !ranged.CanReceiveGroundHit, "追撃は24ダメージで一度だけ命中");
            Require((float)GetPrivateField(ranged, "_downRemaining") == beforeGroundDownTime, "追撃で起き上がりを延長しない");
            Require(!ranged.TryTakeGroundHit(24, 1), "同じダウン中の二度目の追撃を拒否");

            ranged.RestoreHealth(100);
            SetPrivateField(ranged, "_stunRemaining", 0f);
            SetPrivateField(ranged, "_downRemaining", 0f);
            ranged.TakeDamage(30, 1);
            Require(ranged.CanReceiveGroundHit, "再ダウンでは追撃を再受付");
            ranged.transform.position = new Vector3(1, 0, 1);
            int beforeMiss = ranged.Health;
            game.HitGroundTarget(player, ranged, 24);
            Require(ranged.Health == beforeMiss, "奥行きが離れた追撃は空振り");
            ranged.transform.position = Vector3.right;
            SetPrivateField(ranged, "_downRemaining", 0f);
            game.HitGroundTarget(player, ranged, 24);
            Require(ranged.Health == beforeMiss, "起き上がった相手への追撃は空振り");
            player.TakeDamage(5, player.Facing);
            Require(!player.IsGroundAttacking, "被ダメージで追撃を中断");
            updateStop.Invoke(game, new object[] { 1f });

            var heavy = UnityEngine.Object.FindAnyObjectByType<HeavyFighter>();
            Require(heavy != null && heavy.MaxHealth == 116, "第2ウェーブにHP116の重装型を生成");
            player.RestoreHealth(100);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_dashRemaining", 0f);
            SetPrivateField(cleric, "_protectionRemaining", 0f);
            player.transform.position = Vector3.zero;
            heavy.transform.position = Vector3.right;
            typeof(Fighter).GetMethod("GetEnemyMovement", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(heavy, null);
            Require(heavy.IsWindingUp && (float)GetPrivateField(heavy, "_windupRemaining") == 0.95f, "重装型は長い予告を開始");
            typeof(Fighter).GetMethod("UpdateAttackWindup", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(heavy, new object[] { 0.96f });
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(heavy, new object[] { 0.15f });
            Require(player.Health == 76, "重装型の実際の攻撃は24ダメージ");
            heavy.transform.position = Vector3.zero;
            typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(heavy, new object[] { Vector3.right, 1f });
            Require(Mathf.Approximately(heavy.transform.position.x, 1.3f), "重装型の移動速度は1.3");
            SetPrivateField(heavy, "_windupRemaining", 0.95f);
            heavy.TakeDamage(18, 1);
            Require(!heavy.IsWindingUp, "重装型の予告も攻撃で中断");
            SetPrivateField(heavy, "_stunRemaining", 0f);
            heavy.TakeDamage(30, 1);
            Require(heavy.CanReceiveGroundHit && heavy.TryTakeGroundHit(24, 1), "重装型も転倒と追撃が有効");
            updateStop.Invoke(game, new object[] { 1f });

            // 床の位置を動かさずジャンプし、着地後は再び地上攻撃を受けることを確認します。
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            MethodInfo startJump = typeof(Fighter).GetMethod("StartJump", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updateJump = typeof(Fighter).GetMethod("UpdateJump", BindingFlags.Instance | BindingFlags.NonPublic);
            startJump.Invoke(player, new object[] { Vector3.zero });
            updateJump.Invoke(player, new object[] { 0.12f });
            Require(player.IsAirborne && player.JumpHeight > 0.6f && player.transform.position.y == 0, "ジャンプの高さを床位置と分離");
            int airborneHealth = player.Health;
            player.TakeDamage(10, 1);
            Require(player.Health == airborneHealth, "十分な高さでは地上攻撃を回避");
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            heavy.transform.position = Vector3.right;
            int beforeAirHit = heavy.Health;
            typeof(Fighter).GetMethod("StartAirAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { false });
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.15f });
            Require(player.IsAirAttacking && heavy.Health == beforeAirHit - 20, "空中攻撃が20ダメージで命中");
            updateJump.Invoke(player, new object[] { 1f });
            Require(!player.IsAirborne && player.JumpHeight == 0 && !player.IsAirAttacking, "着地で空中状態を解除");
            player.TakeDamage(10, 1);
            Require(player.Health == airborneHealth - 10, "着地後は地上攻撃を受ける");
            startJump.Invoke(player, new object[] { Vector3.forward });
            Require((float)GetPrivateField(player, "_jumpVelocity") == 9, "上入力は大ジャンプ");
            updateJump.Invoke(player, new object[] { 1f });
            SetPrivateField(player, "_dashRemaining", 0.2f);
            SetPrivateField(player, "_dashDirection", Vector3.right);
            startJump.Invoke(player, new object[] { Vector3.zero });
            Require((Vector3)GetPrivateField(player, "_jumpDrift") == Vector3.right * 4
                && (float)GetPrivateField(player, "_dashRemaining") == 0, "ダッシュジャンプは慣性を残して回避無敵を終了");
            updateJump.Invoke(player, new object[] { 0.2f });
            typeof(Fighter).GetMethod("StartAirAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { true });
            Require((float)GetPrivateField(player, "_jumpVelocity") < 0, "下突きで降下を開始");
            updateJump.Invoke(player, new object[] { 1f });
            updateStop.Invoke(game, new object[] { 1f });

            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_dashRemaining", 0f);
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            player.transform.position = Vector3.zero;
            heavy.transform.position = Vector3.right;
            int beforeCrouchHit = heavy.Health;
            typeof(Fighter).GetMethod("StartCrouchAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { 0.1f });
            Require(heavy.Health == beforeCrouchHit - 12, "しゃがみ攻撃は12ダメージ");
            updateStop.Invoke(game, new object[] { 1f });

            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_dashRemaining", 0f);
            SetPrivateField(player, "_isCrouching", false);
            MethodInfo runInput = typeof(Fighter).GetMethod("UpdateRunningInput", BindingFlags.Instance | BindingFlags.NonPublic);
            runInput.Invoke(player, new object[] { Vector3.zero });
            SetPrivateField(player, "_lastForwardTapAt", -10f);
            runInput.Invoke(player, new object[] { Vector3.right });
            Require(!player.IsRunning, "前方向1回では走らない");
            runInput.Invoke(player, new object[] { Vector3.zero });
            runInput.Invoke(player, new object[] { Vector3.right });
            Require(player.IsRunning, "前方向2回で継続ダッシュ");
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            heavy.transform.position = player.transform.position + Vector3.right * 0.5f;
            int beforeBodyCheck = heavy.Health;
            game.HitRunningTargets(player);
            Require(heavy.Health == beforeBodyCheck - 8, "継続ダッシュ中の接触で8ダメージ");
            SetPrivateField(heavy, "_stunRemaining", 0f);
            game.HitRunningTargets(player);
            Require(heavy.Health == beforeBodyCheck - 8, "同じダッシュでは同じ敵に二重命中しない");
            runInput.Invoke(player, new object[] { Vector3.zero });
            Require(!player.IsRunning, "方向を離すと継続ダッシュ解除");
            runInput.Invoke(player, new object[] { Vector3.right });
            runInput.Invoke(player, new object[] { Vector3.zero });
            runInput.Invoke(player, new object[] { Vector3.right });
            SetPrivateField(heavy, "_stunRemaining", 0f);
            game.HitRunningTargets(player);
            Require(heavy.Health == beforeBodyCheck - 16, "新しいダッシュでは体当たりを再受付");
            int beforeRunningDamage = player.Health;
            player.TakeDamage(5, -player.Facing);
            Require(!player.IsRunning && player.Health == beforeRunningDamage - 5, "継続ダッシュは無敵ではなく被ダメージで中断");
            updateStop.Invoke(game, new object[] { 1f });

            var commands = new DirectionCommandBuffer();
            commands.Record(Vector3.back, 1, 0);
            commands.Record(new Vector3(1, 0, -1), 1, 0.1f);
            Require(commands.TryConsumeSlide(new Vector3(1, 0, -1), 0.15f, out float slideFacing) && slideFacing == 1,
                "下から斜め前下で右向きコマンド成立");
            Require(!commands.TryConsumeSlide(new Vector3(1, 0, -1), 0.16f, out _), "コマンドの二重消費を防止");
            commands.Record(Vector3.back, -1, 1);
            commands.Record(new Vector3(-1, 0, -1), -1, 1.1f);
            Require(commands.TryConsumeSlide(new Vector3(-1, 0, -1), 1.15f, out slideFacing) && slideFacing == -1,
                "左向きでは方向コマンドを反転");
            commands.Record(Vector3.back, 1, 2);
            commands.Record(new Vector3(1, 0, -1), 1, 2.95f);
            Require(!commands.TryConsumeSlide(new Vector3(1, 0, -1), 2.96f, out _), "遅い入力は不成立");
            commands.Record(Vector3.back, 1, 3);
            commands.Record(new Vector3(0.32f, 0, -0.8f), 1, 3.2f);
            commands.Record(new Vector3(0.45f, 0, -0.6f), 1, 3.8f);
            Require(commands.TryConsumeSlide(new Vector3(0.45f, 0, -0.6f), 4.35f, out _),
                "浅い斜め入力への移行とゆっくりしたジャンプ入力を受理");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackRemaining", 0f);
            player.transform.position = Vector3.zero;
            typeof(Fighter).GetMethod("StartSlide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 1f });
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            heavy.transform.position = Vector3.right * 0.5f;
            int beforeSlideHit = heavy.Health;
            game.HitSlidingTargets(player);
            Require(heavy.Health == beforeSlideHit - 12, "スライディング接触で12ダメージ");
            SetPrivateField(heavy, "_stunRemaining", 0f);
            game.HitSlidingTargets(player);
            Require(heavy.Health == beforeSlideHit - 12, "同じ滑りでは二重命中しない");
            typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { Vector3.zero, 1f });
            Require(!player.IsSliding && Mathf.Approximately(player.transform.position.x, 0.96f), "短押しの移動は0.12秒分で制限");
            updateStop.Invoke(game, new object[] { 1f });

            Debug.Log("COMBAT_VALIDATION_PASSED");
            Finish(0);
        }
        catch (Exception error)
        {
            // 検証ランナーの最上位では全例外を失敗として記録し、終了コードに変換します。
            // ゲーム本体の例外を握りつぶして続行するための catch ではありません。
            Debug.LogException(error);
            Finish(1);
        }
    }

    // テスト用に内部状態を設定します。ゲーム側の公開 API を増やさず確認するための仕組みです。
    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = FindPrivateField(target, fieldName);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindPrivateField(target, fieldName);
        field.SetValue(target, value);
    }

    private static FieldInfo FindPrivateField(object target, string fieldName)
    {
        // 派生した敵でも、基底クラスが保持する private 状態を検証できるようにします。
        FieldInfo field = null;
        for (Type type = target.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        }
        if (field == null)
        {
            throw new InvalidOperationException($"検証対象のフィールドが見つかりません：{fieldName}");
        }

        return field;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("確認失敗：" + message);
        }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool("CombatValidation.Running", false);
        EditorApplication.update -= Check;
        Application.logMessageReceived -= WatchErrors;
        EditorApplication.Exit(code);
    }

    // アサーション以外の Unity 実行時エラーも、検証失敗として扱います。
    private static void WatchErrors(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
        {
            return;
        }
        // この環境では Unity エディターの検索 DB が起動時に例外を出すことがあります。
        // ゲームコードを含まない、この既知のエディター検索エラーだけは検証対象外にします。
        if (trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("BeltBrawler") && !trace.Contains("Fighter"))
        {
            return;
        }
        // ログ出力の途中で終了すると原因が記録されないため、次のエディター更新で終了します。
        Application.logMessageReceived -= WatchErrors;
        Debug.LogWarning("COMBAT_VALIDATION_FAILED: " + message + "\n" + trace);
        EditorApplication.update -= Check;
        EditorApplication.delayCall += () => Finish(1);
    }
}
