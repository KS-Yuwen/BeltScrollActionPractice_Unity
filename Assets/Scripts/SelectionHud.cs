using UnityEngine;

// 魔法・所持品の選択表示。入力や所持数を変更せず、クレリックの状態を読み取って描画します。
public static class SelectionHud
{
    // 表示内容は固定リストではなく、実際の6スロットから取得します。
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_countStyle;
    private static GUIStyle s_hintStyle;
    private static GUIStyle s_statusStyle;

    public static void Draw(ClericFighter cleric, Font statusFont)
    {
        // GUI.skinの参照はOnGUIから呼ばれた時だけ行い、スタイルを毎回作り直しません。
        if (s_titleStyle == null)
        {
            s_titleStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SelectionTitleFontSize, fontStyle = FontStyle.Bold };
            s_countStyle = new GUIStyle(s_titleStyle) { fontSize = BrawlerBalance.SelectionCountFontSize };
            s_hintStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SelectionTitleFontSize };
            s_statusStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.SelectionStatusFontSize };
        }
        s_statusStyle.font = statusFont;
        float width = cleric.Inventory.SlotCount * BrawlerBalance.SelectionCardWidth
            + (cleric.Inventory.SlotCount - 1) * BrawlerBalance.SelectionCardGap;
        float height = BrawlerBalance.SelectionCardHeight + BrawlerBalance.SelectionHintHeight * 2;
        float scale = Mathf.Min(1, Mathf.Max(0, Screen.width - BrawlerBalance.SelectionHudMargin * 2) / width);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector3 origin = new Vector3(BrawlerBalance.SelectionHudMargin,
            Screen.height - BrawlerBalance.SelectionBottomOffset - height * scale, 0);
        GUI.matrix = previousMatrix * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
        for (int i = 0; i < cleric.Inventory.SlotCount; i++)
        {
            DrawCard(cleric, i, i * (BrawlerBalance.SelectionCardWidth + BrawlerBalance.SelectionCardGap));
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
            BrawlerDefines.ActionUseStatus.AttackBoostActive => "ATTACK BOOST ACTIVE",
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

    private static void DrawCard(ClericFighter cleric, int slot, float x)
    {
        BrawlerDefines.UsableAction action = cleric.Inventory.GetAction(slot);
        bool selected = cleric.SelectedSlot == slot;
        bool protection = action == BrawlerDefines.UsableAction.Protection;
        bool expiring = protection && cleric.IsProtectionExpiring;
        // 効果時間から点滅を求め、ヒットストップ中は残り時間も点滅も停止させます。
        float pulse = expiring ? (1 + Mathf.Sin((BrawlerBalance.ProtectionSeconds - cleric.ProtectionSecondsRemaining)
            * BrawlerBalance.ProtectionWarningPulsesPerSecond * Mathf.PI * 2)) / 2 : 0;
        int count = cleric.Inventory.GetSlotCount(slot);
        int capacity = BrawlerBalance.InventoryStackLimit;
        string title = action switch
        {
            BrawlerDefines.UsableAction.Healing => "HEAL",
            BrawlerDefines.UsableAction.Protection => "PROTECTION",
            BrawlerDefines.UsableAction.AttackBoost => "ATTACK BOOST",
            BrawlerDefines.UsableAction.HealingPotion => "POTION",
            BrawlerDefines.UsableAction.ThrowingKnife => "KNIFE",
            BrawlerDefines.UsableAction.ThrowingHammer => "HAMMER",
            _ => "NOTHING"
        };
        var card = new Rect(x, 0, BrawlerBalance.SelectionCardWidth, BrawlerBalance.SelectionCardHeight);
        GUI.color = selected ? new Color(0.12f, 0.25f, 0.35f, 0.95f) : new Color(0.05f, 0.07f, 0.1f, 0.9f);
        if (expiring) GUI.color = Color.Lerp(GUI.color, new Color(0.55f, 0.18f, 0.03f, 0.95f), pulse);
        GUI.DrawTexture(card, Texture2D.whiteTexture);
        GUI.color = selected ? Color.yellow : Color.gray;
        if (expiring) GUI.color = Color.Lerp(GUI.color, new Color(1, 0.45f, 0.1f), pulse);
        float border = BrawlerBalance.SelectionBorderWidth;
        // 選択項目は色だけでなく枠と「>」でも示し、残りゼロでも選択位置が分かるようにします。
        GUI.DrawTexture(new Rect(x, 0, card.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, card.height - border, card.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, 0, border, card.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + card.width - border, 0, border, card.height), Texture2D.whiteTexture);
        float padding = BrawlerBalance.SelectionCardPadding;
        GUI.color = selected ? Color.yellow : count > 0 ? Color.white : Color.gray;
        GUI.Label(new Rect(x + padding, padding, card.width - padding * 2, card.height / 3),
            (selected ? "> " : "") + title, s_titleStyle);
        GUI.Label(new Rect(x + padding, card.height / 3, card.width - padding * 2, card.height / 3),
            action == BrawlerDefines.UsableAction.None ? "—" : $"{count} / {capacity}", s_countStyle);
        if (action == BrawlerDefines.UsableAction.AttackBoost && cleric.IsAttackBoosted)
        {
            GUI.color = cleric.IsAttackBoostExpiring ? Color.yellow : Color.white;
            string boostStatus = cleric.IsAttackBoostExpiring ? "まもなく終了"
                : $"残り {cleric.AttackBoostSecondsRemaining:F1}秒";
            GUI.Label(new Rect(x + padding, card.height * 2 / 3, card.width - padding * 2, card.height / 3),
                boostStatus, s_statusStyle);
        }
        if (protection && cleric.IsProtected && cleric.Health > 0)
        {
            // 他の項目を選択中でも予告を表示します。選択の矢印と枠は引き続き維持します。
            GUI.color = expiring ? Color.yellow : Color.white;
            string protectionStatus = expiring ? "まもなく終了" : $"残り {cleric.ProtectionSecondsRemaining:F1}秒";
            GUI.Label(new Rect(x + padding, card.height * 2 / 3, card.width - padding * 2, card.height / 3),
                protectionStatus, s_statusStyle);
        }
    }
}
