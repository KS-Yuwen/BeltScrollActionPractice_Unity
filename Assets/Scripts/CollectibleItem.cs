using UnityEngine;

// 床アイテムの共通処理。期限・XZ距離・二重取得防止を共用し、保存先だけ派生クラスが決めます。
public abstract class CollectibleItem : MonoBehaviour
{
    private Fighter _player;
    private BrawlerAudio _audio;
    private float _remainingLifetime;
    private bool _wasCollected;
    // 準備区間の支給品は時間切れにせず、戦闘開始時から通常の寿命を進めます。
    public bool IsLifetimePaused { get; private set; }
    public void SetLifetimePaused(bool paused) => IsLifetimePaused = paused;

    protected abstract float PickupRadius { get; }
    protected abstract BrawlerDefines.PickupKind Kind { get; }
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
        AdvanceLifetime(Time.deltaTime);
        if (_remainingLifetime <= 0)
        {
            Destroy(gameObject);
            return;
        }
        TryCollect();
    }

    private void AdvanceLifetime(float deltaTime)
    {
        if (!IsLifetimePaused)
        {
            _remainingLifetime = Mathf.Max(0, _remainingLifetime - Mathf.Max(0, deltaTime));
        }
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
        _player.NotifyItemPickup(Kind);
        _audio.Play(BrawlerSound.Pickup);
        Destroy(gameObject);
        return true;
    }
}
