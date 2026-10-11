// アンデッドの近接敵。移動・予告・攻撃は共通処理を継承します。
// 属性だけを上書きし、今後のアンデッド専用の能力追加にも対応できます。
public sealed class UndeadFighter : Fighter
{
    public override bool IsUndead => true;
}
