using UnityEngine;

// 出現予定を読むだけの表示。生成・カウントダウンはBeltBrawlerの予約処理に任せます。
public static class SpawnWarningHud
{
    private static GUIStyle s_style;

    public static void Draw(BeltBrawler game, Fighter player, Camera camera, Font font)
    {
        int visibleCount = 0;
        for (int i = 0; i < game.PendingSpawnCount; i++)
        {
            if (game.TryGetSpawnNotice(i, out _, out _, out _))
            {
                visibleCount++;
            }
        }
        if (visibleCount == 0)
        {
            return;
        }
        if (s_style == null)
        {
            s_style = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SpawnNoticeFontSize };
        }
        s_style.font = font;
        Color previousColor = GUI.color;
        float width = Mathf.Min(BrawlerBalance.SpawnNoticeWidth, Mathf.Max(0, Screen.width - BrawlerBalance.SpawnNoticeMargin * 2));
        float height = (visibleCount + 1) * BrawlerBalance.SpawnNoticeRowHeight;
        float x = Screen.width - width - BrawlerBalance.SpawnNoticeMargin;
        float y = Mathf.Max(BrawlerBalance.SpawnNoticeMargin, Screen.height * BrawlerBalance.SpawnNoticeTopFraction - height / 2);
        GUI.color = new Color(0, 0, 0, BrawlerBalance.SpawnNoticeBackgroundAlpha);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.yellow;
        GUI.Label(new Rect(x, y, width, BrawlerBalance.SpawnNoticeRowHeight), "増援予告", s_style);
        int row = 1;
        for (int i = 0; i < game.PendingSpawnCount; i++)
        {
            if (!game.TryGetSpawnNotice(i, out WaveSpawnSettings.EnemyKind kind, out Vector3 position, out float remaining))
            {
                continue;
            }
            string enemy = kind switch
            {
                WaveSpawnSettings.EnemyKind.Undead => "アンデッド",
                WaveSpawnSettings.EnemyKind.Ranged => "遠距離敵",
                WaveSpawnSettings.EnemyKind.Heavy => "重装敵",
                WaveSpawnSettings.EnemyKind.Boss => "ボス",
                _ => "近接敵"
            };
            // 予約時の左右オフセットでなく、現在のプレイヤーから見た出現地点の方向を示します。
            string direction = position.x < player.transform.position.x ? "< 左から"
                : position.x > player.transform.position.x ? "右から >" : "正面";
            GUI.Label(new Rect(x, y + row * BrawlerBalance.SpawnNoticeRowHeight, width, BrawlerBalance.SpawnNoticeRowHeight),
                $"{direction}　{enemy}　{remaining:F1}秒", s_style);
            row++;

            // 一覧で名前を分け、位置マーカー同士が近くても文字が重ならないようにします。
            Vector3 screen = camera.WorldToScreenPoint(position + Vector3.up * BrawlerBalance.SpawnNoticeMarkerHeight);
            float margin = BrawlerBalance.SpawnNoticeMargin;
            float markerX = Mathf.Clamp(screen.x, margin, Mathf.Max(margin, Screen.width - margin));
            float markerY = Mathf.Clamp(Screen.height - screen.y, margin, Mathf.Max(margin, Screen.height - margin));
            string marker = screen.x < margin ? "<" : screen.x > Screen.width - margin ? ">" : "!";
            GUI.Label(new Rect(markerX - BrawlerBalance.SpawnNoticeMarkerWidth / 2, markerY,
                BrawlerBalance.SpawnNoticeMarkerWidth, BrawlerBalance.SpawnNoticeRowHeight), marker, s_style);
        }
        GUI.color = previousColor;
    }
}
