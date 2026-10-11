using UnityEngine;

// 床に落ちた回復ポーション。近づくと所持品に入り、使用するまでHPは回復しません。
public sealed class HealingPotion : MonoBehaviour
{
    private const float PickupRadius = BrawlerBalance.PotionPickupRadius;
    private Fighter _player;
    private float _remainingLifetime = BrawlerBalance.PotionLifetimeSeconds;
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
        // Destroy はフレーム末なのでフラグでも二重取得を防ぎます。
        if (_wasCollected || _remainingLifetime <= 0 || _player == null
            || _player.Health <= 0)
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
        if (!_player.TryStoreHealingPotion())
        {
            return false;
        }
        _wasCollected = true;
        _audio.Play(BrawlerSound.Pickup);
        Destroy(gameObject);
        return true;
    }
}
