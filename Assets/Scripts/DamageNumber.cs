using UnityEngine;

// 確定したHP変化の種類と数値を、共通の頭上表示へ渡します。
public sealed class DamageNumber : FloatingLabel
{
    public int Amount { get; private set; }
    public BrawlerDefines.DamageKind Kind { get; private set; }

    public void Initialize(Camera camera, int amount, BrawlerDefines.DamageKind kind)
    {
        Amount = amount;
        Kind = kind;
        Color color = kind switch
        {
            BrawlerDefines.DamageKind.Counter => Color.cyan,
            BrawlerDefines.DamageKind.HeavyStrike => new Color(1, 0.5f, 0.15f),
            BrawlerDefines.DamageKind.Healing => Color.green,
            _ => Color.white
        };
        string text = kind == BrawlerDefines.DamageKind.Healing ? "+" + amount : amount.ToString();
        int fontSize = kind == BrawlerDefines.DamageKind.Normal || kind == BrawlerDefines.DamageKind.Healing
            ? BrawlerBalance.DamageNumberFontSize : BrawlerBalance.SpecialDamageNumberFontSize;
        InitializeLabel(camera, text, color, fontSize, BrawlerBalance.DamageNumberSeconds, BrawlerBalance.DamageNumberWidth);
    }
}
