using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 外部から ProjectSettings.asset を変更しても、起動済みエディターの入力バックエンドは
// 切り替わりません。Unity 自身の設定 API で適用し、保存後に再起動するためのツールです。
public static class BrawlerInputSetup
{
    [MenuItem("Tools/Neon Street/Apply Input Settings and Restart")]
    public static void ApplyAndRestart()
    {
        // 再起動前に編集中のシーンを保存する機会を設けます。キャンセル時は再起動しません。
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        PlayerSettings[] playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
        if (playerSettings.Length == 0)
        {
            SettingsService.OpenProjectSettings("Project/Player");
            Debug.LogError("Player Settings を開きました。もう一度このメニューを実行してください。");
            return;
        }

        // グローバル設定と、ロード済みビルドプロファイルの設定を同じ入力方式にそろえます。
        foreach (PlayerSettings playerSetting in playerSettings)
        {
            var settings = new SerializedObject(playerSetting);
            SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                Debug.LogError("Active Input Handling が見つかりません。Player Settings から Input System Package (New) を選択してください。");
                return;
            }
            inputHandler.intValue = 1;
            settings.ApplyModifiedProperties();
        }
        AssetDatabase.SaveAssets();
        // ソースの再コンパイルだけでは実機デバイス用バックエンドは有効にならないため再起動します。
        EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
    }
}
