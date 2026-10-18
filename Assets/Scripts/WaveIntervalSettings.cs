using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// ウェーブを全滅させた後の間隔を管理します。敵1体ごとの出現遅延とは別の設定です。
public sealed class WaveIntervalSettings
{
    private const string Header = "wave,clearDelaySeconds";
    private const int ColumnCount = 2;
    private const string ResourcePath = "Balance/wave_intervals";
    private readonly float[] _clearDelays;
    public static WaveIntervalSettings Current { get; private set; } = CreateDefault();

    private WaveIntervalSettings(float[] clearDelays)
    {
        _clearDelays = clearDelays;
    }

    // 引数は「これから出す番号」ではなく「全滅した番号」です。最終ウェーブには呼びません。
    public float GetClearDelaySeconds(int clearedWave) => _clearDelays[clearedWave];

    public static WaveIntervalSettings CreateDefault()
    {
        var delays = new float[BrawlerBalance.StageWaveCount];
        for (int wave = 1; wave < BrawlerBalance.StageWaveCount; wave++)
        {
            delays[wave] = BrawlerBalance.NextWaveDelaySeconds;
        }
        return new WaveIntervalSettings(delays);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        // ドメインリロード無効時も、Play開始ごとに読み直します。
        Current = CreateDefault();
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning("ウェーブ間隔CSVがないため既定の待ち時間を使用します。");
        }
        else if (TryParse(asset.text, out WaveIntervalSettings settings, out string error))
        {
            Current = settings;
        }
        else
        {
            Debug.LogWarning("ウェーブ間隔CSVを適用できず全体を既定値へ戻します：" + error);
        }
    }

    public static bool TryParse(string csv, out WaveIntervalSettings settings, out string error)
    {
        settings = CreateDefault();
        error = string.Empty;
        var delays = new float[BrawlerBalance.StageWaveCount];
        var seen = new HashSet<int>();
        bool headerRead = false;
        string[] lines = (csv ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            if (!headerRead)
            {
                if (line != Header)
                {
                    error = "列名を" + Header + "にしてください。";
                    return false;
                }
                headerRead = true;
                continue;
            }
            string[] cells = line.Split(',');
            if (cells.Length != ColumnCount
                || !int.TryParse(cells[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int wave)
                || wave < 1 || wave >= BrawlerBalance.StageWaveCount || !seen.Add(wave)
                || !float.TryParse(cells[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float delay)
                || float.IsNaN(delay) || float.IsInfinity(delay)
                || delay < 0 || delay > BrawlerBalance.MaxWaveClearDelaySeconds)
            {
                error = $"{i + 1}行目：列数・番号・重複・待ち時間の範囲を確認してください。";
                return false;
            }
            delays[wave] = delay;
        }
        // 一部の行だけを採用せず、全区間を定義できた場合だけ適用します。
        if (!headerRead || seen.Count != BrawlerBalance.StageWaveCount - 1)
        {
            error = "最終ウェーブを除く全ウェーブの待ち時間を指定してください。";
            return false;
        }
        settings = new WaveIntervalSettings(delays);
        return true;
    }
}
