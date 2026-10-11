using UnityEngine;

// 結果の描画だけを担当します。クリア・敗北時に確定した値を受け取り、進行や得点を変更しません。
public static class StageResultHud
{
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_bodyStyle;

    public static void Draw(bool cleared, int reachedWave, int score, int defeatedEnemies)
    {
        if (s_titleStyle == null)
        {
            s_titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = BrawlerBalance.ResultTitleFontSize
            };
            s_bodyStyle = new GUIStyle(s_titleStyle) { fontSize = BrawlerBalance.ResultBodyFontSize };
        }
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        float width = BrawlerBalance.ResultPanelWidth;
        float height = BrawlerBalance.ResultPanelHeight;
        float scale = Mathf.Min(1, Screen.width / (width + BrawlerBalance.ResultPanelMargin * 2),
            Screen.height / (height + BrawlerBalance.ResultPanelMargin * 2));
        GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
            (Screen.height - height * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
        GUI.color = new Color(0.06f, 0.12f, 0.2f);
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
        float row = BrawlerBalance.ResultRowHeight;
        float top = BrawlerBalance.ResultPanelMargin;
        GUI.color = cleared ? Color.yellow : Color.red;
        GUI.Label(new Rect(0, top, width, row), cleared ? "STAGE CLEAR" : "DEFEATED", s_titleStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, top + row, width, row), $"WAVES {reachedWave} / {BrawlerBalance.StageWaveCount}", s_bodyStyle);
        GUI.Label(new Rect(0, top + row * 2, width, row), "SCORE  " + score, s_bodyStyle);
        GUI.Label(new Rect(0, top + row * 3, width, row), "DEFEATED  " + defeatedEnemies, s_bodyStyle);
        string restart = BrawlerInput.IsGamepadConnected ? "Start" : "R";
        GUI.Label(new Rect(0, top + row * 4, width, row), $"Press {restart} to restart", s_bodyStyle);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }
}
