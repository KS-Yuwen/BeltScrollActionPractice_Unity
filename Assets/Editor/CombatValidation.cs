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
            Require(!cleric.TryUseSelectedAction() && cleric.HealingUsesRemaining == 3, "選択使用でも満タンでは回数を消費しない");
            player.TakeDamage(45, player.Facing);
            Require(!cleric.TryHeal(), "被ダメージ硬直中は回復できない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(cleric.TryUseSelectedAction() && player.Health == 85 && cleric.HealingUsesRemaining == 2, "選択した回復でHP を30回復し1回消費");
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
            Require(!cleric.TryUseSelectedAction(), "選択使用でも回数を使い切ると発動できない");

            // 回復の使用回数を使い切っても、補助魔法は独立した回数で使えます。
            cleric.SelectNextAction();
            Require(cleric.SelectedActionUsesRemaining == BrawlerBalance.ProtectionUses, "選択表示は補助魔法の独立した残り回数");
            Require(cleric.TryUseSelectedAction() && cleric.ProtectionUsesRemaining == 1, "選択した補助魔法は回復と独立した回数");
            Require(!cleric.TryProtection() && cleric.ProtectionUsesRemaining == 1, "効果中の再使用は消費しない");
            int beforeProtectionHit = player.Health;
            player.TakeDamage(9, player.Facing);
            Require(player.Health == beforeProtectionHit - 5, "奇数ダメージは半減して切り上げ");
            SetPrivateField(player, "_stunRemaining", 0f);
            player.TakeDamage(8, player.Facing);
            Require(player.Health == beforeProtectionHit - 9, "弾の8ダメージも4に軽減");
            SetPrivateField(player, "_stunRemaining", 0f);
            // エディターの検証コールバックでは deltaTime が0の場合があるため、経過時間を明示します。
            SetPrivateField(cleric, "_protectionRemaining", BrawlerBalance.ProtectionWarningSeconds + 1);
            Require(!cleric.IsProtectionExpiring, "終了予告のしきい値より前は警告しない");
            SetPrivateField(cleric, "_protectionRemaining", BrawlerBalance.ProtectionWarningSeconds);
            Require(cleric.IsProtectionExpiring, "残り時間がしきい値に達すると終了予告");
            int warningProtectionUses = cleric.ProtectionUsesRemaining;
            typeof(ClericFighter).GetMethod("UpdateProtection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cleric, new object[] { 0f });
            Require(cleric.IsProtectionExpiring && cleric.ProtectionUsesRemaining == warningProtectionUses,
                "時間停止中も終了予告を維持し、使用回数に影響しない");
            Require((int)typeof(ClericFighter).GetMethod("CalculateReceivedDamage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cleric, new object[] { 9 }) == 5, "終了予告中もダメージ軽減が続く");
            typeof(ClericFighter).GetMethod("UpdateProtection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cleric, new object[] { 8.1f });
            Require(!cleric.IsProtectionExpiring, "効果終了後は終了予告も解除");
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
            cleric.SelectNextAction();

            player.RestoreHealth(100);
            Require(cleric.SelectedAction == BrawlerDefines.UsableAction.HealingPotion, "補助魔法の次はポーションを選択");
            potion.transform.position = player.transform.position + Vector3.forward;
            Require(!potion.TryCollect(), "奥行きが離れたポーションを拾わない");
            for (int held = 0; held < BrawlerBalance.PotionCapacity; held++)
            {
                Require(player.TryStoreHealingPotion(), "所持上限まで保存可能");
            }
            potion.transform.position = player.transform.position;
            int potionNoticesBefore = UnityEngine.Object.FindObjectsByType<PickupNotice>().Length;
            Require(!potion.TryCollect() && !player.TryStoreHealingPotion(), "所持上限では床に残し、超過しない");
            Require(UnityEngine.Object.FindObjectsByType<PickupNotice>().Length == potionNoticesBefore, "所持上限で取得通知を出さない");
            typeof(Fighter).GetProperty("HealingPotionsHeld").SetValue(player, 0);
            Require(potion.TryCollect() && player.Health == 100 && player.HealingPotionsHeld == 1,
                "HP満タンでも所持品へ入り、拾得時には回復しない");
            Require(!potion.TryCollect() && player.HealingPotionsHeld == 1, "同フレームの二重取得を防止");
            Require(UnityEngine.Object.FindObjectsByType<PickupNotice>().Length == potionNoticesBefore + 1
                && Array.Exists(UnityEngine.Object.FindObjectsByType<PickupNotice>(), n => n.Kind == BrawlerDefines.PickupKind.HealingPotion
                    && n.DisplayText == "ポーション＋1"), "ポーション取得成功時だけ通知を一つ生成");
            Require(!cleric.TryUseSelectedAction() && player.HealingPotionsHeld == 1, "満タンでは使用しても消費しない");
            player.TakeDamage(35, player.Facing);
            Require(!cleric.TryUseSelectedAction() && player.HealingPotionsHeld == 1, "被ダメージ硬直中は消費しない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(cleric.TryUseSelectedAction() && player.Health == 85 && player.HealingPotionsHeld == 0,
                "選択使用でHPを20回復し、所持数だけを一つ消費");
            Require(!cleric.TryUseSelectedAction(), "使用直後は再使用できない");
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            Require(!cleric.TryUseSelectedAction() && player.Health == 85, "所持数ゼロでは回復しない");
            Require(cleric.HealingUsesRemaining == 0, "ポーションは魔法の回数を変更しない");
            cleric.SelectNextAction();
            Require(cleric.SelectedAction == BrawlerDefines.UsableAction.ThrowingKnife, "ポーションの次は投げナイフ");
            cleric.SelectNextAction();

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
            Require(commands.TryConsumeDownForward(new Vector3(1, 0, -1), 0.15f, out float slideFacing) && slideFacing == 1,
                "下から斜め前下で右向きコマンド成立");
            Require(!commands.TryConsumeDownForward(new Vector3(1, 0, -1), 0.16f, out _), "コマンドの二重消費を防止");
            commands.Record(Vector3.back, -1, 1);
            commands.Record(new Vector3(-1, 0, -1), -1, 1.1f);
            Require(commands.TryConsumeDownForward(new Vector3(-1, 0, -1), 1.15f, out slideFacing) && slideFacing == -1,
                "左向きでは方向コマンドを反転");
            commands.Record(Vector3.back, 1, 2);
            commands.Record(new Vector3(1, 0, -1), 1, 2.95f);
            Require(!commands.TryConsumeDownForward(new Vector3(1, 0, -1), 2.96f, out _), "遅い入力は不成立");
            commands.Record(Vector3.back, 1, 3);
            commands.Record(new Vector3(0.32f, 0, -0.8f), 1, 3.2f);
            commands.Record(new Vector3(0.45f, 0, -0.6f), 1, 3.8f);
            Require(commands.TryConsumeDownForward(new Vector3(0.45f, 0, -0.6f), 4.35f, out _),
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

            // CSVは部分指定も許可しますが、不正な値があれば全体を既定値へ戻します。
            Require(SlideBalanceSettings.TryParse("key,value\nSlideSpeed,10\nSlideDamage,22\nSlideMinSeconds,0.2\n", out SlideBalanceSettings customSlide, out _),
                "CSVから有効なスライディング設定を読み込み");
            Require(customSlide.Speed == 10 && customSlide.Damage == 22 && customSlide.DirectionWindowSeconds == 0.9f,
                "指定値を適用し省略項目には既定値を使用");
            string[] invalidCsvs = {
                "", "key,value\nSlideSpeed,NaN", "key,value\nSlideSpeed,-1",
                "key,value\nSlideDamage,2.5", "key,value\nUnknown,1",
                "key,value\nSlideSpeed,10\nSlideSpeed,12", "key,value\nSlideMinSeconds,1\nSlideMaxSeconds,0.4"
            };
            foreach (string invalidCsv in invalidCsvs)
            {
                Require(!SlideBalanceSettings.TryParse(invalidCsv, out SlideBalanceSettings fallback, out string error)
                    && fallback.Speed == BrawlerBalance.DefaultSlideSpeed && !string.IsNullOrEmpty(error),
                    "不正なCSVは理由を返して既定値へフォールバック");
            }
            // 読み込みだけでなく、実際の移動・ダメージも設定窓口経由で変わることを確認します。
            FieldInfo settingsField = typeof(BrawlerBalance).GetField("s_slideSettings", BindingFlags.Static | BindingFlags.NonPublic);
            object originalSettings = settingsField.GetValue(null);
            try
            {
                settingsField.SetValue(null, customSlide);
                SetPrivateField(player, "_stunRemaining", 0f);
                player.transform.position = Vector3.zero;
                typeof(Fighter).GetMethod("StartSlide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 1f });
                heavy.RestoreHealth(200);
                SetPrivateField(heavy, "_stunRemaining", 0f);
                heavy.transform.position = Vector3.right * 0.5f;
                int csvHitHealth = heavy.Health;
                game.HitSlidingTargets(player);
                Require(heavy.Health == csvHitHealth - 22, "CSVのダメージが実際の接触に反映");
                typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { Vector3.zero, 1f });
                Require(Mathf.Approximately(player.transform.position.x, 2), "CSVの速度と最短時間が実際の移動に反映");
            }
            finally
            {
                settingsField.SetValue(null, originalSettings);
            }
            updateStop.Invoke(game, new object[] { 1f });

            // 空中攻撃中に逆方向へ移動しても、攻撃とモデルの向きを反転しません。
            MethodInfo moveFacingCheck = typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rangeFacingCheck = typeof(BeltBrawler).GetMethod("IsInAttackRange", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float expectedFacing in new float[] { -1, 1 })
            {
                player.transform.position = Vector3.zero;
                typeof(Fighter).GetProperty("Facing").SetValue(player, expectedFacing);
                SetPrivateField(player, "_attackRemaining", BrawlerBalance.AttackSeconds);
                moveFacingCheck.Invoke(player, new object[] { new Vector3(-expectedFacing, 0, 0), 0.01f });
                Require(player.Facing == expectedFacing, "攻撃中の逆方向移動でも向きを固定");
                heavy.RestoreHealth(200);
                heavy.transform.position = player.transform.position + Vector3.right * expectedFacing;
                Require((bool)rangeFacingCheck.Invoke(game, new object[] { player, heavy }), "左右の前方命中判定が正しい");
                heavy.transform.position = player.transform.position - Vector3.right * expectedFacing;
                Require(!(bool)rangeFacingCheck.Invoke(game, new object[] { player, heavy }), "左右の背後命中を拒否");
            }
            updateStop.Invoke(game, new object[] { 1f });

            player.transform.position = Vector3.zero;
            heavy.transform.position = Vector3.right;
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            player.RestoreHealth(100);
            typeof(Fighter).GetMethod("StartDashSpecial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 1f });
            int specialHitHealth = heavy.Health;
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.11f });
            Require(heavy.Health == specialHitHealth - 40 && heavy.IsDowned, "必殺技は40ダメージで転倒を誘発");
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.01f });
            Require(heavy.Health == specialHitHealth - 40, "必殺技の命中は1回だけ");
            player.TakeDamage(5, -player.Facing);
            Require(player.Health == 95 && !player.IsDashSpecialAttacking
                && (float)GetPrivateField(player, "_dashAttackMovementRemaining") == 0,
                "必殺技は無敵でなく被ダメージで踏み込みも中断");
            Require(SlideBalanceSettings.TryParse("key,value\nDashSpecialDamage,55\nDashSpecialSpeed,12", out SlideBalanceSettings specialSettings, out _)
                && specialSettings.DashSpecialDamage == 55 && specialSettings.DashSpecialSpeed == 12, "CSVから必殺技の調整値を読み込み");
            Require(!SlideBalanceSettings.TryParse("key,value\nDashSpecialHitDelaySeconds,1", out _, out _), "攻撃終了後の命中時刻を拒否");
            updateStop.Invoke(game, new object[] { 1f });

            var uppercutCommands = new DirectionCommandBuffer();
            uppercutCommands.Record(Vector3.back, 1, 0);
            uppercutCommands.Record(Vector3.zero, 1, 0.2f);
            uppercutCommands.Record(Vector3.forward, 1, 0.8f);
            Require(uppercutCommands.TryConsumeDownUp(Vector3.forward, 1.3f), "対空コマンドはニュートラル経由と遅めの入力を許可");
            Require(!uppercutCommands.TryConsumeDownUp(Vector3.forward, 1.31f), "対空コマンドを二重消費しない");
            uppercutCommands.Record(Vector3.back, 1, 2);
            uppercutCommands.Record(Vector3.forward, 1, 3);
            Require(!uppercutCommands.TryConsumeDownUp(Vector3.forward, 3.1f), "期限を超えた対空コマンドを拒否");
            player.transform.position = Vector3.zero;
            heavy.transform.position = Vector3.left;
            typeof(Fighter).GetProperty("Facing").SetValue(player, -1f);
            player.RestoreHealth(100);
            SetPrivateField(player, "_stunRemaining", 0f);
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            typeof(Fighter).GetMethod("StartUppercut", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            int uppercutHitHealth = heavy.Health;
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.11f });
            Require(heavy.Health == uppercutHitHealth - 36 && heavy.IsDowned, "対空技は36ダメージで転倒");
            player.TakeDamage(5, 1);
            Require(player.Health == 95 && !player.IsUppercutAttacking, "対空技も被ダメージで中断");
            Require(SlideBalanceSettings.TryParse("key,value\nUppercutDamage,48\nUppercutReach,2.5", out SlideBalanceSettings uppercutSettings, out _)
                && uppercutSettings.UppercutDamage == 48 && uppercutSettings.UppercutReach == 2.5f, "対空技をCSVで調整可能");
            Require(!SlideBalanceSettings.TryParse("key,value\nUppercutHitDelaySeconds,1", out _, out _), "対空技の不正な命中時刻を拒否");
            updateStop.Invoke(game, new object[] { 1f });

            player.transform.position = Vector3.zero;
            heavy.transform.position = Vector3.right;
            typeof(Fighter).GetProperty("Facing").SetValue(player, 1f);
            player.RestoreHealth(100);
            SetPrivateField(player, "_stunRemaining", 0f);
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            typeof(Fighter).GetMethod("StartHeavyStrike", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            int strikeHealth = heavy.Health;
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.19f });
            Require(heavy.Health == strikeHealth - 34 && heavy.IsDowned, "大斬りは34ダメージで転倒");
            MethodInfo holdAttack = typeof(Fighter).GetMethod("UpdateAttackHold", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(!(bool)holdAttack.Invoke(player, new object[] { 0.5f, true }), "長押しでも攻撃中は突き飛ばしを待つ");
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(player, "_attackHoldFacing", 1f);
            Require((bool)holdAttack.Invoke(player, new object[] { 0.01f, true }) && player.IsPushAttacking,
                "長押しが後隙終了時に突き飛ばしへ変化");
            heavy.RestoreHealth(200);
            SetPrivateField(heavy, "_stunRemaining", 0f);
            SetPrivateField(heavy, "_downRemaining", 0f);
            int pushHealth = heavy.Health;
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 0.11f });
            Require(heavy.Health == pushHealth - 10 && ((Vector3)GetPrivateField(heavy, "_knockbackVelocity")).x == 12,
                "突き飛ばしは10ダメージと強い押し戻し");
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(!(bool)holdAttack.Invoke(player, new object[] { 1f, true }), "長押し継続だけで二度目を出さない");
            Require(SlideBalanceSettings.TryParse("key,value\nHeavyStrikeDamage,45\nPushHoldSeconds,0.6\nPushKnockbackSpeed,15",
                out SlideBalanceSettings strikeSettings, out _) && strikeSettings.HeavyStrikeDamage == 45
                && strikeSettings.PushHoldSeconds == 0.6f && strikeSettings.PushKnockbackSpeed == 15, "大斬りと突き飛ばしのCSV調整");
            Require(!SlideBalanceSettings.TryParse("key,value\nPushHitDelaySeconds,1", out _, out _), "突き飛ばしの不正な命中時刻を拒否");
            updateStop.Invoke(game, new object[] { 1f });

            // 属性と範囲を独立に確認します。正面・背後・範囲外・通常敵を同時に配置します。
            var turnEnemies = (System.Collections.Generic.List<Fighter>)GetPrivateField(game, "_enemies");
            foreach (Fighter turnEnemy in turnEnemies)
            {
                if (turnEnemy != null)
                {
                    turnEnemy.transform.position = new Vector3(20, 0, 3);
                }
            }
            Require(turnEnemies.Exists(f => f != null && f.IsUndead), "ウェーブにアンデッドを配置");
            MethodInfo createTurnEnemy = typeof(BeltBrawler).GetMethod("CreateFighter", BindingFlags.Instance | BindingFlags.NonPublic);
            player.transform.position = Vector3.zero;
            Fighter frontUndead = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test front undead", Vector3.right, false, false, false, true });
            Fighter backUndead = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test back undead", Vector3.left, false, false, false, true });
            Fighter farUndead = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test far undead", Vector3.forward * (BrawlerBalance.TurnUndeadRadius + 1), false, false, false, true });
            Fighter normalTurnEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test living enemy", Vector3.right, false, false, false, false });
            turnEnemies.AddRange(new Fighter[] { frontUndead, backUndead, farUndead, normalTurnEnemy });
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_dashRemaining", 0f);
            SetPrivateField(player, "_jumpHeight", 0f);
            SetPrivateField(player, "_jumpVelocity", 0f);
            SetPrivateField(player, "_isRunning", false);
            SetPrivateField(player, "_slideRemaining", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            int turnHealth = player.Health;
            int turnHealUses = cleric.HealingUsesRemaining;
            int turnProtectionUses = cleric.ProtectionUsesRemaining;
            int undeadHealth = frontUndead.Health;
            Require(cleric.TryTurnUndead(), "行動可能時にターニングアンデッドを発動");
            Require(frontUndead.Health == undeadHealth - BrawlerBalance.TurnUndeadDamage
                && backUndead.Health == undeadHealth - BrawlerBalance.TurnUndeadDamage,
                "正面と背後のアンデッドに一度だけダメージ");
            Require(farUndead.Health == undeadHealth && normalTurnEnemy.Health == undeadHealth,
                "奥行きの範囲外と通常敵には効果なし");
            Require(player.Health == turnHealth && cleric.HealingUsesRemaining == turnHealUses
                && cleric.ProtectionUsesRemaining == turnProtectionUses, "HP・魔法回数を消費しない");
            Require(frontUndead.IsDowned && !cleric.TryTurnUndead(), "敵を転倒させ、発動直後の連続使用を拒否");
            SetPrivateField(cleric, "_turnUndeadRecoveryRemaining", 0f);
            Require(!cleric.TryTurnUndead(), "硬直終了後も再使用待ち時間を守る");
            SetPrivateField(cleric, "_turnUndeadCooldownRemaining", 0f);
            SetPrivateField(player, "_jumpHeight", 1f);
            Require(!cleric.TryTurnUndead(), "空中で発動しない");
            SetPrivateField(player, "_jumpHeight", 0f);
            Require(cleric.TryTurnUndead(), "再使用待ち時間終了後は再発動可能");
            player.TakeDamage(5, player.Facing);
            Require(player.Health < turnHealth, "発動中にも被ダメージを受け、無敵は付かない");
            // 拾得の距離・上限・二重取得と、左右の直進・低FPS・射程を検証します。
            // 前のポーション検証は撃破数を2から3へ進めるため、ここではナイフの2体目撃破を独立に作ります。
            Fighter knifeDropEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test knife drop enemy", Vector3.right * 10, false, false, false, false });
            turnEnemies.Add(knifeDropEnemy);
            SetPrivateField(game, "_defeatedEnemyCount", BrawlerBalance.KnifeDropInterval - 1);
            knifeDropEnemy.TakeDamage(1000, 1);
            var knifePickup = UnityEngine.Object.FindAnyObjectByType<ThrowingKnifePickup>();
            Require(knifePickup != null, "敵撃破でナイフをドロップ");
            knifePickup.transform.position = player.transform.position + Vector3.forward;
            Require(!knifePickup.TryCollect(), "奥行きが離れたナイフは拾わない");
            knifePickup.transform.position = player.transform.position;
            int knifeNoticesBefore = UnityEngine.Object.FindObjectsByType<PickupNotice>().Length;
            for (int held = 0; held < BrawlerBalance.KnifeCapacity; held++)
            {
                Require(player.TryStoreThrowingKnife(), "ナイフを上限まで保存可能");
            }
            Require(!knifePickup.TryCollect() && !player.TryStoreThrowingKnife(), "ナイフの所持上限を超えない");
            Require(UnityEngine.Object.FindObjectsByType<PickupNotice>().Length == knifeNoticesBefore, "ナイフの所持上限で通知しない");
            typeof(Fighter).GetProperty("ThrowingKnivesHeld").SetValue(player, 0);
            Require(knifePickup.TryCollect() && player.ThrowingKnivesHeld == 1, "床ナイフを所持品に保存");
            Require(!knifePickup.TryCollect() && player.ThrowingKnivesHeld == 1, "ナイフの二重取得を防止");
            Require(UnityEngine.Object.FindObjectsByType<PickupNotice>().Length == knifeNoticesBefore + 1
                && Array.Exists(UnityEngine.Object.FindObjectsByType<PickupNotice>(), n => n.Kind == BrawlerDefines.PickupKind.ThrowingKnife
                    && n.DisplayText == "ナイフ＋1"), "ナイフ取得時だけ一度通知");
            SetPrivateField(cleric, "_turnUndeadRecoveryRemaining", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            foreach (float knifeFacing in new float[] { -1, 1 })
            {
                foreach (Fighter knifeEnemy in turnEnemies)
                {
                    if (knifeEnemy != null) knifeEnemy.transform.position = new Vector3(20, 0, 3);
                }
                Fighter firstKnifeEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test first knife target", Vector3.right * knifeFacing * 3, false, false, false, false });
                Fighter secondKnifeEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test second knife target", Vector3.right * knifeFacing * 5, false, false, false, false });
                turnEnemies.AddRange(new Fighter[] { secondKnifeEnemy, firstKnifeEnemy });
                player.transform.position = Vector3.zero;
                typeof(Fighter).GetProperty("Facing").SetValue(player, knifeFacing);
                typeof(ClericFighter).GetProperty("SelectedAction").SetValue(cleric, BrawlerDefines.UsableAction.ThrowingKnife);
                SetPrivateField(cleric, "_recoveryRemaining", 0f);
                SetPrivateField(player, "_jumpHeight", 1f);
                Require(!cleric.TryUseSelectedAction() && player.ThrowingKnivesHeld == 1, "空中ではナイフを消費しない");
                SetPrivateField(player, "_jumpHeight", 0f);
                Require(cleric.TryUseSelectedAction() && player.ThrowingKnivesHeld == 0, "選択使用でナイフを一つ消費");
                var thrownKnife = UnityEngine.Object.FindAnyObjectByType<ThrowingKnifeProjectile>();
                Require(thrownKnife != null && !cleric.TryThrowKnife(), "発射して使用後の隙では再使用不可");
                // 振り向いてから大きな時間で進めても、発射時の方向で最初の敵だけに当たります。
                typeof(Fighter).GetProperty("Facing").SetValue(player, -knifeFacing);
                int firstKnifeHealth = firstKnifeEnemy.Health;
                thrownKnife.Advance(1f);
                Require(firstKnifeEnemy.Health == firstKnifeHealth - BrawlerBalance.KnifeDamage
                    && secondKnifeEnemy.Health == firstKnifeHealth, "左右・低FPSでも最初の一体だけに命中");
                thrownKnife.Advance(1f);
                Require(firstKnifeEnemy.Health == firstKnifeHealth - BrawlerBalance.KnifeDamage, "破棄前の再更新でも二重命中しない");
                UnityEngine.Object.DestroyImmediate(thrownKnife.gameObject);
                SetPrivateField(cleric, "_recoveryRemaining", 0f);
                Require(!cleric.TryThrowKnife(), "所持数ゼロでは投げない");
                Require(player.TryStoreThrowingKnife(), "次の射程検証用ナイフを補充");
                firstKnifeEnemy.transform.position = new Vector3(20, 0, 3);
                secondKnifeEnemy.transform.position = Vector3.right * knifeFacing * (BrawlerBalance.KnifeLaunchOffset + BrawlerBalance.KnifeRange + 1);
                typeof(Fighter).GetProperty("Facing").SetValue(player, knifeFacing);
                Require(cleric.TryThrowKnife(), "射程検証用ナイフを発射");
                thrownKnife = UnityEngine.Object.FindAnyObjectByType<ThrowingKnifeProjectile>();
                thrownKnife.Advance(10f);
                Require(secondKnifeEnemy.Health == firstKnifeHealth
                    && Mathf.Abs(thrownKnife.transform.position.x) <= BrawlerBalance.KnifeLaunchOffset + BrawlerBalance.KnifeRange,
                    "大きな経過時間でも射程の先には当たらない");
                UnityEngine.Object.DestroyImmediate(thrownKnife.gameObject);
                SetPrivateField(cleric, "_recoveryRemaining", 0f);
                Require(player.TryStoreThrowingKnife(), "次の左右検証用に補充");
            }
            // 成功した防御・反撃だけが専用演出を生成することを確認します。
            foreach (Fighter effectEnemy in turnEnemies)
            {
                if (effectEnemy != null) effectEnemy.transform.position = new Vector3(20, 0, 3);
            }
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            typeof(Fighter).GetProperty("IsGuarding").SetValue(player, true);
            int guardEffectCount = Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Guard).Length;
            int guardEffectHealth = player.Health;
            player.TakeDamage(10, -player.Facing);
            HitImpact[] guardEffects = Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Guard);
            Require(guardEffects.Length == guardEffectCount + 1 && player.Health == guardEffectHealth, "正面ガード成功で盾の火花を一つ生成");
            Require(Array.Exists(guardEffects, f => Vector3.Distance(f.transform.position, player.GuardImpactPosition) < 0.01f), "実際の盾の位置に火花を生成");
            player.TakeDamage(1, player.Facing);
            Require(Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Guard).Length == guardEffects.Length,
                "背後からの被弾ではガード火花を生成しない");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Fighter counterEffectEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test counter effect", player.transform.position + Vector3.right * player.Facing, false, false, false, false });
            turnEnemies.Add(counterEffectEnemy);
            typeof(Fighter).GetMethod("StartCounterAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            int counterEffectCount = Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Counter).Length;
            game.Hit(player, BrawlerBalance.CounterDamage);
            HitImpact[] counterEffects = Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Counter);
            Require(counterEffects.Length == counterEffectCount + 1, "カウンター命中で専用の十字演出を生成");
            game.Hit(player, BrawlerBalance.CounterDamage);
            counterEffectEnemy.transform.position = new Vector3(20, 0, 3);
            game.Hit(player, BrawlerBalance.CounterDamage);
            Require(Array.FindAll(UnityEngine.Object.FindObjectsByType<HitImpact>(), f => f.Kind == BrawlerDefines.ImpactKind.Counter).Length == counterEffects.Length,
                "硬直で拒否された命中と空振りでは反撃演出を追加しない");
            HitImpact effectToAdvance = counterEffects[counterEffects.Length - 1];
            effectToAdvance.Advance(BrawlerBalance.CounterImpactSeconds);
            Require(effectToAdvance.transform.localScale == Vector3.zero, "ヒットストップ中も実時間で専用演出が終了");
            // 予告開始・進行・攻撃開始・中断・死亡に表示が追従することを確認します。
            Fighter warningEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test warning", Vector3.right * 10, false, false, false, false });
            EnemyAttackWarning warning = warningEnemy.GetComponentInChildren<EnemyAttackWarning>();
            Require(warning != null && !warning.IsVisible, "敵の生成時に予告マーカーを一度だけ作り、通常時は非表示");
            MethodInfo beginWarning = typeof(Fighter).GetMethod("BeginAttackWindup", BindingFlags.Instance | BindingFlags.NonPublic);
            beginWarning.Invoke(warningEnemy, new object[] { BrawlerBalance.MeleeWindupSeconds });
            warning.Refresh();
            Require(warning.IsVisible && warning.CurrentColor == Color.yellow && warningEnemy.AttackWindupProgress == 0,
                "予告開始時は黄色で表示");
            SetPrivateField(warningEnemy, "_windupRemaining", BrawlerBalance.MeleeWindupSeconds / 2);
            warning.Refresh();
            Require(Mathf.Abs(warningEnemy.AttackWindupProgress - 0.5f) < 0.01f
                && warning.CurrentColor.g < 1 && warning.CurrentColor.r == 1, "半分進むと橙色へ変化");
            foreach (float warningFacing in new float[] { -1, 1 })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(warningEnemy, warningFacing);
                warning.Refresh();
                Require(Mathf.Sign(warning.transform.Find("Attack direction").localPosition.x) == warningFacing,
                    "左右の攻撃方向を足元の棒で表示");
            }
            SetPrivateField(warningEnemy, "_windupRemaining", BrawlerBalance.MeleeWindupSeconds * 0.01f);
            warning.Refresh();
            Require(warning.CurrentColor.g < 0.02f, "発動直前は赤に近づく");
            typeof(Fighter).GetMethod("UpdateAttackWindup", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(warningEnemy, new object[] { BrawlerBalance.MeleeWindupSeconds });
            warning.Refresh();
            Require(!warning.IsVisible, "攻撃開始で予告を消す");
            beginWarning.Invoke(warningEnemy, new object[] { BrawlerBalance.HeavyWindupSeconds });
            SetPrivateField(warningEnemy, "_windupRemaining", BrawlerBalance.HeavyWindupSeconds / 2);
            Require(Mathf.Abs(warningEnemy.AttackWindupProgress - 0.5f) < 0.01f, "異なる予告時間も正しく正規化");
            warningEnemy.TakeDamage(1, 1);
            warning.Refresh();
            Require(!warning.IsVisible, "被ダメージによる中断でマーカーを消す");
            SetPrivateField(warningEnemy, "_stunRemaining", 0f);
            beginWarning.Invoke(warningEnemy, new object[] { BrawlerBalance.MeleeWindupSeconds });
            warningEnemy.TakeDamage(1000, 1);
            warning.Refresh();
            Require(!warning.IsVisible && Array.TrueForAll(warning.GetComponentsInChildren<Renderer>(), r => !r.enabled),
                "死亡後は全マーカーレンダラーを非表示");
            Require(warningEnemy.GetComponentsInChildren<EnemyAttackWarning>().Length == 1, "予告を繰り返してもマーカーは増えない");
            Fighter rangedWarningEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test ranged warning", Vector3.zero, false, true, false, false });
            Fighter meleeWarningEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test melee warning", Vector3.zero, false, false, true, false });
            foreach (Fighter rangeOwner in new Fighter[] { meleeWarningEnemy, rangedWarningEnemy })
            {
                EnemyAttackWarning rangeWarning = rangeOwner.GetComponentInChildren<EnemyAttackWarning>();
                beginWarning.Invoke(rangeOwner, new object[] { BrawlerBalance.MeleeWindupSeconds });
                foreach (float rangeFacing in new float[] { -1, 1 })
                {
                    typeof(Fighter).GetProperty("Facing").SetValue(rangeOwner, rangeFacing);
                    rangeWarning.Refresh();
                    Transform frontRangeEdge = rangeWarning.transform.Find("Front edge");
                    Require(Mathf.Abs(frontRangeEdge.localScale.x - (rangeOwner.WarningRangeEnd - rangeOwner.WarningRangeStart)) < 0.01f
                        && Mathf.Abs(frontRangeEdge.localPosition.z - rangeOwner.WarningLaneHalfWidth) < 0.01f,
                        "種類ごとの予告長さと奥行き幅を表示");
                    Require(Mathf.Abs(rangeWarning.transform.Find("Right edge").localPosition.x - rangeFacing * rangeOwner.WarningRangeEnd) < 0.01f
                        && Mathf.Abs(rangeWarning.transform.Find("Left edge").localPosition.x - rangeFacing * rangeOwner.WarningRangeStart) < 0.01f,
                        "左右の向きに応じて範囲の始点と終点を反転");
                }
            }
            Require(meleeWarningEnemy.WarningRangeStart == -BrawlerBalance.AttackBehindAllowance
                && meleeWarningEnemy.WarningRangeEnd == BrawlerBalance.EnemyAttackReach, "近接表示は実際のリーチと背後の余裕を共用");
            Require(rangedWarningEnemy.WarningRangeStart == BrawlerBalance.EnemyProjectileLaunchOffset
                && rangedWarningEnemy.WarningRangeEnd == BrawlerBalance.EnemyProjectileLaunchOffset
                    + BrawlerBalance.EnemyProjectileSpeed * BrawlerBalance.EnemyProjectileLifetimeSeconds
                && rangedWarningEnemy.WarningLaneHalfWidth == BrawlerBalance.EnemyProjectileHitRadius,
                "射線表示は実際の弾の発射位置・飛距離・衝突幅を共用");
            // 受理された実ダメージだけを表示し、技の種類が表示色へ伝わることを確認します。
            foreach (Fighter numberEnemy in turnEnemies)
            {
                if (numberEnemy != null) numberEnemy.transform.position = new Vector3(20, 0, 3);
            }
            player.transform.position = Vector3.zero;
            typeof(Fighter).GetProperty("Facing").SetValue(player, 1f);
            foreach (BrawlerDefines.DamageKind numberKind in new BrawlerDefines.DamageKind[]
                { BrawlerDefines.DamageKind.Normal, BrawlerDefines.DamageKind.Counter, BrawlerDefines.DamageKind.HeavyStrike })
            {
                Fighter numberTarget = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test damage number", Vector3.right, false, false, false, false });
                turnEnemies.Add(numberTarget);
                int damageNumberCount = UnityEngine.Object.FindObjectsByType<DamageNumber>().Length;
                int numberDamage = 18;
                if (numberKind != BrawlerDefines.DamageKind.Normal)
                {
                    string actionName = numberKind == BrawlerDefines.DamageKind.Counter ? "StartCounterAttack" : "StartHeavyStrike";
                    typeof(Fighter).GetMethod(actionName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                    numberDamage = numberKind == BrawlerDefines.DamageKind.Counter ? BrawlerBalance.CounterDamage : BrawlerBalance.HeavyStrikeDamage;
                    game.Hit(player, numberDamage);
                }
                else
                {
                    numberTarget.TakeDamage(numberDamage, 1);
                }
                Require(UnityEngine.Object.FindObjectsByType<DamageNumber>().Length == damageNumberCount + 1, "実際の命中で数値を一つ表示");
                DamageNumber shownNumber = Array.Find(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Amount == numberDamage
                    && n.Kind == numberKind && Vector3.Distance(n.transform.position, numberTarget.transform.position + Vector3.up * BrawlerBalance.DamageNumberHeight) < 0.01f);
                Require(shownNumber != null, "通常・反撃・大斬りの種類と威力を頭上表示へ渡す");
                numberTarget.TakeDamage(numberDamage, 1);
                Require(UnityEngine.Object.FindObjectsByType<DamageNumber>().Length == damageNumberCount + 1, "硬直中の無効な命中では表示を追加しない");
                Vector3 numberStart = shownNumber.transform.position;
                shownNumber.Advance(BrawlerBalance.DamageNumberSeconds / 2);
                Require(shownNumber.transform.position.y > numberStart.y && Mathf.Abs(shownNumber.Alpha - 0.5f) < 0.01f,
                    "停止中も実時間で上昇し薄くなる");
                shownNumber.Advance(BrawlerBalance.DamageNumberSeconds);
                Require(shownNumber.Alpha == 0, "表示時間終了で透明になり破棄される");
                numberTarget.transform.position = new Vector3(20, 0, 3);
            }
            Fighter overkillTarget = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test overkill number", Vector3.right * 3, false, false, false, false });
            int overkillHealth = overkillTarget.Health;
            overkillTarget.TakeDamage(1000, 1);
            Require(Array.Exists(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Amount == overkillHealth
                && n.transform.parent == null), "撃破時は残りHP分を表示し、敵の破棄に巻き込まれない");
            // 回復通知は共通HP処理から一度だけ出し、上限で切り止めた実回復量を表示します。
            player.RestoreHealth(player.MaxHealth);
            int healingNumberCount = Array.FindAll(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing).Length;
            player.RestoreHealth(10);
            player.RestoreHealth(0);
            player.RestoreHealth(-10);
            Require(Array.FindAll(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing).Length == healingNumberCount,
                "満タン・ゼロ・負の回復量では表示を追加しない");
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            SetPrivateField(cleric, "_turnUndeadRecoveryRemaining", 0f);
            SetPrivateField(cleric, "_protectionRemaining", 0f);
            player.TakeDamage(25, player.Facing);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            typeof(ClericFighter).GetProperty("HealingUsesRemaining").SetValue(cleric, 1);
            Require(cleric.TryHeal() && player.Health == player.MaxHealth, "回復表示用の魔法を発動");
            DamageNumber[] healingNumbers = Array.FindAll(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing);
            Require(healingNumbers.Length == healingNumberCount + 1
                && Array.Exists(healingNumbers, n => n.Amount == 25 && n.DisplayText == "+25" && n.DisplayColor == Color.green),
                "回復魔法は上限で切り止めた25を緑の＋数値で一度だけ表示");
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            player.TakeDamage(5, player.Facing);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            Require(player.TryStoreHealingPotion() && cleric.TryUseHealingPotion(), "ポーションの回復表示を確認");
            Require(Array.FindAll(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing).Length == healingNumberCount + 2
                && Array.Exists(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing && n.DisplayText == "+5"),
                "ポーションも実際に増えた5だけを一度表示");
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            player.TakeDamage(20, player.Facing);
            typeof(BeltBrawler).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Require(player.Health == player.MaxHealth - 20 + BrawlerBalance.WaveHealingAmount
                && Array.FindAll(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing).Length == healingNumberCount + 3,
                "ウェーブ報酬の回復も一度だけ表示");
            DamageNumber healingToAdvance = Array.Find(UnityEngine.Object.FindObjectsByType<DamageNumber>(), n => n.Kind == BrawlerDefines.DamageKind.Healing && n.DisplayText == "+5");
            healingToAdvance.Advance(BrawlerBalance.DamageNumberSeconds / 2);
            Require(Mathf.Abs(healingToAdvance.Alpha - 0.5f) < 0.01f, "回復数値も停止中に実時間でフェード");
            PickupNotice pickupToAdvance = UnityEngine.Object.FindAnyObjectByType<PickupNotice>();
            Vector3 pickupNoticeStart = pickupToAdvance.transform.position;
            pickupToAdvance.Advance(BrawlerBalance.PickupNoticeSeconds / 2);
            Require(pickupToAdvance.transform.position.y > pickupNoticeStart.y
                && Mathf.Abs(pickupToAdvance.Alpha - 0.5f) < 0.01f, "取得通知も停止中に上昇・フェード");
            pickupToAdvance.Advance(BrawlerBalance.PickupNoticeSeconds);
            Require(pickupToAdvance.Alpha == 0, "取得通知は表示時間終了で消える");
            // 使用可否の表示は実際の発動判定と同じ結果になり、照会だけでは消費しません。
            player.RestoreHealth(player.MaxHealth);
            SetPrivateField(player, "_attackRemaining", 0f);
            SetPrivateField(player, "_attackCooldown", 0f);
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(cleric, "_recoveryRemaining", 0f);
            SetPrivateField(cleric, "_turnUndeadRecoveryRemaining", 0f);
            SetPrivateField(cleric, "_protectionRemaining", 0f);
            typeof(ClericFighter).GetProperty("HealingUsesRemaining").SetValue(cleric, 1);
            typeof(ClericFighter).GetProperty("ProtectionUsesRemaining").SetValue(cleric, 1);
            typeof(Fighter).GetProperty("HealingPotionsHeld").SetValue(player, 1);
            typeof(Fighter).GetProperty("ThrowingKnivesHeld").SetValue(player, 1);
            Require(cleric.GetActionUseStatus(BrawlerDefines.UsableAction.Healing) == BrawlerDefines.ActionUseStatus.HealthFull
                && !cleric.TryHeal() && cleric.HealingUsesRemaining == 1, "HP満タン表示と回復の拒否が一致");
            Require(cleric.GetActionUseStatus(BrawlerDefines.UsableAction.HealingPotion) == BrawlerDefines.ActionUseStatus.HealthFull
                && !cleric.TryUseHealingPotion() && player.HealingPotionsHeld == 1, "HP満タン表示とポーションの拒否が一致");
            typeof(Fighter).GetProperty("ThrowingKnivesHeld").SetValue(player, 0);
            Require(cleric.GetActionUseStatus(BrawlerDefines.UsableAction.ThrowingKnife) == BrawlerDefines.ActionUseStatus.Empty
                && !cleric.TryThrowKnife(), "残りゼロ表示とナイフの拒否が一致");
            SetPrivateField(cleric, "_protectionRemaining", 1f);
            Require(cleric.GetActionUseStatus(BrawlerDefines.UsableAction.Protection) == BrawlerDefines.ActionUseStatus.ProtectionActive
                && !cleric.TryProtection() && cleric.ProtectionUsesRemaining == 1, "効果中表示と補助魔法の拒否が一致");
            foreach (BrawlerDefines.UsableAction statusAction in Enum.GetValues(typeof(BrawlerDefines.UsableAction)))
            {
                typeof(ClericFighter).GetProperty("SelectedAction").SetValue(cleric, statusAction);
                SetPrivateField(player, "_attackRemaining", 0.1f);
                Require(cleric.SelectedActionUseStatus == BrawlerDefines.ActionUseStatus.Busy
                    && !cleric.TryUseSelectedAction(), "行動中の表示と選択発動の拒否が一致");
                SetPrivateField(player, "_attackRemaining", 0f);
                SetPrivateField(player, "_jumpHeight", 1f);
                Require(cleric.SelectedActionUseStatus == BrawlerDefines.ActionUseStatus.Airborne
                    && !cleric.TryUseSelectedAction(), "空中表示と選択発動の拒否が一致");
                SetPrivateField(player, "_jumpHeight", 0f);
            }
            SetPrivateField(cleric, "_protectionRemaining", 0f);
            Require(cleric.GetActionUseStatus(BrawlerDefines.UsableAction.Protection) == BrawlerDefines.ActionUseStatus.Ready
                && cleric.TryProtection(), "使用可能表示と補助魔法の発動が一致");
            Fighter fadeTarget = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test defeat fade", Vector3.right * 10, false, false, false, false });
            Fighter unfadedTarget = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test shared material", Vector3.right * 12, false, false, false, false });
            Transform fadeVisual = (Transform)GetPrivateField(fadeTarget, "_visual");
            Transform unfadedVisual = (Transform)GetPrivateField(unfadedTarget, "_visual");
            Renderer fadeBody = fadeVisual.Find("Body").GetComponent<Renderer>();
            Renderer unfadedBody = unfadedVisual.Find("Body").GetComponent<Renderer>();
            Material originalBodyMaterial = fadeBody.sharedMaterial;
            Require(unfadedBody.sharedMaterial == originalBodyMaterial, "生存中の敵は素材を共有");
            fadeTarget.TakeDamage(BrawlerBalance.KnockdownDamageThreshold, 1);
            Require(fadeTarget.IsDowned && fadeTarget.GetComponent<EnemyDefeatFade>() == null, "生存中のダウンには撃破フェードを付けない");
            SetPrivateField(fadeTarget, "_stunRemaining", 0f);
            fadeTarget.TakeDamage(1000, 1);
            EnemyDefeatFade defeatFade = fadeTarget.GetComponent<EnemyDefeatFade>();
            Require(defeatFade != null && defeatFade.Alpha == 1 && fadeTarget.Health == 0, "撃破時だけフェードを開始");
            Require(fadeBody.sharedMaterial != originalBodyMaterial && unfadedBody.sharedMaterial == originalBodyMaterial
                && originalBodyMaterial.color.a == 1, "撃破した個体だけ素材を複製し、共有素材を保持");
            defeatFade.Advance(BrawlerBalance.EnemyRemovalSeconds / 2);
            var fadeProperties = new MaterialPropertyBlock();
            fadeBody.GetPropertyBlock(fadeProperties);
            Require(Mathf.Abs(defeatFade.Alpha - 0.5f) < 0.01f
                && Mathf.Abs(fadeProperties.GetColor("_Color").a - 0.5f) < 0.01f, "時間経過で体の透明度を下げる");
            defeatFade.Advance(0);
            Require(Mathf.Abs(defeatFade.Alpha - 0.5f) < 0.01f, "ヒットストップ中は消滅時間を進めない");
            fadeTarget.TakeDamage(1000, 1);
            Require(fadeTarget.GetComponents<EnemyDefeatFade>().Length == 1, "撃破済みへの追加攻撃で演出を二重生成しない");
            defeatFade.Advance(BrawlerBalance.EnemyRemovalSeconds);
            Require(defeatFade.Alpha == 0, "既存の消滅時間で透明になり破棄される");
            // 全滅通知は一度だけ出し、次の出現予告中をクリアと誤認しないことを確認します。
            var banner = (WaveBanner)GetPrivateField(game, "_waveBanner");
            var bannerPending = (System.Collections.Generic.List<Vector3>)GetPrivateField(game, "_pendingSpawnPositions");
            turnEnemies.Clear();
            bannerPending.Clear();
            SetPrivateField(game, "_waveClearAnnounced", false);
            SetPrivateField(game, "_nextWaveElapsed", 0f);
            banner.Clear();
            MethodInfo waveProgress = typeof(BeltBrawler).GetMethod("UpdateWaveProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            waveProgress.Invoke(game, new object[] { 0f });
            Require(banner.Text == "WAVE CLEAR", "全滅時に中央クリア通知を開始");
            banner.Advance(BrawlerBalance.WaveBannerSeconds / 2);
            float bannerRemaining = banner.RemainingSeconds;
            waveProgress.Invoke(game, new object[] { 0f });
            Require(banner.RemainingSeconds == bannerRemaining, "空の状態が続いても通知の時間をリセットしない");
            banner.Advance(BrawlerBalance.WaveBannerSeconds);
            waveProgress.Invoke(game, new object[] { 0f });
            Require(banner.Text == string.Empty, "消えたクリア通知を同じウェーブで再表示しない");
            int waveBeforeBanner = (int)GetPrivateField(game, "_wave");
            waveProgress.Invoke(game, new object[] { BrawlerBalance.NextWaveDelaySeconds + 0.1f });
            Require(banner.Text == "WAVE " + (waveBeforeBanner + 1) && bannerPending.Count > 0,
                "次の出現予告と同時にウェーブ番号を表示");
            waveProgress.Invoke(game, new object[] { 0f });
            Require(banner.Text != "WAVE CLEAR", "出現待ちの敵がいる間はクリア通知しない");
            banner.Advance(BrawlerBalance.WaveBannerSeconds - BrawlerBalance.WaveBannerFadeSeconds / 2);
            Require(Mathf.Abs(banner.Alpha - 0.5f) < 0.01f, "終了前にウェーブ通知をフェード");
            // 攻撃方向と攻撃元は逆です。有効被弾だけが方向通知を更新することを確認します。
            var hitDirection = (HitDirectionIndicator)GetPrivateField(game, "_hitDirectionIndicator");
            SetPrivateField(player, "_stunRemaining", 0f);
            SetPrivateField(player, "_dashRemaining", 0f);
            typeof(Fighter).GetProperty("IsGuarding").SetValue(player, false);
            player.TakeDamage(1, 1);
            Require(hitDirection.SourceSide == -1 && hitDirection.RemainingSeconds == BrawlerBalance.HitDirectionSeconds,
                "右へ進む攻撃の被弾元を左側へ表示");
            hitDirection.Advance(0.1f);
            float directionRemaining = hitDirection.RemainingSeconds;
            player.TakeDamage(1, -1);
            Require(hitDirection.SourceSide == -1 && hitDirection.RemainingSeconds == directionRemaining,
                "硬直で無効な命中は方向通知を更新しない");
            SetPrivateField(player, "_stunRemaining", 0f);
            typeof(Fighter).GetProperty("Facing").SetValue(player, 1f);
            typeof(Fighter).GetProperty("IsGuarding").SetValue(player, true);
            player.TakeDamage(1, -1);
            Require(hitDirection.SourceSide == -1 && hitDirection.RemainingSeconds == directionRemaining,
                "ガード成功では被弾方向を通知しない");
            typeof(Fighter).GetProperty("IsGuarding").SetValue(player, false);
            SetPrivateField(player, "_dashRemaining", 0.1f);
            player.TakeDamage(1, -1);
            Require(hitDirection.RemainingSeconds == directionRemaining, "回避無敵では通知しない");
            SetPrivateField(player, "_dashRemaining", 0f);
            player.TakeDamage(1, -1);
            Require(hitDirection.SourceSide == 1, "左へ進む攻撃の被弾元を右側へ表示");
            typeof(Fighter).GetProperty("Facing").SetValue(player, -1f);
            Require(hitDirection.SourceSide == 1, "振り向いても記憶した被弾側を変更しない");
            hitDirection.Advance(BrawlerBalance.HitDirectionSeconds - BrawlerBalance.HitDirectionFadeSeconds / 2);
            Require(Mathf.Abs(hitDirection.Alpha - 0.5f) < 0.01f, "通知終了前に赤い矢印をフェード");
            hitDirection.Advance(BrawlerBalance.HitDirectionSeconds);
            Require(hitDirection.RemainingSeconds == 0 && hitDirection.Alpha == 0, "被弾方向通知は時間経過で消える");
            SetPrivateField(player, "_stunRemaining", 0f);
            player.TakeDamage(1000, player.Facing);
            waveProgress.Invoke(game, new object[] { 0f });
            Require(banner.Text == string.Empty, "敗北時は中央ウェーブ通知を消す");
            Require(game.IsDefeated && !game.IsStageCleared && game.IsRunFinished,
                "致命傷で敗北リザルトを確定");
            int defeatScore = game.ResultScore;
            int defeatKills = game.ResultDefeatedEnemies;
            int defeatWave = game.ResultWave;
            game.RegisterDamage(player, 10, 10);
            waveProgress.Invoke(game, new object[] { 10f });
            Require(game.ResultScore == defeatScore && game.ResultDefeatedEnemies == defeatKills
                && game.ResultWave == defeatWave && bannerPending.Count == 0,
                "敗北後は結果を固定し、敵の出現予約を破棄");
            Require(cleric.SelectedActionUseStatus == BrawlerDefines.ActionUseStatus.Defeated,
                "敗北後の選択アクションは死亡状態を返す");
            // この後のクリア検証は別のプレイ状況として実行するため、検証内だけで終了状態を戻します。
            typeof(BeltBrawler).GetProperty("IsDefeated").SetValue(game, false);

            ValidateHealthFeedback();
            Fighter trailEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test enemy HP trail", Vector3.right * 10, false, false, true, false });
            Fighter separateTrailEnemy = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test separate HP trail", Vector3.right * 12, false, true, false, false });
            int trailEnemyMaxHealth = trailEnemy.MaxHealth;
            Require(trailEnemy.EnemyHealthTrail == trailEnemyMaxHealth
                && separateTrailEnemy.EnemyHealthTrail == separateTrailEnemy.MaxHealth, "敵ごとの最大HPで残像を初期化");
            trailEnemy.TakeDamage(18, 1);
            Require(trailEnemy.Health == trailEnemyMaxHealth - 18 && trailEnemy.EnemyHealthTrail == trailEnemyMaxHealth,
                "敵への命中で現在HPだけ即座に減り、赤い残像は直前HPを保持");
            Require(separateTrailEnemy.EnemyHealthTrail == separateTrailEnemy.MaxHealth, "他の敵のHP残像には影響しない");
            var enemyTrailFeedback = (HealthBarFeedback)GetPrivateField(trailEnemy, "_enemyHealthBarFeedback");
            enemyTrailFeedback.Refresh(trailEnemy.Health, trailEnemy.MaxHealth, BrawlerBalance.HealthTrailDelaySeconds);
            enemyTrailFeedback.Refresh(trailEnemy.Health, trailEnemy.MaxHealth, 0.05f);
            Require(trailEnemy.EnemyHealthTrail < trailEnemyMaxHealth && trailEnemy.EnemyHealthTrail > trailEnemy.Health,
                "敵の残像も待機後に現在HPへ縮む");
            trailEnemy.RestoreHealth(5);
            Require(trailEnemy.EnemyHealthTrail == trailEnemy.Health, "敵の回復では古い残像を消す");
            SetPrivateField(trailEnemy, "_stunRemaining", 0f);
            trailEnemy.TakeDamage(BrawlerBalance.CounterDamage, 1, BrawlerDefines.DamageKind.Counter);
            float enemyTrailPaused = trailEnemy.EnemyHealthTrail;
            enemyTrailFeedback.Refresh(trailEnemy.Health, trailEnemy.MaxHealth, 0);
            Require(trailEnemy.EnemyHealthTrail == enemyTrailPaused, "敵の残像も停止中は進まない");
            ValidateAttackTransitions(game, createTurnEnemy);
            ValidateStageClear(game, createTurnEnemy, turnEnemies, bannerPending, waveProgress);
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

    // 表示用の計算はゲーム進行と切り離して検証し、テスト同士の状態の持ち越しを減らします。
    private static void ValidateHealthFeedback()
    {
        // HP残像は実HPを変えず、待機・連続被弾・回復・死亡を正しく表示します。
        var healthFeedback = new HealthBarFeedback();
        healthFeedback.Refresh(100, 100, 0);
        healthFeedback.Refresh(80, 100, 0);
        Require(healthFeedback.TrailHealth == 100, "被弾直後は以前のHPを赤い残像として保持");
        healthFeedback.Refresh(80, 100, BrawlerBalance.HealthTrailDelaySeconds);
        Require(healthFeedback.TrailHealth == 100, "待機中は赤い残像を縮めない");
        healthFeedback.Refresh(80, 100, 0.05f);
        Require(healthFeedback.TrailHealth < 100 && healthFeedback.TrailHealth > 80, "待機後は残像が現在HPへ徐々に追いつく");
        float trailBeforeSecondHit = healthFeedback.TrailHealth;
        healthFeedback.Refresh(60, 100, 0);
        healthFeedback.Refresh(60, 100, 0);
        Require(healthFeedback.TrailHealth == trailBeforeSecondHit, "連続被弾と時間停止では残像を保持");
        healthFeedback.Refresh(90, 100, 0);
        Require(healthFeedback.TrailHealth == 90, "回復すると古い被ダメージ残像を消す");
        healthFeedback.Refresh(0, 100, 0);
        Require(healthFeedback.TrailHealth == 90, "死亡時も直前HPを残像として保持");
        healthFeedback.Refresh(0, 100, 10);
        Require(healthFeedback.TrailHealth == 0, "大きな経過時間でもゼロを超えず縮みきる");
    }

    private static void ValidateStageClear(BeltBrawler game, MethodInfo createTurnEnemy,
        System.Collections.Generic.List<Fighter> turnEnemies,
        System.Collections.Generic.List<Vector3> bannerPending, MethodInfo waveProgress)
    {
        // 最終ウェーブの出現待ち・戦闘中はクリアせず、全滅して消えた時点だけ結果を確定します。
        Fighter resultPlayer = (Fighter)createTurnEnemy.Invoke(game, new object[] { "Test result player", Vector3.zero, true, false, false, false });
        SetPrivateField(game, "_player", resultPlayer);
        turnEnemies.Clear();
        bannerPending.Clear();
        SetPrivateField(game, "_wave", BrawlerBalance.StageWaveCount - 1);
        typeof(BeltBrawler).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        waveProgress.Invoke(game, new object[] { 0f });
        Require(!game.IsStageCleared && bannerPending.Count > 0, "最終ウェーブの出現予告中はクリアしない");
        typeof(BeltBrawler).GetMethod("UpdateSpawnWarning", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game, new object[] { BrawlerBalance.SpawnWarningSeconds });
        Require(turnEnemies.Count > 0 && !game.IsStageCleared, "最終ウェーブにも敵を配置");
        foreach (Fighter finalEnemy in turnEnemies)
        {
            finalEnemy.TakeDamage(1000, 1);
        }
        waveProgress.Invoke(game, new object[] { 0f });
        Require(!game.IsStageCleared, "撃破フェード中は結果画面へ移らない");
        foreach (Fighter finalEnemy in turnEnemies)
        {
            UnityEngine.Object.DestroyImmediate(finalEnemy.gameObject);
        }
        turnEnemies.Clear();
        int finalScore = (int)GetPrivateField(game, "_score");
        int finalKills = (int)GetPrivateField(game, "_defeatedEnemyCount");
        waveProgress.Invoke(game, new object[] { 0f });
        Require(game.IsStageCleared && game.ResultScore == finalScore && game.ResultDefeatedEnemies == finalKills,
            "最終全滅後にクリアとスコア・撃破数を確定");
        waveProgress.Invoke(game, new object[] { 10f });
        typeof(BeltBrawler).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        Require(bannerPending.Count == 0 && (int)GetPrivateField(game, "_wave") == BrawlerBalance.StageWaveCount,
            "クリア後は第六ウェーブを予約しない");
        int resultHealth = resultPlayer.Health;
        resultPlayer.TakeDamage(10, 1);
        game.RegisterDamage(resultPlayer, 10, 10);
        Require(resultPlayer.Health == resultHealth && game.ResultScore == finalScore
            && (int)GetPrivateField(game, "_score") == finalScore, "クリア後は被ダメージと得点更新を停止");
        Require(((ClericFighter)resultPlayer).SelectedActionUseStatus == BrawlerDefines.ActionUseStatus.Unavailable
            && !resultPlayer.TryStoreHealingPotion(), "クリア後は魔法・アイテム使用と取得を停止");
        Fighter stoppedTarget = (Fighter)createTurnEnemy.Invoke(game,
            new object[] { "Test stopped ground target", Vector3.zero, false, false, false, false });
        SetPrivateField(stoppedTarget, "_downRemaining", BrawlerBalance.DownSeconds);
        int stoppedHealth = stoppedTarget.Health;
        Require(!stoppedTarget.TryTakeGroundHit(BrawlerBalance.GroundAttackDamage, 1)
            && stoppedTarget.Health == stoppedHealth, "終了後は追撃経路でもHPを変更しない");
    }

    // 本番の開始処理を呼び、技を切り替えたときに前の攻撃種別が残らないことを確認します。
    private static void ValidateAttackTransitions(BeltBrawler game, MethodInfo createFighter)
    {
        Fighter fighter = (Fighter)createFighter.Invoke(game,
            new object[] { "Test attack transitions", Vector3.zero, true, false, false, false });
        typeof(Fighter).GetMethod("StartHeavyStrike", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fighter, null);
        Require(fighter.IsHeavyStriking && !fighter.IsCounterAttacking, "強攻撃は反撃の種類を残さない");
        typeof(Fighter).GetMethod("StartAirAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fighter, new object[] { true });
        Require(fighter.IsAirAttacking && !fighter.IsHeavyStriking, "下突きへ切替後は強攻撃を解除");
        typeof(Fighter).GetMethod("StartCounterAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fighter, null);
        Require(fighter.IsCounterAttacking && !fighter.IsAirAttacking, "反撃へ切替後は空中攻撃を解除");
        typeof(Fighter).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fighter, null);
        Require(!fighter.IsCounterAttacking && !fighter.IsAirAttacking && !fighter.IsHeavyStriking,
            "通常攻撃へ切替後は特殊技の種類を解除");
        UnityEngine.Object.DestroyImmediate(fighter.gameObject);
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
