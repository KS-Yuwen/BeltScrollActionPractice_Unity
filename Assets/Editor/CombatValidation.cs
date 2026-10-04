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
            var enemy = Array.Find(UnityEngine.Object.FindObjectsByType<Fighter>(), f => !f.IsPlayer);
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
            enemy.TakeDamage(1000, 1);
            Require(enemy.Health == 0 && (int)GetPrivateField(game, "_score") == previousScore + 110, "撃破時のスコアと HP 下限");
            enemy.TakeDamage(1000, 1);
            Require((int)GetPrivateField(game, "_score") == previousScore + 110, "撃破スコアの二重加算を防止");

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
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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
