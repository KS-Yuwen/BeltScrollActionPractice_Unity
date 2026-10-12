using UnityEngine;

// 方向の変化と入力更新の順番を記録します。技ごとのニュートラル条件と共通の受付時間を分けます。
public sealed class DirectionCommandBuffer
{
    private BrawlerDefines.CommandDirection _previousDirection;
    private bool _hasRecorded;
    private float _downAt = BrawlerDefines.UnsetTime;
    private float _diagonalAt = BrawlerDefines.UnsetTime;
    private int _diagonalSample = BrawlerDefines.UnsetInputSample;
    private bool _downStartedFromNeutral;
    private float _facing;
    private float _uppercutDownAt = BrawlerDefines.UnsetTime;
    private bool _uppercutPassedNeutral;
    private float _uppercutUpAt = BrawlerDefines.UnsetTime;
    private int _upSample = BrawlerDefines.UnsetInputSample;
    private float _forwardAt = BrawlerDefines.UnsetTime;
    private int _forwardSample = BrawlerDefines.UnsetInputSample;
    private float _forwardFacing;
    private float _directionAt = BrawlerDefines.UnsetTime;
    private int _directionSample = BrawlerDefines.UnsetInputSample;

    public bool IsNeutral => _hasRecorded && _previousDirection == BrawlerDefines.CommandDirection.Neutral;
    public bool EnteredForwardFromNeutral { get; private set; }

    public void Record(Vector3 input, float facing, float time, int sample = BrawlerDefines.UnsetInputSample)
    {
        EnteredForwardFromNeutral = false;
        float threshold = BrawlerBalance.CommandDirectionThreshold;
        bool vertical = Mathf.Abs(input.z) > threshold;
        bool horizontal = Mathf.Abs(input.x) > threshold;
        float relativeFacing = _downAt != BrawlerDefines.UnsetTime ? _facing : facing;
        bool forward = input.x * relativeFacing > threshold;
        BrawlerDefines.CommandDirection direction = !vertical
            ? !horizontal ? BrawlerDefines.CommandDirection.Neutral
                : forward ? BrawlerDefines.CommandDirection.Forward : BrawlerDefines.CommandDirection.Backward
            : input.z < 0
                ? !horizontal ? BrawlerDefines.CommandDirection.Down
                    : forward ? BrawlerDefines.CommandDirection.DownForward : BrawlerDefines.CommandDirection.DownBackward
                : !horizontal ? BrawlerDefines.CommandDirection.Up
                    : forward ? BrawlerDefines.CommandDirection.UpForward : BrawlerDefines.CommandDirection.UpBackward;
        if (_hasRecorded && direction == _previousDirection)
        {
            return;
        }
        bool wasNeutral = IsNeutral;
        _hasRecorded = true;
        _previousDirection = direction;
        _directionAt = time;
        _directionSample = sample;
        if (direction == BrawlerDefines.CommandDirection.Down)
        {
            // 下は斜めで代用しません。スライディング用の履歴はニュートラルなしでも開始します。
            _downAt = time;
            _facing = facing;
            _downStartedFromNeutral = wasNeutral;
            _diagonalAt = BrawlerDefines.UnsetTime;
            _uppercutDownAt = wasNeutral ? time : BrawlerDefines.UnsetTime;
            _uppercutPassedNeutral = false;
            _uppercutUpAt = BrawlerDefines.UnsetTime;
        }
        else
        {
            if (direction == BrawlerDefines.CommandDirection.DownForward
                && _downAt != BrawlerDefines.UnsetTime && time - _downAt <= BrawlerBalance.SlideDirectionWindowSeconds)
            {
                _diagonalAt = time;
                _diagonalSample = sample;
                _downAt = BrawlerDefines.UnsetTime;
            }
            else
            {
                _downAt = BrawlerDefines.UnsetTime;
                _diagonalAt = BrawlerDefines.UnsetTime;
            }
            if (direction == BrawlerDefines.CommandDirection.Neutral && _uppercutDownAt != BrawlerDefines.UnsetTime)
            {
                _uppercutPassedNeutral = true;
            }
            else if (direction == BrawlerDefines.CommandDirection.Up && wasNeutral && _uppercutPassedNeutral
                && _uppercutDownAt != BrawlerDefines.UnsetTime
                && time - _uppercutDownAt <= BrawlerBalance.UppercutDirectionWindowSeconds)
            {
                _uppercutUpAt = time;
                _upSample = sample;
                _uppercutDownAt = BrawlerDefines.UnsetTime;
            }
            else
            {
                _uppercutDownAt = BrawlerDefines.UnsetTime;
                _uppercutUpAt = BrawlerDefines.UnsetTime;
            }
        }
        _forwardAt = BrawlerDefines.UnsetTime;
        if (direction == BrawlerDefines.CommandDirection.Forward && wasNeutral)
        {
            _forwardAt = time;
            _forwardSample = sample;
            _forwardFacing = facing;
            EnteredForwardFromNeutral = true;
        }
    }

    // 実行時は入力更新番号で順番を判定します。ヒットストップ中にTime.timeが同じでも順番を保持できます。
    // 独立した履歴テストでは更新番号を省略し、渡された時刻で前後関係を確認します。
    private static bool IsEarlier(float enteredAt, int enteredSample, float time, int sample)
    {
        return enteredAt != BrawlerDefines.UnsetTime
            && (sample >= 0 && enteredSample >= 0 ? sample > enteredSample : time > enteredAt);
    }

    public bool IsDownHeldBeforeButton(float time, int sample)
    {
        return _previousDirection == BrawlerDefines.CommandDirection.Down
            && IsEarlier(_directionAt, _directionSample, time, sample);
    }

    public bool IsUpHeldBeforeButton(float time, int sample)
    {
        return _previousDirection == BrawlerDefines.CommandDirection.Up
            && IsEarlier(_directionAt, _directionSample, time, sample);
    }

    public bool TryConsumeDownUp(Vector3 input, float time, int sample = BrawlerDefines.UnsetInputSample)
    {
        if (_previousDirection != BrawlerDefines.CommandDirection.Up
            || !IsEarlier(_uppercutUpAt, _upSample, time, sample)
            || time - _uppercutUpAt > BrawlerBalance.UppercutButtonWindowSeconds)
        {
            return false;
        }
        _uppercutUpAt = BrawlerDefines.UnsetTime;
        return true;
    }

    public bool TryConsumeDownForward(Vector3 input, float time, out float direction,
        bool requiresNeutral = false, int sample = BrawlerDefines.UnsetInputSample)
    {
        direction = _facing;
        if (_previousDirection != BrawlerDefines.CommandDirection.DownForward
            || !IsEarlier(_diagonalAt, _diagonalSample, time, sample)
            || time - _diagonalAt > BrawlerBalance.SlideButtonWindowSeconds
            || requiresNeutral && !_downStartedFromNeutral
            || input.x * _facing <= BrawlerBalance.CommandDirectionThreshold)
        {
            return false;
        }
        _diagonalAt = BrawlerDefines.UnsetTime;
        return true;
    }

    public bool TryConsumeForward(float facing, float time, int sample)
    {
        if (_previousDirection != BrawlerDefines.CommandDirection.Forward || facing != _forwardFacing
            || !IsEarlier(_forwardAt, _forwardSample, time, sample)
            || time - _forwardAt > BrawlerBalance.HeavyStrikeButtonWindowSeconds)
        {
            return false;
        }
        _forwardAt = BrawlerDefines.UnsetTime;
        return true;
    }
}
