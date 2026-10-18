using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 思考の調整値を検証してから公開します。CSVの一部が不正なら全体を既定値へ戻します。
public sealed class EnemyAiSettings
{
    private static readonly Dictionary<string, (float Default, float Min, float Max, bool Integer)> s_rules
        = new Dictionary<string, (float Default, float Min, float Max, bool Integer)>(StringComparer.Ordinal)
    {
        { nameof(MeleeSpawnWaitSeconds), (0.6f, 0, 5, false) },
        { nameof(UndeadSpawnWaitSeconds), (0.6f, 0, 5, false) },
        { nameof(RangedSpawnWaitSeconds), (0.8f, 0, 5, false) },
        { nameof(HeavySpawnWaitSeconds), (1f, 0, 5, false) },
        { nameof(BossSpawnWaitSeconds), (1.2f, 0, 5, false) },
        { nameof(MaxSimultaneousAttackers), (1, 1, 8, true) },
        { nameof(AttackStartIntervalSeconds), (0.75f, 0, 10, false) },
        { nameof(MeleeWindupSeconds), (0.55f, 0.05f, 5, false) },
        { nameof(HeavyWindupSeconds), (0.95f, 0.05f, 5, false) },
        { nameof(MeleeAttackIntervalSeconds), (1.2f, 0, 10, false) },
        { nameof(HeavyAttackIntervalSeconds), (1.8f, 0, 10, false) },
        { nameof(MeleeRecoverySeconds), (0.65f, 0, 10, false) },
        { nameof(HeavyRecoverySeconds), (0.85f, 0, 10, false) },
        { nameof(MeleeStopDistance), (1.1f, 0.1f, 3, false) },
        { nameof(MeleeLaneTolerance), (0.5f, 0.05f, 3, false) },
        { nameof(WaitingDistance), (2.2f, 0.1f, 8, false) },
        { nameof(WaitingDepthOffset), (0.9f, 0, 3, false) },
        { nameof(WaitingMoveSpeed), (1f, 0, 5, false) },
        { nameof(WaitingPositionTolerance), (0.15f, 0.01f, 1, false) },
        { nameof(WaitingEngagementDistance), (5f, 0.1f, 12, false) },
        { nameof(SeparationDistance), (0.75f, 0, 3, false) },
        { nameof(SeparationStrength), (0.6f, 0, 3, false) }
    };
    private readonly Dictionary<string, float> _values;
    public static EnemyAiSettings Current { get; private set; } = CreateDefault();
    // 敵を生成した時点から数える待機時間です。出現前の予告・攻撃後の隙とは分けます。
    public float MeleeSpawnWaitSeconds => _values[nameof(MeleeSpawnWaitSeconds)];
    public float UndeadSpawnWaitSeconds => _values[nameof(UndeadSpawnWaitSeconds)];
    public float RangedSpawnWaitSeconds => _values[nameof(RangedSpawnWaitSeconds)];
    public float HeavySpawnWaitSeconds => _values[nameof(HeavySpawnWaitSeconds)];
    public float BossSpawnWaitSeconds => _values[nameof(BossSpawnWaitSeconds)];

    public int MaxSimultaneousAttackers => (int)_values[nameof(MaxSimultaneousAttackers)];
    public float AttackStartIntervalSeconds => _values[nameof(AttackStartIntervalSeconds)];
    public float MeleeWindupSeconds => _values[nameof(MeleeWindupSeconds)];
    public float HeavyWindupSeconds => _values[nameof(HeavyWindupSeconds)];
    public float MeleeAttackIntervalSeconds => _values[nameof(MeleeAttackIntervalSeconds)];
    public float HeavyAttackIntervalSeconds => _values[nameof(HeavyAttackIntervalSeconds)];
    public float MeleeRecoverySeconds => _values[nameof(MeleeRecoverySeconds)];
    public float HeavyRecoverySeconds => _values[nameof(HeavyRecoverySeconds)];
    public float MeleeStopDistance => _values[nameof(MeleeStopDistance)];
    public float MeleeLaneTolerance => _values[nameof(MeleeLaneTolerance)];

    public float WaitingDistance => _values[nameof(WaitingDistance)];
    public float WaitingDepthOffset => _values[nameof(WaitingDepthOffset)];
    public float WaitingMoveSpeed => _values[nameof(WaitingMoveSpeed)];
    public float WaitingPositionTolerance => _values[nameof(WaitingPositionTolerance)];
    public float WaitingEngagementDistance => _values[nameof(WaitingEngagementDistance)];

    public float SeparationDistance => _values[nameof(SeparationDistance)];
    public float SeparationStrength => _values[nameof(SeparationStrength)];

    private EnemyAiSettings(Dictionary<string, float> values)
    {
        _values = values;
    }

    public static EnemyAiSettings CreateDefault()
    {
        var values = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var rule in s_rules)
        {
            values.Add(rule.Key, rule.Value.Default);
        }
        return new EnemyAiSettings(values);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        // ドメインリロードを無効にしたPlayでも、前回の調整値を残しません。
        Current = CreateDefault();
        TextAsset asset = Resources.Load<TextAsset>("Balance/enemy_ai");
        if (asset == null)
        {
            Debug.LogWarning("敵AIのCSVがないため既定値を使用します。");
        }
        else if (TryParse(asset.text, out EnemyAiSettings settings, out string error))
        {
            Current = settings;
        }
        else
        {
            Debug.LogWarning("敵AIのCSVを適用できず既定値を使用します：" + error);
        }
    }

    public static bool TryParse(string csv, out EnemyAiSettings settings, out string error)
    {
        settings = CreateDefault();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "CSVが空です。";
            return false;
        }
        var values = new Dictionary<string, float>(settings._values, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool headerRead = false;
        string[] lines = csv.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            if (!headerRead)
            {
                if (line != "key,value")
                {
                    error = "先頭行はkey,valueにしてください。";
                    return false;
                }
                headerRead = true;
                continue;
            }
            string[] cells = line.Split(',');
            if (cells.Length != 2 || !s_rules.TryGetValue(cells[0].Trim(), out var rule)
                || !seen.Add(cells[0].Trim())
                || !float.TryParse(cells[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value) || value < rule.Min || value > rule.Max
                || (rule.Integer && value != Mathf.Floor(value)))
            {
                error = $"{i + 1}行目：項目名・重複・数値の範囲を確認してください。";
                return false;
            }
            values[cells[0].Trim()] = value;
        }
        if (!headerRead || seen.Count == 0)
        {
            error = "調整項目がありません。";
            return false;
        }
        if (values[nameof(WaitingEngagementDistance)] < values[nameof(WaitingDistance)]
            || values[nameof(WaitingEngagementDistance)] < values[nameof(MeleeStopDistance)])
        {
            error = "待機への参加距離は待機距離・接近停止距離以上にしてください。";
            return false;
        }
        // 省略した項目は既定値のまま。一行も失敗していない場合だけ全体を採用します。
        settings = new EnemyAiSettings(values);
        return true;
    }
}
