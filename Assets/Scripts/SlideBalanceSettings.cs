using System;
using System.Collections.Generic;
using System.Globalization;

// CSVを検証してから作る読み取り専用の設定。読み込み途中の値をゲームへ公開しません。
public sealed class SlideBalanceSettings
{
    public float DirectionWindowSeconds { get; }
    public float ButtonWindowSeconds { get; }
    public float DirectionThreshold { get; }
    public float Speed { get; }
    public float MinSeconds { get; }
    public float MaxSeconds { get; }
    public float CooldownSeconds { get; }
    public int Damage { get; }
    // 同じ方向コマンドで出す技なので、ダッシュ必殺技も同じCSVで管理します。
    public int DashSpecialDamage { get; }
    public float DashSpecialSpeed { get; }
    public float DashSpecialMovementSeconds { get; }
    public float DashSpecialAttackSeconds { get; }
    public float DashSpecialCooldownSeconds { get; }
    public float DashSpecialHitDelaySeconds { get; }
    public int UppercutDamage { get; }
    public float UppercutDirectionWindowSeconds { get; }
    public float UppercutButtonWindowSeconds { get; }
    public float UppercutAttackSeconds { get; }
    public float UppercutCooldownSeconds { get; }
    public float UppercutHitDelaySeconds { get; }
    public float UppercutReach { get; }

    public int HeavyStrikeDamage { get; }
    public float HeavyStrikeAttackSeconds { get; }
    public float HeavyStrikeCooldownSeconds { get; }
    public float HeavyStrikeHitDelaySeconds { get; }
    public float HeavyStrikeReach { get; }
    public int PushDamage { get; }
    public float PushHoldSeconds { get; }
    public float PushAttackSeconds { get; }
    public float PushCooldownSeconds { get; }
    public float PushHitDelaySeconds { get; }
    public float PushReach { get; }
    public float PushKnockbackSpeed { get; }

    private SlideBalanceSettings(Dictionary<string, float> values)
    {
        DirectionWindowSeconds = values["SlideDirectionWindowSeconds"];
        ButtonWindowSeconds = values["SlideButtonWindowSeconds"];
        DirectionThreshold = values["CommandDirectionThreshold"];
        Speed = values["SlideSpeed"];
        MinSeconds = values["SlideMinSeconds"];
        MaxSeconds = values["SlideMaxSeconds"];
        CooldownSeconds = values["SlideCooldownSeconds"];
        Damage = (int)values["SlideDamage"];
        DashSpecialDamage = (int)values["DashSpecialDamage"];
        DashSpecialSpeed = values["DashSpecialSpeed"];
        DashSpecialMovementSeconds = values["DashSpecialMovementSeconds"];
        DashSpecialAttackSeconds = values["DashSpecialAttackSeconds"];
        DashSpecialCooldownSeconds = values["DashSpecialCooldownSeconds"];
        DashSpecialHitDelaySeconds = values["DashSpecialHitDelaySeconds"];
        UppercutDamage = (int)values["UppercutDamage"];
        UppercutDirectionWindowSeconds = values["UppercutDirectionWindowSeconds"];
        UppercutButtonWindowSeconds = values["UppercutButtonWindowSeconds"];
        UppercutAttackSeconds = values["UppercutAttackSeconds"];
        UppercutCooldownSeconds = values["UppercutCooldownSeconds"];
        UppercutHitDelaySeconds = values["UppercutHitDelaySeconds"];
        UppercutReach = values["UppercutReach"];
        HeavyStrikeDamage = (int)values["HeavyStrikeDamage"];
        HeavyStrikeAttackSeconds = values["HeavyStrikeAttackSeconds"];
        HeavyStrikeCooldownSeconds = values["HeavyStrikeCooldownSeconds"];
        HeavyStrikeHitDelaySeconds = values["HeavyStrikeHitDelaySeconds"];
        HeavyStrikeReach = values["HeavyStrikeReach"];
        PushDamage = (int)values["PushDamage"];
        PushHoldSeconds = values["PushHoldSeconds"];
        PushAttackSeconds = values["PushAttackSeconds"];
        PushCooldownSeconds = values["PushCooldownSeconds"];
        PushHitDelaySeconds = values["PushHitDelaySeconds"];
        PushReach = values["PushReach"];
        PushKnockbackSpeed = values["PushKnockbackSpeed"];
    }

    private static Dictionary<string, float> CreateDefaults()
    {
        return new Dictionary<string, float>(StringComparer.Ordinal)
        {
            { "SlideDirectionWindowSeconds", BrawlerBalance.DefaultSlideDirectionWindowSeconds },
            { "SlideButtonWindowSeconds", BrawlerBalance.DefaultSlideButtonWindowSeconds },
            { "CommandDirectionThreshold", BrawlerBalance.DefaultCommandDirectionThreshold },
            { "SlideSpeed", BrawlerBalance.DefaultSlideSpeed },
            { "SlideMinSeconds", BrawlerBalance.DefaultSlideMinSeconds },
            { "SlideMaxSeconds", BrawlerBalance.DefaultSlideMaxSeconds },
            { "SlideCooldownSeconds", BrawlerBalance.DefaultSlideCooldownSeconds },
            { "SlideDamage", BrawlerBalance.DefaultSlideDamage },
            { "DashSpecialDamage", BrawlerBalance.DefaultDashSpecialDamage },
            { "DashSpecialSpeed", BrawlerBalance.DefaultDashSpecialSpeed },
            { "DashSpecialMovementSeconds", BrawlerBalance.DefaultDashSpecialMovementSeconds },
            { "DashSpecialAttackSeconds", BrawlerBalance.DefaultDashSpecialAttackSeconds },
            { "DashSpecialCooldownSeconds", BrawlerBalance.DefaultDashSpecialCooldownSeconds },
            { "DashSpecialHitDelaySeconds", BrawlerBalance.DefaultDashSpecialHitDelaySeconds },
            { "UppercutDamage", BrawlerBalance.DefaultUppercutDamage },
            { "UppercutDirectionWindowSeconds", BrawlerBalance.DefaultUppercutDirectionWindowSeconds },
            { "UppercutButtonWindowSeconds", BrawlerBalance.DefaultUppercutButtonWindowSeconds },
            { "UppercutAttackSeconds", BrawlerBalance.DefaultUppercutAttackSeconds },
            { "UppercutCooldownSeconds", BrawlerBalance.DefaultUppercutCooldownSeconds },
            { "UppercutHitDelaySeconds", BrawlerBalance.DefaultUppercutHitDelaySeconds },
            { "UppercutReach", BrawlerBalance.DefaultUppercutReach },
            { "HeavyStrikeDamage", BrawlerBalance.DefaultHeavyStrikeDamage },
            { "HeavyStrikeAttackSeconds", BrawlerBalance.DefaultHeavyStrikeAttackSeconds },
            { "HeavyStrikeCooldownSeconds", BrawlerBalance.DefaultHeavyStrikeCooldownSeconds },
            { "HeavyStrikeHitDelaySeconds", BrawlerBalance.DefaultHeavyStrikeHitDelaySeconds },
            { "HeavyStrikeReach", BrawlerBalance.DefaultHeavyStrikeReach },
            { "PushDamage", BrawlerBalance.DefaultPushDamage },
            { "PushHoldSeconds", BrawlerBalance.DefaultPushHoldSeconds },
            { "PushAttackSeconds", BrawlerBalance.DefaultPushAttackSeconds },
            { "PushCooldownSeconds", BrawlerBalance.DefaultPushCooldownSeconds },
            { "PushHitDelaySeconds", BrawlerBalance.DefaultPushHitDelaySeconds },
            { "PushReach", BrawlerBalance.DefaultPushReach },
            { "PushKnockbackSpeed", BrawlerBalance.DefaultPushKnockbackSpeed }
        };
    }

    public static SlideBalanceSettings CreateDefault() => new SlideBalanceSettings(CreateDefaults());

    public static bool TryParse(string csv, out SlideBalanceSettings settings, out string error)
    {
        // 失敗時は全項目を既定値へ戻します。正常な行だけ混ぜると調整ミスを見落としやすいためです。
        settings = CreateDefault();
        error = null;
        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "CSVが空です。";
            return false;
        }
        Dictionary<string, float> values = CreateDefaults();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        string[] lines = csv.TrimStart('\uFEFF').Replace("\r", "").Split('\n');
        bool hasHeader = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            if (!hasHeader)
            {
                hasHeader = true;
                if (line == "key,value")
                {
                    continue;
                }
                error = $"{i + 1}行目：ヘッダーは key,value にしてください。";
                return false;
            }
            string[] columns = line.Split(',');
            string key = columns[0].Trim();
            if (columns.Length != 2 || !values.ContainsKey(key) || !seenKeys.Add(key))
            {
                error = $"{i + 1}行目：列数・設定名・重複を確認してください。";
                return false;
            }
            if (!float.TryParse(columns[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value) || !IsInRange(key, value))
            {
                error = $"{i + 1}行目：{key} の値が不正です。";
                return false;
            }
            values[key] = value;
        }
        if (seenKeys.Count == 0 || values["SlideMinSeconds"] > values["SlideMaxSeconds"])
        {
            error = "設定行がないか、最短時間が最長時間を超えています。";
            return false;
        }
        if (values["DashSpecialMovementSeconds"] > values["DashSpecialAttackSeconds"]
            || values["DashSpecialHitDelaySeconds"] >= values["DashSpecialAttackSeconds"]
            || values["DashSpecialCooldownSeconds"] < values["DashSpecialAttackSeconds"])
        {
            error = "ダッシュ必殺技は移動・命中時刻が攻撃時間内に収まり、再使用待ちは攻撃時間以上にしてください。";
            return false;
        }
        if (values["UppercutHitDelaySeconds"] >= values["UppercutAttackSeconds"]
            || values["UppercutCooldownSeconds"] < values["UppercutAttackSeconds"])
        {
            error = "対空必殺技の命中時刻は攻撃時間内、再使用待ちは攻撃時間以上にしてください。";
            return false;
        }
        foreach (string prefix in new string[] { "HeavyStrike", "Push" })
        {
            if (values[prefix + "HitDelaySeconds"] >= values[prefix + "AttackSeconds"]
                || values[prefix + "CooldownSeconds"] < values[prefix + "AttackSeconds"])
            {
                error = prefix + " の命中時刻と再使用待ちを確認してください。";
                return false;
            }
        }
        settings = new SlideBalanceSettings(values);
        return true;
    }

    private static bool IsInRange(string key, float value)
    {
        // 想定外の高速移動や、0秒・負数・小数ダメージを読み込み時に拒否します。
        switch (key)
        {
            case "CommandDirectionThreshold":
                return value >= 0.05f && value <= 0.9f;
            case "SlideSpeed":
            case "DashSpecialSpeed":
            case "PushKnockbackSpeed":
                return value >= 0.1f && value <= 30;
            case "SlideDamage":
            case "DashSpecialDamage":
            case "UppercutDamage":
            case "HeavyStrikeDamage":
            case "PushDamage":
                return value >= 1 && value <= 1000 && value == Math.Floor(value);
            default:
                return value >= 0.01f && value <= 3;
        }
    }
}
