// 先行ボタン→後続ボタンの押下履歴を扱います。デバイスやキャラクターの行動状態には依存しません。
public sealed class ButtonSequenceBuffer
{
    private float _firstPressedAt = BrawlerDefines.UnsetTime;

    public bool Record(bool firstPressed, bool secondPressed, float time, float windowSeconds)
    {
        // 同時押しは順番入力として扱いません。時間が止まっていても別更新なら順番を判定できます。
        bool requested = secondPressed && !firstPressed && _firstPressedAt != BrawlerDefines.UnsetTime
            && time - _firstPressedAt <= windowSeconds;
        if (firstPressed)
        {
            _firstPressedAt = time;
        }
        // 行動に移れない場合の消費判断は呼び出し側が担当します。
        return requested;
    }

    public void Reset()
    {
        _firstPressedAt = BrawlerDefines.UnsetTime;
    }
}
