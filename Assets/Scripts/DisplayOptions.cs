using UnityEngine;

// 表示の好みを管理します。今後のオプション画面はこのプロパティを切り替えるだけで反映できます。
// ゲームの回数・使用可否とは独立し、表示を無効にしても戦闘の処理は変わりません。
public static class DisplayOptions
{
    private const string LowSpellUsesKey = "Display.ShowLowSpellUsesWarning";
    private const string DepletedSpellKey = "Display.ShowSpellDepletedNotice";
    private const string CameraShakeKey = "Display.CameraShakeEnabled";
    private const int EnabledValue = 1;
    private const int DisabledValue = 0;

    // それぞれ初期値はオン。PlayerPrefsを使い、リスタート・次回起動でも選択を保持します。
    public static bool ShowLowSpellUsesWarning
    {
        get => PlayerPrefs.GetInt(LowSpellUsesKey, EnabledValue) == EnabledValue;
        set => Save(LowSpellUsesKey, value);
    }

    public static bool ShowSpellDepletedNotice
    {
        get => PlayerPrefs.GetInt(DepletedSpellKey, EnabledValue) == EnabledValue;
        set => Save(DepletedSpellKey, value);
    }

    // 初期値は従来通りオンです。戦闘の命中判定やヒットストップには影響させません。
    public static bool CameraShakeEnabled
    {
        get => PlayerPrefs.GetInt(CameraShakeKey, EnabledValue) == EnabledValue;
        set => Save(CameraShakeKey, value);
    }

    private static void Save(string key, bool enabled)
    {
        // 保存は設定を変更したときだけ行い、HUDの描画ごとには書き込みません。
        PlayerPrefs.SetInt(key, enabled ? EnabledValue : DisabledValue);
        PlayerPrefs.Save();
    }
}
