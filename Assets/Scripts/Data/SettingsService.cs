using System;
using UnityEngine;

public static class SettingsService
{
    private const string MusicVolumeKey = "MusicVolume";
    private static float musicVolume = 0.8f;
    private static bool loaded;
    public static event Action<float> MusicVolumeChanged;

    public static void Load()
    {
        musicVolume = Sanitize(PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f));
        loaded = true;
        MusicVolumeChanged?.Invoke(musicVolume);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Get());
        PlayerPrefs.Save();
    }

    public static float Get()
    {
        if (!loaded) Load();
        return musicVolume;
    }

    public static void Set(float value)
    {
        if (!loaded) Load();
        float volume = Sanitize(value);
        if (musicVolume == volume) return;
        musicVolume = volume;
        MusicVolumeChanged?.Invoke(volume);
    }

    private static float Sanitize(float value) => float.IsNaN(value) ? 0.8f : Mathf.Clamp01(value);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        loaded = false;
        musicVolume = 0.8f;
        MusicVolumeChanged = null;
    }
}
