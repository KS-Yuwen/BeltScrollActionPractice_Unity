using UnityEngine;

// 魔法・所持品の選択表示。入力や所持数を変更せず、クレリックの状態を読み取って描画します。
public static class SelectionHud
{
    // 選択操作と同じ循環順。番号に依存せず、各項目の名前で状態を取得します。
    private static readonly BrawlerDefines.UsableAction[] s_actions =
    {
        BrawlerDefines.UsableAction.Healing,
        BrawlerDefines.UsableAction.Protection,
        BrawlerDefines.UsableAction.HealingPotion,
        BrawlerDefines.UsableAction.ThrowingKnife
    };
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_countStyle;
    private static GUIStyle s_hintStyle;

    public static void Draw(ClericFighter cleric)
    {
        // GUI.skinの参照はOnGUIから呼ばれた時だけ行い、スタイルを毎回作り直しません。
        if (s_titleStyle == null)
        {
            s_titleStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SelectionTitleFontSize, fontStyle = FontStyle.Bold };
            s_countStyle = new GUIStyle(s_titleStyle) { fontSize = BrawlerBalance.SelectionCountFontSize };
            s_hintStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SelectionTitleFontSize };
        }
        float width = s_actions.Length * BrawlerBalance.SelectionCardWidth
            + (s_actions.Length - 1) * BrawlerBalance.SelectionCardGap;
        float height = BrawlerBalance.SelectionCardHeight + BrawlerBalance.SelectionHintHeight * 2;
        float scale = Mathf.Min(1, Mathf.Max(0, Screen.width - BrawlerBalance.SelectionHudMargin * 2) / width);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector3 origin = new Vector3(BrawlerBalance.SelectionHudMargin,
            Screen.height - BrawlerBalance.SelectionBottomOffset - height * scale, 0);
        GUI.matrix = previousMatrix * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
        for (int i = 0; i < s_actions.Length; i++)
        {
            DrawCard(cleric, s_actions[i], i * (BrawlerBalance.SelectionCardWidth + BrawlerBalance.SelectionCardGap));
        }
        GUI.color = Color.white;
        string hint = BrawlerInput.IsGamepadConnected ? "View / Share: Select    RT / R2: Use" : "Q: Select    E: Use";
        GUI.Label(new Rect(0, BrawlerBalance.SelectionCardHeight, width, BrawlerBalance.SelectionHintHeight), hint, s_hintStyle);
        BrawlerDefines.ActionUseStatus status = cleric.SelectedActionUseStatus;
        string reason = status switch
        {
            BrawlerDefines.ActionUseStatus.Ready => "READY",
            BrawlerDefines.ActionUseStatus.Empty => "EMPTY",
            BrawlerDefines.ActionUseStatus.HealthFull => "HP FULL",
            BrawlerDefines.ActionUseStatus.ProtectionActive => "PROTECTION ACTIVE",
            BrawlerDefines.ActionUseStatus.Busy => "BUSY",
            BrawlerDefines.ActionUseStatus.Airborne => "AIRBORNE",
            BrawlerDefines.ActionUseStatus.Defeated => "DEFEATED",
            _ => "UNAVAILABLE"
        };
        GUI.color = status == BrawlerDefines.ActionUseStatus.Ready ? Color.green : Color.yellow;
        GUI.Label(new Rect(0, BrawlerBalance.SelectionCardHeight + BrawlerBalance.SelectionHintHeight,
            width, BrawlerBalance.SelectionHintHeight), "SELECTED: " + reason, s_hintStyle);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static void DrawCard(ClericFighter cleric, BrawlerDefines.UsableAction action, float x)
    {
        bool selected = cleric.SelectedAction == action;
        (string title, int count, int capacity) = action switch
        {
            BrawlerDefines.UsableAction.Healing => ("HEAL", cleric.HealingUsesRemaining, BrawlerBalance.HealingUses),
            BrawlerDefines.UsableAction.Protection => ("PROTECTION", cleric.ProtectionUsesRemaining, BrawlerBalance.ProtectionUses),
            BrawlerDefines.UsableAction.HealingPotion => ("POTION", cleric.HealingPotionsHeld, BrawlerBalance.PotionCapacity),
            _ => ("KNIFE", cleric.ThrowingKnivesHeld, BrawlerBalance.KnifeCapacity)
        };
        var card = new Rect(x, 0, BrawlerBalance.SelectionCardWidth, BrawlerBalance.SelectionCardHeight);
        GUI.color = selected ? new Color(0.12f, 0.25f, 0.35f, 0.95f) : new Color(0.05f, 0.07f, 0.1f, 0.9f);
        GUI.DrawTexture(card, Texture2D.whiteTexture);
        GUI.color = selected ? Color.yellow : Color.gray;
        float border = BrawlerBalance.SelectionBorderWidth;
        // 選択項目は色だけでなく枠と「>」でも示し、残りゼロでも選択位置が分かるようにします。
        GUI.DrawTexture(new Rect(x, 0, card.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, card.height - border, card.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, 0, border, card.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + card.width - border, 0, border, card.height), Texture2D.whiteTexture);
        float padding = BrawlerBalance.SelectionCardPadding;
        GUI.color = selected ? Color.yellow : count > 0 ? Color.white : Color.gray;
        GUI.Label(new Rect(x + padding, padding, card.width - padding * 2, card.height / 2),
            (selected ? "> " : "") + title, s_titleStyle);
        GUI.Label(new Rect(x + padding, card.height / 2, card.width - padding * 2, card.height / 2),
            $"{count} / {capacity}", s_countStyle);
    }
}
