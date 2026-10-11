using UnityEngine;

// 床アイテムの共通処理。期限・XZ距離・二重取得防止を共用し、保存先だけ派生クラスが決めます。
public abstract class CollectibleItem : MonoBehaviour
{
    private Fighter _player;
    private BrawlerAudio _audio;
    private float _remainingLifetime;
    private bool _wasCollected;

    protected abstract float PickupRadius { get; }
    protected abstract float LifetimeSeconds { get; }
    protected abstract bool TryStore(Fighter player);

    public void Initialize(Fighter player, BrawlerAudio audio)
    {
        _player = player;
        _audio = audio;
        _remainingLifetime = LifetimeSeconds;
    }

    protected virtual void Update()
    {
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
        // Destroyはフレーム末まで遅延するので、取得済みフラグで同じ物を二度保存しません。
        if (_wasCollected || _remainingLifetime <= 0 || _player == null || _player.Health <= 0)
        {
            return false;
        }
        Vector3 offset = _player.transform.position - transform.position;
        offset.y = 0;
        if (offset.sqrMagnitude > PickupRadius * PickupRadius || !TryStore(_player))
        {
            return false;
        }
        _wasCollected = true;
        _audio.Play(BrawlerSound.Pickup);
        Destroy(gameObject);
        return true;
    }
}
