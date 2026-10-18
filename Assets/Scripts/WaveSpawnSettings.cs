using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 1行1体の編成表。公開後は変更せず、波の途中でCSVの値や出現位置が動かないようにします。
public sealed class WaveSpawnSettings
{
    public enum EnemyKind
    {
        Melee,
        Undead,
        Ranged,
        Heavy,
        Boss
    }

    public sealed class Entry
    {
        public EnemyKind Kind { get; }
        public float OffsetX { get; }
        public float LaneZ { get; }
        public float DelaySeconds { get; }

        internal Entry(EnemyKind kind, float offsetX, float laneZ, float delaySeconds)
        {
            Kind = kind;
            OffsetX = offsetX;
            LaneZ = laneZ;
            DelaySeconds = delaySeconds;
        }

        public Vector3 ResolvePosition(Vector3 playerPosition)
        {
            float x = Mathf.Clamp(playerPosition.x + OffsetX, -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth);
            // 端で出現位置が近すぎた場合は反対側へ回します。奥行きはCSVの絶対座標です。
            if (Mathf.Abs(x - playerPosition.x) < BrawlerBalance.SpawnMinimumDistance)
            {
                x = Mathf.Clamp(playerPosition.x - OffsetX, -BrawlerBalance.StageHalfWidth, BrawlerBalance.StageHalfWidth);
            }
            return new Vector3(x, 0, LaneZ);
        }
    }

    private const string Header = "wave,enemy,offsetX,laneZ,delaySeconds";
    private const int ColumnCount = 5;
    private readonly Dictionary<int, IReadOnlyList<Entry>> _waves;
    public static WaveSpawnSettings Current { get; private set; } = CreateDefault();

    private WaveSpawnSettings(Dictionary<int, List<Entry>> waves)
    {
        _waves = new Dictionary<int, IReadOnlyList<Entry>>();
        foreach (var wave in waves)
        {
            _waves.Add(wave.Key, wave.Value.AsReadOnly());
        }
    }

    public IReadOnlyList<Entry> GetWave(int wave) => _waves[wave];

    public static WaveSpawnSettings CreateDefault()
    {
        var waves = new Dictionary<int, List<Entry>>();
        for (int wave = 1; wave <= BrawlerBalance.StageWaveCount; wave++)
        {
            bool bossWave = wave == BrawlerBalance.StageWaveCount;
            int count = bossWave ? BrawlerBalance.BossWaveEnemyCount
                : Mathf.Min(BrawlerBalance.WaveBaseEnemyCount + wave, BrawlerBalance.WaveMaxEnemyCount);
            var entries = new List<Entry>();
            for (int i = 0; i < count; i++)
            {
                EnemyKind kind = bossWave ? EnemyKind.Boss
                    : wave >= BrawlerBalance.MixedEnemiesStartWave && i == BrawlerBalance.HeavySpawnIndex ? EnemyKind.Heavy
                    : i % BrawlerBalance.RangedSpawnInterval == BrawlerBalance.RangedSpawnOffset ? EnemyKind.Ranged
                    : i == BrawlerBalance.UndeadSpawnIndex ? EnemyKind.Undead : EnemyKind.Melee;
                float side = wave >= BrawlerBalance.MixedEnemiesStartWave && i % 2 == 1 ? -1 : 1;
                // 不正CSVで戻る場合も、編成・位置を再現できる固定の初期データを使います。
                float lane = (i % BrawlerBalance.DefaultSpawnLaneCount - 1) * BrawlerBalance.DefaultSpawnLaneSpacing;
                entries.Add(new Entry(kind, side * (BrawlerBalance.SpawnBaseDistance + i), lane, 0));
            }
            waves.Add(wave, entries);
        }
        return new WaveSpawnSettings(waves);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Current = CreateDefault();
        TextAsset asset = Resources.Load<TextAsset>("Balance/wave_spawns");
        if (asset == null)
        {
            Debug.LogWarning("ウェーブ編成CSVがないため既定の編成を使用します。");
        }
        else if (TryParse(asset.text, out WaveSpawnSettings settings, out string error))
        {
            Current = settings;
        }
        else
        {
            Debug.LogWarning("ウェーブ編成CSVを適用できず全体を既定値へ戻します：" + error);
        }
    }

    public static bool TryParse(string csv, out WaveSpawnSettings settings, out string error)
    {
        settings = CreateDefault();
        error = string.Empty;
        var waves = new Dictionary<int, List<Entry>>();
        var seen = new HashSet<(int, EnemyKind, float, float, float)>();
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
                || wave < 1 || wave > BrawlerBalance.StageWaveCount
                || !Enum.TryParse(cells[1].Trim(), false, out EnemyKind kind) || !Enum.IsDefined(typeof(EnemyKind), kind)
                || cells[1].Trim() != kind.ToString()
                || !TryFloat(cells[2], out float offset) || Mathf.Abs(offset) < BrawlerBalance.SpawnMinimumDistance
                || Mathf.Abs(offset) > BrawlerBalance.StageHalfWidth * 2
                || !TryFloat(cells[3], out float lane) || Mathf.Abs(lane) > BrawlerBalance.StageHalfDepth
                || !TryFloat(cells[4], out float delay) || delay < 0 || delay > BrawlerBalance.MaxSpawnDelaySeconds
                || !seen.Add((wave, kind, offset, lane, delay)))
            {
                error = $"{i + 1}行目：列数・番号・敵種別・座標・遅延・重複を確認してください。";
                return false;
            }
            if (!waves.TryGetValue(wave, out List<Entry> entries))
            {
                entries = new List<Entry>();
                waves.Add(wave, entries);
            }
            entries.Add(new Entry(kind, offset, lane, delay));
            if (entries.Count > BrawlerBalance.WaveMaxEnemyCount
                || (kind == EnemyKind.Boss) != (wave == BrawlerBalance.StageWaveCount))
            {
                error = $"{i + 1}行目：人数上限、または最終ウェーブのボス配置が不正です。";
                return false;
            }
        }
        if (!headerRead || waves.Count != BrawlerBalance.StageWaveCount
            || waves[BrawlerBalance.StageWaveCount].Count != BrawlerBalance.BossWaveEnemyCount)
        {
            error = "全ウェーブに1体以上、最終ウェーブにはボスを1体指定してください。";
            return false;
        }
        settings = new WaveSpawnSettings(waves);
        return true;
    }

    private static bool TryFloat(string text, out float value)
    {
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
