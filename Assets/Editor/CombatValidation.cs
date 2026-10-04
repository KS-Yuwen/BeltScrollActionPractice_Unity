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
    static double deadline;
    static double readyAt;

    // Play 開始時のドメインリロードでは static 状態が初期化されます。
    // SessionState に実行中フラグを残し、リロード後に検証処理を再登録します。
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool("CombatValidation.Running", false)) return;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= Check;
        EditorApplication.update += Check;
        Application.logMessageReceived -= WatchErrors;
        Application.logMessageReceived += WatchErrors;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/NeonStreet.unity");
        SessionState.SetBool("CombatValidation.Running", true);
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Check;
        Application.logMessageReceived += WatchErrors;
        EditorApplication.isPlaying = true;
    }

    static void Check()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play モードの開始がタイムアウトしました。");
            if (!EditorApplication.isPlaying) return;
            var game = UnityEngine.Object.FindAnyObjectByType<BeltBrawler>();
            if (game == null || game.Target == null) return;
            var enemy = Array.Find(UnityEngine.Object.FindObjectsByType<Fighter>(), f => !f.isPlayer);
            if (enemy == null) return;
            // Start だけでなく通常の Update も走らせ、初期化後の例外を検出します。
            if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + .5; return; }
            if (EditorApplication.timeSinceStartup < readyAt) return;
            var player = game.Target;

            // 回避中の無敵と、回避終了後にダメージを受けることを両方確認します。
            Set(player, "dashTime", .2f);
            player.Damage(10, 1);
            Require(player.health == 100, "回避中はダメージを無効化");
            Set(player, "dashTime", 0f);
            player.Damage(10, 1);
            Require(player.health == 90, "回避終了後はダメージを受理");

            // 無敵で無効になった命中は、連続ヒットを水増ししないことを確認します。
            Set(enemy, "windup", .5f);
            enemy.Damage(18, 1);
            Require(!enemy.IsWindingUp && game.HitChain == 1, "被ダメージで予告を中断し命中を加算");
            enemy.Damage(18, 1);
            Require(game.HitChain == 1, "硬直中の重複ダメージを拒否");
            Set(enemy, "stun", 0f);
            enemy.Damage(30, 1);
            Require(((Vector3)Get(enemy, "knockback")).x == 9, "最終段の強い吹き飛ばし");
            Set(player, "stun", 0f);
            player.Damage(10, 1);
            Require(game.HitChain == 0, "被ダメージで連続ヒットをリセット");

            // ダメージ量が HP を超えても負にならず、一度だけ撃破ボーナスを加算します。
            Set(enemy, "stun", 0f);
            int previousScore = (int)Get(game, "score");
            enemy.Damage(1000, 1);
            Require(enemy.health == 0 && (int)Get(game, "score") == previousScore + 110, "撃破時のスコアと HP 下限");
            enemy.Damage(1000, 1);
            Require((int)Get(game, "score") == previousScore + 110, "撃破スコアの二重加算を防止");

            Debug.Log("COMBAT_VALIDATION_PASSED");
            Finish(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    // テスト用に内部状態を設定します。ゲーム側の公開 API を増やさず確認するための仕組みです。
    static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("確認失敗：" + message);
    }
    static void Finish(int code)
    {
        SessionState.SetBool("CombatValidation.Running", false);
        EditorApplication.update -= Check;
        Application.logMessageReceived -= WatchErrors;
        EditorApplication.Exit(code);
    }

    // アサーション以外の Unity 実行時エラーも、検証失敗として扱います。
    static void WatchErrors(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // この環境では Unity エディターの検索 DB が起動時に例外を出すことがあります。
        // ゲームコードを含まない、この既知のエディター検索エラーだけは検証対象外にします。
        if (trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("BeltBrawler") && !trace.Contains("Fighter")) return;
        // ログ出力の途中で終了すると原因が記録されないため、次のエディター更新で終了します。
        Application.logMessageReceived -= WatchErrors;
        Debug.LogWarning("COMBAT_VALIDATION_FAILED: " + message + "\n" + trace);
        EditorApplication.update -= Check;
        EditorApplication.delayCall += () => Finish(1);
    }
}
