using UnityEngine;

// 魔法・所持品の選択表示。入力や所持数を変更せず、職業共通の状態を読み取って描画します。
public static class SelectionHud
{
    // 表示内容は固定リストではなく、実際の6スロットから取得します。
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_countStyle;
    private static GUIStyle s_hintStyle;
    private static GUIStyle s_statusStyle;

    public static void Draw(Fighter player, Font statusFont)
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
        float width = player.Inventory.SlotCount * BrawlerBalance.SelectionCardWidth
            + (player.Inventory.SlotCount - 1) * BrawlerBalance.SelectionCardGap;
        // 魔法職ではバーの場所を常に確保し、発動のたびに所持欄が上下へ跳ねないようにします。
        float height = BrawlerBalance.SelectionCardHeight + BrawlerBalance.SelectionHintHeight * 2
            + (player is SpellFighter ? BrawlerBalance.SelectionSpellProgressHeight : 0);
        float scale = Mathf.Min(1, Mathf.Max(0, Screen.width - BrawlerBalance.SelectionHudMargin * 2) / width);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector3 origin = new Vector3(BrawlerBalance.SelectionHudMargin,
            Screen.height - BrawlerBalance.SelectionBottomOffset - height * scale, 0);
        GUI.matrix = previousMatrix * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
        for (int i = 0; i < player.Inventory.SlotCount; i++)
        {
            DrawCard(player, i, i * (BrawlerBalance.SelectionCardWidth + BrawlerBalance.SelectionCardGap));
        }
        GUI.color = Color.white;
        string hint = BrawlerInput.IsGamepadConnected ? "View / Share: Select    RT / R2: Use" : "Q: Select    E: Use";
        GUI.Label(new Rect(0, BrawlerBalance.SelectionCardHeight, width, BrawlerBalance.SelectionHintHeight), hint, s_hintStyle);
        BrawlerDefines.ActionUseStatus status = ((IPlayerActionUser)player).SelectedActionUseStatus;
        string reason = player is SpellFighter caster && caster.IsCastingSpell
            ? $"CASTING {caster.SpellCastSecondsRemaining:F1}s"
            : player is SpellFighter releasing && releasing.IsReleasingSpell ? $"SPELL EFFECT {releasing.SpellEffectSecondsRemaining:F1}s"
            : player is SpellFighter recovering && recovering.IsRecoveringFromSpell ? $"RECOVERING {recovering.SpellRecoverySecondsRemaining:F1}s"
            : status switch
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
        DrawSpellProgress(player, width);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static void DrawSpellProgress(Fighter player, float width)
    {
        if (player is not SpellFighter caster || player.Health <= 0
            || (!caster.IsCastingSpell && !caster.IsReleasingSpell && !caster.IsRecoveringFromSpell))
        {
            return;
        }
        // 選択枠と独立して表示するので、使用でNOTHINGになった場合や枠を切り替えた場合も維持します。
        float gap = BrawlerBalance.SelectionSpellProgressGap;
        float phaseWidth = (width - gap * (BrawlerBalance.SelectionSpellPhaseCount - 1)) / BrawlerBalance.SelectionSpellPhaseCount;
        float y = BrawlerBalance.SelectionCardHeight + BrawlerBalance.SelectionHintHeight * 2;
        DrawSpellPhase(0, y, phaseWidth, "CASTING", caster.IsCastingSpell,
            caster.IsCastingSpell ? caster.SpellCastProgress : 1, caster.SpellCastSecondsRemaining,
            BrawlerBalance.SelectionSpellCastColor);
        DrawSpellPhase(phaseWidth + gap, y, phaseWidth, "SPELL EFFECT", caster.IsReleasingSpell,
            caster.IsCastingSpell ? 0 : caster.IsReleasingSpell ? caster.SpellEffectProgress : 1,
            caster.SpellEffectSecondsRemaining, BrawlerBalance.SelectionSpellEffectColor);
        DrawSpellPhase((phaseWidth + gap) * 2, y, phaseWidth, "RECOVERING", caster.IsRecoveringFromSpell,
            caster.IsRecoveringFromSpell ? caster.SpellRecoveryProgress : 0, caster.SpellRecoverySecondsRemaining,
            BrawlerBalance.SelectionSpellRecoveryColor);
    }

    private static void DrawSpellPhase(float x, float y, float width, string label, bool active,
        float progress, float remaining, Color color)
    {
        GUI.color = active ? color : Color.gray;
        float labelHeight = BrawlerBalance.SelectionSpellProgressHeight - BrawlerBalance.SelectionSpellBarHeight;
        GUI.Label(new Rect(x, y, width, labelHeight), active ? $"> {label} {remaining:F1}s" : label, s_hintStyle);
        var bar = new Rect(x, y + labelHeight, width, BrawlerBalance.SelectionSpellBarHeight);
        GUI.color = new Color(0.05f, 0.07f, 0.1f, 0.9f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        // 各段階で左から満ち、現在の段階は色と「>」の両方で示します。
        GUI.color = active ? color : Color.gray;
        bar.width *= Mathf.Clamp01(progress);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
    }

    private static void DrawCard(Fighter player, int slot, float x)
    {
        var cleric = player as ClericFighter;
        BrawlerDefines.UsableAction action = player.Inventory.GetAction(slot);
        bool selected = ((IPlayerActionUser)player).SelectedSlot == slot;
        bool protection = action == BrawlerDefines.UsableAction.Protection;
        bool expiring = protection && cleric != null && cleric.IsProtectionExpiring;
        // 効果時間から点滅を求め、ヒットストップ中は残り時間も点滅も停止させます。
        float pulse = expiring ? (1 + Mathf.Sin((BrawlerBalance.ProtectionSeconds - cleric.ProtectionSecondsRemaining)
            * BrawlerBalance.ProtectionWarningPulsesPerSecond * Mathf.PI * 2)) / 2 : 0;
        int count = player.Inventory.GetSlotCount(slot);
        int capacity = BrawlerBalance.InventoryStackLimit;
        bool isSpell = action == BrawlerDefines.UsableAction.Healing || action == BrawlerDefines.UsableAction.Protection
            || action == BrawlerDefines.UsableAction.AttackBoost || action == BrawlerDefines.UsableAction.MagicMissile;
        bool lowUses = DisplayOptions.ShowLowSpellUsesWarning && isSpell && count > 0
            && count <= BrawlerBalance.LowSpellUsesThreshold;
        string title = action switch
        {
            BrawlerDefines.UsableAction.MagicMissile => "MAGIC MISSILE",
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
        // 少数時は色と「!」で強調します。常時点滅させず、設定でこの強調だけを無効にできます。
        if (lowUses)
        {
            GUI.color = BrawlerBalance.LowSpellUsesColor;
        }
        GUI.Label(new Rect(x + padding, card.height / 3, card.width - padding * 2, card.height / 3),
            action == BrawlerDefines.UsableAction.None ? "—" : (lowUses ? "! " : "") + $"{count} / {capacity}", s_countStyle);
        if (DisplayOptions.ShowSpellDepletedNotice && action == BrawlerDefines.UsableAction.None
            && player is SpellFighter caster && caster.DepletedSpellSlot == slot
            && caster.DepletedSpellNoticeSecondsRemaining > 0 && player.Health > 0)
        {
            // 選択を別の枠へ移しても元の空き枠に表示します。補充や別アイテムの取得後には表示しません。
            GUI.color = BrawlerBalance.LowSpellUsesColor;
            GUI.Label(new Rect(x + padding, card.height * 2 / 3, card.width - padding * 2, card.height / 3),
                "使い切り", s_statusStyle);
        }
        GUI.color = selected ? Color.yellow : count > 0 ? Color.white : Color.gray;
        if (action == BrawlerDefines.UsableAction.AttackBoost && cleric != null && cleric.IsAttackBoosted)
        {
            GUI.color = cleric.IsAttackBoostExpiring ? Color.yellow : Color.white;
            string boostStatus = cleric.IsAttackBoostExpiring ? "まもなく終了"
                : $"残り {cleric.AttackBoostSecondsRemaining:F1}秒";
            GUI.Label(new Rect(x + padding, card.height * 2 / 3, card.width - padding * 2, card.height / 3),
                boostStatus, s_statusStyle);
        }
        if (protection && cleric != null && cleric.IsProtected && player.Health > 0)
        {
            // 他の項目を選択中でも予告を表示します。選択の矢印と枠は引き続き維持します。
            GUI.color = expiring ? Color.yellow : Color.white;
            string protectionStatus = expiring ? "まもなく終了" : $"残り {cleric.ProtectionSecondsRemaining:F1}秒";
            GUI.Label(new Rect(x + padding, card.height * 2 / 3, card.width - padding * 2, card.height / 3),
                protectionStatus, s_statusStyle);
        }
    }
}
