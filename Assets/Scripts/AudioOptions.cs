using System;
using UnityEngine;

// 音声設定の保存と変更通知を担当します。音を再生する役やオプション画面とは分離します。
public static class AudioOptions
{
    private const string SoundEffectsVolumeKey = "Audio.SoundEffectsVolume";
    public static event Action<float> SoundEffectsVolumeChanged;

    // 0は消音、1は従来の最大音量。新規設定でも以前と同じ音の大きさで始めます。
    public static float SoundEffectsVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SoundEffectsVolumeKey, BrawlerBalance.DefaultSoundEffectsVolume));
        set
        {
            if (float.IsNaN(value))
            {
                return;
            }
            float volume = Mathf.Clamp01(value);
            if (Mathf.Approximately(volume, SoundEffectsVolume))
            {
                return;
            }
            // 値が変わったときだけ保存・通知し、再生中の音にも即座に反映します。
            PlayerPrefs.SetFloat(SoundEffectsVolumeKey, volume);
            PlayerPrefs.Save();
            SoundEffectsVolumeChanged?.Invoke(volume);
        }
    }
}
