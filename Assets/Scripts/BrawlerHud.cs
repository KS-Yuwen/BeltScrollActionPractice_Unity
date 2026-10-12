using System.Collections.Generic;
using UnityEngine;

// HUDの描画だけを担当します。進行・得点・HPの状態をこのクラスから変更しません。
public sealed class BrawlerHud
{
    private GUIStyle _style;
    private GUIStyle _healthStyle;

    private static void DrawBossStatus(BossFighter boss, GUIStyle style)
    {
        float width = BrawlerBalance.BossHudWidth;
        float x = Mathf.Max(BrawlerBalance.BossHudMargin, Screen.width - width - BrawlerBalance.BossHudMargin);
        float y = BrawlerBalance.BossHudMargin;
        Color previousColor = GUI.color;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width, BrawlerBalance.BossHudTitleHeight),
            $"WARDEN  {boss.Health}/{boss.MaxHealth}", style);
        y += BrawlerBalance.BossHudTitleHeight;
        GUI.color = Color.gray;
        GUI.DrawTexture(new Rect(x, y, width, BrawlerBalance.BossHudBarHeight), Texture2D.whiteTexture);
        GUI.color = new Color(0.8f, 0.25f, 0.65f);
        GUI.DrawTexture(new Rect(x, y, width * boss.Health / boss.MaxHealth, BrawlerBalance.BossHudBarHeight), Texture2D.whiteTexture);
        GUI.color = boss.IsRecovering ? Color.green : Color.yellow;
        string state = boss.IsRecovering ? $"OPENING {boss.RecoverySecondsRemaining:F1}s"
            : boss.IsWindingUp ? (boss.IsWideAttack ? "WIDE ATTACK" : "LONG STRIKE") : "BOSS";
        GUI.Label(new Rect(x, y + BrawlerBalance.BossHudBarHeight, width, BrawlerBalance.BossHudTitleHeight), state, style);
        GUI.color = previousColor;
    }

    public void Draw(BeltBrawler game, Fighter player, IReadOnlyList<Fighter> enemies,
        Camera camera, int wave, int score, int hitChain, HealthBarFeedback healthFeedback,
        IReadOnlyList<Vector3> pendingSpawns, float spawnWarningRemaining, Font font,
        WaveBanner waveBanner, HitDirectionIndicator hitDirection)
    {
        if (game.IsRunFinished)
        {
            StageResultHud.Draw(game.IsStageCleared, game.ResultWave, game.ResultScore, game.ResultDefeatedEnemies);
            return;
        }
        // OnGUIは同じフレームでも複数回呼ばれます。スタイルを毎回生成せず共有します。
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.HudFontSize };
            _healthStyle = new GUIStyle(_style)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = BrawlerBalance.PlayerHealthBarFontSize
            };
        }
        GUIStyle style = _style;
        if (game.IsPreparingBoss)
        {
            float top = BrawlerBalance.BossPreparationHudTop;
            float line = BrawlerBalance.BossPreparationHudLineHeight;
            GUI.Label(new Rect(BrawlerBalance.BossHudMargin, top, BrawlerBalance.BossPreparationHudWidth, line),
                "BOSS PREPARATION — Collect supplies and check your 6 slots", style);
            string button = BrawlerInput.IsGamepadConnected ? "Start / Options" : "Enter";
            GUI.Label(new Rect(BrawlerBalance.BossHudMargin, top + line, BrawlerBalance.BossPreparationHudWidth, line),
                $"{button}: Begin boss battle when ready", style);
        }
        GUI.Label(new Rect(24, 20, 700, 40), "NEON STREET  |  WAVE " + wave + "  |  ENEMIES " + enemies.Count, style);
        // 背景→遅れて縮む赤い残像→現在HPの順に重ね、HPゼロでも数値を表示します。
        var healthArea = new Rect(24, 65, BrawlerBalance.PlayerHealthBarWidth, BrawlerBalance.PlayerHealthBarHeight);
        GUI.color = new Color(0.12f, 0.12f, 0.15f);
        GUI.DrawTexture(healthArea, Texture2D.whiteTexture);
        float trailFraction = Mathf.Clamp01(Mathf.Max(player.Health, healthFeedback.TrailHealth) / player.MaxHealth);
        float healthFraction = Mathf.Clamp01((float)player.Health / player.MaxHealth);
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(healthArea.x, healthArea.y, healthArea.width * trailFraction, healthArea.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.1f, 0.7f, 1);
        GUI.DrawTexture(new Rect(healthArea.x, healthArea.y, healthArea.width * healthFraction, healthArea.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle healthStyle = _healthStyle;
        GUI.Label(healthArea, $"HP {player.Health} / {player.MaxHealth}", healthStyle);
        // 残像の長さではなく現在HPで判断します。回復・敗北したフレームで表示も切り替わります。
        if (player.Health > 0 && healthFraction <= BrawlerBalance.LowHealthWarningFraction)
        {
            float pulse = (1 + Mathf.Sin(Time.time * BrawlerBalance.LowHealthWarningPulsesPerSecond * Mathf.PI * 2)) / 2;
            GUI.color = Color.Lerp(new Color(0.45f, 0.12f, 0.08f), new Color(1, 0.35f, 0.05f), pulse);
            float border = BrawlerBalance.LowHealthWarningBorderWidth;
            // 外側だけを強調し、現在HP・赤い残像・HP数値を隠しません。
            GUI.DrawTexture(new Rect(healthArea.x - border, healthArea.y - border,
                healthArea.width + border * 2, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(healthArea.x - border, healthArea.yMax,
                healthArea.width + border * 2, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(healthArea.x - border, healthArea.y, border, healthArea.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(healthArea.xMax, healthArea.y, border, healthArea.height), Texture2D.whiteTexture);
            GUI.Label(new Rect(healthArea.xMax + BrawlerBalance.LowHealthWarningLabelGap, healthArea.y,
                BrawlerBalance.LowHealthWarningLabelWidth, healthArea.height), "LOW HP", healthStyle);
            GUI.color = Color.white;
        }
        GUI.Label(new Rect(24, 100, 700, 40), "SCORE " + score + "   |   " + hitChain + " HITS", style);
        string dashHint = BrawlerInput.IsGamepadConnected ? "RB / R1: Dash" : "Shift: Dash";
        if (player.IsRunning)
        {
            GUI.Label(new Rect(24, 360, 900, 40), "RUNNING — Attack / Jump, release direction to stop", style);
        }
        GUI.Label(new Rect(24, 138, 700, 40), player.DashReady ? $"DASH READY — {dashHint}" : "DASH RECHARGING", style);
        if (player.CanCounter)
        {
            GUI.Label(new Rect(24, 175, 700, 40), "COUNTER READY — Press Attack", style);
        }
        else if (game.FindGroundAttackTarget(player) != null)
        {
            GUI.Label(new Rect(24, 175, 700, 40), "GROUND ATTACK — Press Attack", style);
        }
        string controls = BrawlerInput.IsGamepadConnected
            ? "Stick: Move  X / Square: Attack  A / Cross: Jump  RB: Dash  LB / LT: Guard"
            : "WASD / Arrows: Move   J / Space: Attack   U: Jump   Shift: Dash   K: Guard   R: Restart";
        GUI.Label(new Rect(24, Screen.height - 48, 1100, 40), controls, style);
        if (player is ClericFighter cleric)
        {
            SelectionHud.Draw(cleric, font);
            string turnButton = BrawlerInput.IsGamepadConnected ? "X / Square + A / Cross" : "J / Space + U";
            GUI.Label(new Rect(24, 323, 1100, 40), cleric.TurnUndeadCooldownRemaining > 0
                ? $"TURN UNDEAD — {cleric.TurnUndeadCooldownRemaining:F1}s"
                : $"TURN UNDEAD READY — {turnButton}", style);
            string healButton = BrawlerInput.IsGamepadConnected ? "Y / Triangle" : "L";
            GUI.Label(new Rect(24, 212, 900, 40),
                $"HEAL {cleric.HealingUsesRemaining}/{BrawlerBalance.InventoryStackLimit} — {healButton}: +{BrawlerBalance.HealingAmount} HP"
                + (cleric.IsRecoveringFromHeal ? "  RECOVERING" : ""), style);
            string protectionButton = BrawlerInput.IsGamepadConnected ? "B / Circle" : "I";
            GUI.Label(new Rect(24, 249, 1000, 40),
                $"PROTECTION {cleric.ProtectionUsesRemaining}/{BrawlerBalance.InventoryStackLimit} — {protectionButton}"
                + (cleric.IsProtected ? $"  ACTIVE {cleric.ProtectionSecondsRemaining:F1}s" : $"  {BrawlerBalance.ProtectionSeconds}s: Damage ×{BrawlerBalance.ProtectionDamageMultiplier}"), style);
            GUI.Label(new Rect(24, 286, 1000, 40),
                $"ATTACK BOOST {cleric.AttackBoostUsesRemaining}/{BrawlerBalance.InventoryStackLimit} — Select + Use"
                + (cleric.IsAttackBoosted ? $"  ACTIVE {cleric.AttackBoostSecondsRemaining:F1}s"
                    : $"  {BrawlerBalance.AttackBoostSeconds}s: Melee ×{BrawlerBalance.AttackBoostDamageMultiplier}"), style);
        }
        // 敵の頭上に HP と攻撃予告を表示。ワールド座標を画面座標に変換します。
        // GUI の Y 軸は上から下、WorldToScreenPoint は下から上なので反転が必要です。
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.Health <= 0)
            {
                continue;
            }
            if (enemy is BossFighter boss)
            {
                DrawBossStatus(boss, style);
            }
            Vector3 screen = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * BrawlerBalance.EnemyHealthBarWorldHeight);
            if (screen.z <= 0)
            {
                continue;
            }
            float y = Screen.height - screen.y;
            var enemyHealthArea = new Rect(screen.x - BrawlerBalance.EnemyHealthBarWidth / 2, y,
                BrawlerBalance.EnemyHealthBarWidth, BrawlerBalance.EnemyHealthBarHeight);
            GUI.color = new Color(0.12f, 0.12f, 0.15f);
            GUI.DrawTexture(enemyHealthArea, Texture2D.whiteTexture);
            GUI.color = Color.red;
            float enemyTrailFraction = Mathf.Clamp01(Mathf.Max(enemy.Health, enemy.EnemyHealthTrail) / enemy.MaxHealth);
            GUI.DrawTexture(new Rect(enemyHealthArea.x, y, enemyHealthArea.width * enemyTrailFraction, enemyHealthArea.height), Texture2D.whiteTexture);
            // 現在HPは橙色、予告中は黄色。赤い残像と色を分けて削った量を見せます。
            GUI.color = enemy.IsWindingUp ? Color.yellow : new Color(1, 0.65f, 0.2f);
            GUI.DrawTexture(new Rect(enemyHealthArea.x, y, enemyHealthArea.width * enemy.Health / enemy.MaxHealth,
                enemyHealthArea.height), Texture2D.whiteTexture);
            if (enemy.IsHammerStunned)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(screen.x - BrawlerBalance.HammerStunLabelWidth / 2,
                    y - BrawlerBalance.HammerStunLabelHeight, BrawlerBalance.HammerStunLabelWidth,
                    BrawlerBalance.HammerStunLabelHeight), "STUN", style);
            }
            else if (enemy.IsWindingUp)
            {
                GUI.Label(new Rect(screen.x - 12, y - 35, 40, 35), "!", style);
            }
        }
        GUI.color = Color.white;
        if (!BrawlerInput.HasInputDevice)
        {
            GUI.Label(new Rect(24, 180, 1100, 40), "No input devices — Apply Input Settings and Restart from Tools / Neon Street", style);
        }
        if (pendingSpawns.Count > 0)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(24, 286, 900, 40), $"ENEMIES INCOMING — {spawnWarningRemaining:F1}s", style);
            foreach (Vector3 position in pendingSpawns)
            {
                Vector3 screen = camera.WorldToScreenPoint(position + Vector3.up * 0.3f);
                // 画面外の出現も端の矢印で知らせます。画面内では実際の出現位置に表示します。
                bool offLeft = screen.x < 70;
                bool offRight = screen.x > Screen.width - 70;
                float x = Mathf.Clamp(screen.x, 70, Mathf.Max(70, Screen.width - 70));
                float y = Mathf.Clamp(Screen.height - screen.y, 335, Mathf.Max(335, Screen.height - 90));
                GUI.Label(new Rect(x - 60, y, 160, 40), offLeft ? "< INCOMING" : offRight ? "INCOMING >" : "! SPAWN", style);
            }
            GUI.color = Color.white;
        }
        if (player.Health > 0)
        {
            waveBanner.Draw();
        }
        hitDirection.Draw(player, camera);
    }
}
