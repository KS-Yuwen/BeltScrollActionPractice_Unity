using UnityEngine;

// 取得確定後だけ生成します。使用時の回復数値とは別に、何を拾ったかを知らせます。
public sealed class PickupNotice : FloatingLabel
{
    public BrawlerDefines.PickupKind Kind { get; private set; }

    public void Initialize(Camera camera, BrawlerDefines.PickupKind kind, Font font)
    {
        Kind = kind;
        string text = kind == BrawlerDefines.PickupKind.HealingPotion ? "ポーション＋1" : "ナイフ＋1";
        Color color = kind == BrawlerDefines.PickupKind.HealingPotion ? Color.green : Color.cyan;
        InitializeLabel(camera, text, color, BrawlerBalance.PickupNoticeFontSize,
            BrawlerBalance.PickupNoticeSeconds, BrawlerBalance.PickupNoticeWidth, font);
    }
}
