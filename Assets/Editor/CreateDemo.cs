using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor フォルダ内のコードは Unity エディター専用で、ゲームのビルドには含まれません。
// 試作シーンを自動生成するための補助ツールです。
public static class CreateDemo
{
    // バッチ実行の -executeMethod CreateDemo.BuildScene から呼べる static メソッドです。
    // 実行すると同名のシーンを保存し直し、ビルド対象シーン一覧もこのシーンに置き換えます。
    public static void BuildScene()
    {
        // 空のシーンに切り替えます。床やキャラクターは BeltBrawler.Start で生成します。
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Belt Brawler").AddComponent<BeltBrawler>();
        // 保存先のフォルダが存在しない場合だけ作成します。
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/NeonStreet.unity");
        // シーンを有効なビルド対象として登録し、R キーでの再読み込みを可能にします。
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/NeonStreet.unity", true) };
        AssetDatabase.SaveAssets();
    }
}
