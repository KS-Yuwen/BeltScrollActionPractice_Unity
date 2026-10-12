using System.Collections.Generic;
using UnityEngine;

// 音の種類をゲーム側の意味で指定します。将来録音素材へ置き換えても呼び出し側は共用できます。
public enum BrawlerSound
{
    Hit,
    StrongHit,
    Guard,
    Heal,
    Protection,
    Pickup,
    EnemyWarning,
    EnemySwing,
    EnemyMiss,
    BossWarning,
    BossSwing,
    BossMiss
}

// 試作用の短い効果音を起動時に生成し、同じ AudioClip を繰り返し再生します。
public sealed class BrawlerAudio : MonoBehaviour
{
    private const int SampleRate = 44100;
    // 音作りの調整値。ボスは低い音域にし、複数の通常敵がいる場面でも聞き分けられます。
    private const float WarningDuration = 0.16f;
    private const float SwingDuration = 0.12f;
    private const float MissDuration = 0.09f;
    private const float EnemyWarningStartFrequency = 520f;
    private const float EnemyWarningEndFrequency = 780f;
    private const float BossWarningStartFrequency = 180f;
    private const float BossWarningEndFrequency = 320f;
    private const float EnemySwingStartFrequency = 420f;
    private const float EnemySwingEndFrequency = 130f;
    private const float BossSwingStartFrequency = 200f;
    private const float BossSwingEndFrequency = 55f;
    private const float EnemyMissStartFrequency = 250f;
    private const float EnemyMissEndFrequency = 90f;
    private const float BossMissStartFrequency = 120f;
    private const float BossMissEndFrequency = 40f;
    private const float WhooshToneWeight = 0.3f;
    private const float WhooshNoiseWeight = 0.7f;
    private readonly Dictionary<BrawlerSound, AudioClip> _clips = new Dictionary<BrawlerSound, AudioClip>();
    private readonly Dictionary<BrawlerSound, float> _lastPlayedAt = new Dictionary<BrawlerSound, float>();
    private AudioSource _source;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0;
        _source.volume = 0.35f;
        // 音声はゲーム時間の停止に影響されません。リスナーの一時停止にも依存させません。
        _source.ignoreListenerPause = true;
        foreach (BrawlerSound sound in System.Enum.GetValues(typeof(BrawlerSound)))
        {
            _clips.Add(sound, CreateClip(sound));
        }
    }

    public void Play(BrawlerSound sound)
    {
        // 範囲攻撃で同じ音が同時に重なることを抑えます。異なる種類の音は重ねて再生できます。
        float now = Time.unscaledTime;
        if (_lastPlayedAt.TryGetValue(sound, out float lastTime) && now - lastTime < 0.03f)
        {
            return;
        }
        _lastPlayedAt[sound] = now;
        _source.PlayOneShot(_clips[sound]);
    }

    private static AudioClip CreateClip(BrawlerSound sound)
    {
        bool isImpact = sound == BrawlerSound.Hit || sound == BrawlerSound.StrongHit;
        bool isWhoosh = sound == BrawlerSound.EnemySwing || sound == BrawlerSound.BossSwing
            || sound == BrawlerSound.EnemyMiss || sound == BrawlerSound.BossMiss;
        float duration = sound switch
        {
            BrawlerSound.EnemyWarning or BrawlerSound.BossWarning => WarningDuration,
            BrawlerSound.EnemySwing or BrawlerSound.BossSwing => SwingDuration,
            BrawlerSound.EnemyMiss or BrawlerSound.BossMiss => MissDuration,
            BrawlerSound.Heal or BrawlerSound.Protection => 0.4f,
            _ => 0.18f
        };
        var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
        // ノイズは専用の乱数を使い、敵の出現位置などゲーム側の乱数列を変えません。
        var noise = new System.Random(17 + (int)sound);
        float phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float progress = time / duration;
            float frequency = sound == BrawlerSound.StrongHit ? Mathf.Lerp(150, 45, progress)
                : sound == BrawlerSound.Hit ? Mathf.Lerp(240, 80, progress)
                : sound == BrawlerSound.Guard ? 1000
                : sound == BrawlerSound.Heal ? Mathf.Lerp(400, 900, progress)
                : sound == BrawlerSound.Protection ? Mathf.Lerp(650, 350, progress)
                : progress < 0.5f ? 700 : 1050;
            // 予兆は上昇する合図、振りと空振りは短い風切り音として作ります。
            frequency = sound switch
            {
                BrawlerSound.EnemyWarning => Mathf.Lerp(EnemyWarningStartFrequency, EnemyWarningEndFrequency, progress),
                BrawlerSound.BossWarning => Mathf.Lerp(BossWarningStartFrequency, BossWarningEndFrequency, progress),
                BrawlerSound.EnemySwing => Mathf.Lerp(EnemySwingStartFrequency, EnemySwingEndFrequency, progress),
                BrawlerSound.BossSwing => Mathf.Lerp(BossSwingStartFrequency, BossSwingEndFrequency, progress),
                BrawlerSound.EnemyMiss => Mathf.Lerp(EnemyMissStartFrequency, EnemyMissEndFrequency, progress),
                BrawlerSound.BossMiss => Mathf.Lerp(BossMissStartFrequency, BossMissEndFrequency, progress),
                _ => frequency
            };
            phase += 2 * Mathf.PI * frequency / SampleRate;
            float tone = Mathf.Sin(phase);
            if (isWhoosh)
            {
                tone = tone * WhooshToneWeight + ((float)noise.NextDouble() * 2 - 1) * WhooshNoiseWeight;
            }
            else if (isImpact)
            {
                tone = tone * 0.65f + ((float)noise.NextDouble() * 2 - 1) * 0.35f;
            }
            else if (sound == BrawlerSound.Guard)
            {
                tone = (tone + Mathf.Sin(phase * 1.7f) * 0.5f) / 1.5f;
            }
            // 最初と最後の振幅を0に近づけ、急な波形の切り替えによるクリック音を抑えます。
            float envelope = Mathf.Min(1, time / 0.005f) * Mathf.Pow(1 - progress, 2);
            samples[i] = tone * envelope * 0.8f;
        }
        var clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        // 実行時に生成した音声データは、リスタート時に解放します。
        foreach (AudioClip clip in _clips.Values)
        {
            Destroy(clip);
        }
    }
}
