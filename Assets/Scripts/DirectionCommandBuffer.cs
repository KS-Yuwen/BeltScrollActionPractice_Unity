using UnityEngine;

// 方向の「変化」を覚える小さな入力履歴。保持し続けるだけではコマンドを再成立させません。
public sealed class DirectionCommandBuffer
{
    private BrawlerDefines.CommandDirection _previousDirection;
    private float _downAt = BrawlerDefines.UnsetTime;
    private float _diagonalAt = BrawlerDefines.UnsetTime;
    private float _facing;
    private float _uppercutDownAt = BrawlerDefines.UnsetTime;
    private float _uppercutUpAt = BrawlerDefines.UnsetTime;
    private bool _wasDown;
    private bool _wasUp;

    public void Record(Vector3 input, float facing, float time)
    {
        // 下→上は途中のニュートラルを許可します。スライディングとは独立した履歴です。
        bool currentDown = input.z < -BrawlerBalance.CommandDirectionThreshold;
        bool currentUp = input.z > BrawlerBalance.CommandDirectionThreshold;
        if (currentDown && !_wasDown)
        {
            _uppercutDownAt = time;
            _uppercutUpAt = BrawlerDefines.UnsetTime;
        }
        if (currentUp && !_wasUp)
        {
            _uppercutUpAt = time - _uppercutDownAt <= BrawlerBalance.UppercutDirectionWindowSeconds
                ? time : BrawlerDefines.UnsetTime;
            _uppercutDownAt = BrawlerDefines.UnsetTime;
        }
        _wasDown = currentDown;
        _wasUp = currentUp;
        bool down = input.z < -BrawlerBalance.CommandDirectionThreshold;
        // 判定の境界をそろえます。浅い斜め入力で状態だけ進み、後の深い入力を見逃すのを防ぎます。
        BrawlerDefines.CommandDirection direction = down
            ? (Mathf.Abs(input.x) <= BrawlerBalance.CommandDirectionThreshold ? BrawlerDefines.CommandDirection.Down
                : input.x * _facing > BrawlerBalance.CommandDirectionThreshold ? BrawlerDefines.CommandDirection.DownForward
                : BrawlerDefines.CommandDirection.DownBackward)
            : BrawlerDefines.CommandDirection.Neutral;
        if (direction == _previousDirection)
        {
            return;
        }
        _previousDirection = direction;
        if (direction == BrawlerDefines.CommandDirection.Down)
        {
            _downAt = time;
            _diagonalAt = BrawlerDefines.UnsetTime;
            _facing = facing;
        }
        else if (direction == BrawlerDefines.CommandDirection.DownForward && time - _downAt <= BrawlerBalance.SlideDirectionWindowSeconds)
        {
            _diagonalAt = time;
            _downAt = BrawlerDefines.UnsetTime;
        }
        else
        {
            _downAt = BrawlerDefines.UnsetTime;
            _diagonalAt = BrawlerDefines.UnsetTime;
        }
    }

    public bool TryConsumeDownUp(Vector3 input, float time)
    {
        if (input.z <= BrawlerBalance.CommandDirectionThreshold
            || time - _uppercutUpAt > BrawlerBalance.UppercutButtonWindowSeconds)
        {
            return false;
        }
        _uppercutUpAt = BrawlerDefines.UnsetTime;
        return true;
    }

    public bool TryConsumeDownForward(Vector3 input, float time, out float direction)
    {
        direction = _facing;
        if (time - _diagonalAt > BrawlerBalance.SlideButtonWindowSeconds
            || input.z >= -BrawlerBalance.CommandDirectionThreshold || input.x * _facing <= BrawlerBalance.CommandDirectionThreshold)
        {
            return false;
        }
        _diagonalAt = BrawlerDefines.UnsetTime;
        return true;
    }
}
