using UnityEngine;

// 取得確定後だけ生成します。使用時の回復数値とは別に、何を拾ったかを知らせます。
public sealed class PickupNotice : FloatingLabel
{
    public BrawlerDefines.PickupKind Kind { get; private set; }

    public void Initialize(Camera camera, BrawlerDefines.PickupKind kind, Font font)
    {
        Kind = kind;
        string text = kind switch
        {
            BrawlerDefines.PickupKind.HealingPotion => "ポーション＋1",
            BrawlerDefines.PickupKind.ThrowingKnife => "ナイフ＋1",
            BrawlerDefines.PickupKind.HealingScroll => "回復魔法＋1",
            BrawlerDefines.PickupKind.ProtectionScroll => "補助魔法＋1",
            _ => "ハンマー＋1"
        };
        Color color = kind == BrawlerDefines.PickupKind.HealingPotion ? Color.green : Color.cyan;
        InitializeLabel(camera, text, color, BrawlerBalance.PickupNoticeFontSize,
            BrawlerBalance.PickupNoticeSeconds, BrawlerBalance.PickupNoticeWidth, font);
    }
}
