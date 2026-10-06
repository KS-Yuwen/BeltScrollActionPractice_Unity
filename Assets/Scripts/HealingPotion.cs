using UnityEngine;

// 床に落ちた回復ポーション。魔法の回数を消費せず、近づくだけで拾えます。
public sealed class HealingPotion : MonoBehaviour
{
    private const int HealingAmount = 20;
    private const float PickupRadius = 0.7f;
    private Fighter _player;
    private float _remainingLifetime = 20;
    private bool _wasCollected;
    private BrawlerAudio _audio;

    public void Initialize(Fighter player, BrawlerAudio audio)
    {
        _player = player;
        _audio = audio;
    }

    private void Update()
    {
        // 拾わずに残したアイテムも20秒で消し、長時間プレイ時の蓄積を防ぎます。
        _remainingLifetime = Mathf.Max(0, _remainingLifetime - Time.deltaTime);
        if (_remainingLifetime <= 0)
        {
            Destroy(gameObject);
            return;
        }
        TryCollect();
    }

    public bool TryCollect()
    {
        // 満タン・死亡時には拾いません。Destroy はフレーム末なのでフラグでも二重取得を防ぎます。
        if (_wasCollected || _remainingLifetime <= 0 || _player == null
            || _player.Health <= 0 || _player.Health >= _player.MaxHealth)
        {
            return false;
        }
        Vector3 offset = _player.transform.position - transform.position;
        offset.y = 0;
        // X と Z の両方を調べ、奥行きが離れた瓶を拾わないようにします。
        if (offset.sqrMagnitude > PickupRadius * PickupRadius)
        {
            return false;
        }
        _wasCollected = true;
        _player.RestoreHealth(HealingAmount);
        _audio.Play(BrawlerSound.Pickup);
        Destroy(gameObject);
        return true;
    }
}
