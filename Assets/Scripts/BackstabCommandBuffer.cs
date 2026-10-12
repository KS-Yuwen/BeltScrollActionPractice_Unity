using UnityEngine;

// 後ろ→前→攻撃を記録します。後ろへ歩いて向きが変わっても、最初の前方向を維持します。
public sealed class BackstabCommandBuffer
{
    private float _direction;
    private float _backAt = BrawlerDefines.UnsetTime;
    private float _forwardAt = BrawlerDefines.UnsetTime;
    private int _sample;
    private int _forwardSample;

    public void Record(Vector3 input, float facing, float time)
    {
        _sample++;
        if (Mathf.Abs(input.z) > BrawlerBalance.CommandDirectionThreshold)
        {
            Reset();
            return;
        }
        if (_forwardAt == BrawlerDefines.UnsetTime && _backAt != BrawlerDefines.UnsetTime && time - _backAt > BrawlerBalance.BackstabDirectionWindowSeconds)
        {
            Reset();
        }
        if (_backAt == BrawlerDefines.UnsetTime && input.x * facing < -BrawlerBalance.CommandDirectionThreshold)
        {
            _direction = facing;
            _backAt = time;
        }
        else if (_backAt != BrawlerDefines.UnsetTime && _forwardAt == BrawlerDefines.UnsetTime
            && input.x * _direction > BrawlerBalance.CommandDirectionThreshold)
        {
            _forwardAt = time;
            _forwardSample = _sample;
        }
    }

    public bool TryConsume(Vector3 input, float time, out float direction)
    {
        direction = _direction;
        if (_forwardAt == BrawlerDefines.UnsetTime || _sample <= _forwardSample
            || time - _forwardAt > BrawlerBalance.BackstabButtonWindowSeconds
            || input.x * _direction <= BrawlerBalance.CommandDirectionThreshold)
        {
            return false;
        }
        Reset();
        return true;
    }

    public void Reset()
    {
        _backAt = BrawlerDefines.UnsetTime;
        _forwardAt = BrawlerDefines.UnsetTime;
    }
}
