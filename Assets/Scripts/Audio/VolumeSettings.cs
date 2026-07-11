using UnityEngine;

public static class VolumeSettings
{
    const string KEY_MASTER = "vol_master";
    const string KEY_MUSIC  = "vol_music";
    const string KEY_HIT    = "vol_hit";
    const string KEY_EFFECT = "vol_effect_on";
    const string KEY_TRAIL  = "vol_trail_on";

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume  { get; private set; } = 1f;
    public static float HitVolume    { get; private set; } = 1f;
    public static bool  HitEffectOn  { get; private set; } = true;
    public static bool  TrailEffectOn { get; private set; } = true;

    public static void Load()
    {
        MasterVolume  = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        MusicVolume   = PlayerPrefs.GetFloat(KEY_MUSIC,  1f);
        HitVolume     = PlayerPrefs.GetFloat(KEY_HIT,    1f);
        HitEffectOn   = PlayerPrefs.GetInt(KEY_EFFECT,   1) == 1;
        TrailEffectOn = PlayerPrefs.GetInt(KEY_TRAIL,    1) == 1;
        Apply();
    }

    public static void SetMaster(float v)
    {
        MasterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_MASTER, MasterVolume);
        Apply();
    }

    public static System.Action<float> OnMusicVolumeChanged;
    public static System.Action<float> OnHitVolumeChanged;

    public static void SetMusic(float v)
    {
        MusicVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_MUSIC, MusicVolume);
        OnMusicVolumeChanged?.Invoke(MusicVolume);
    }

    public static void SetHit(float v)
    {
        HitVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_HIT, HitVolume);
        OnHitVolumeChanged?.Invoke(HitVolume);
    }

    public static void SetHitEffect(bool on)
    {
        HitEffectOn = on;
        PlayerPrefs.SetInt(KEY_EFFECT, on ? 1 : 0);
    }

    public static void SetTrailEffect(bool on)
    {
        TrailEffectOn = on;
        PlayerPrefs.SetInt(KEY_TRAIL, on ? 1 : 0);
    }

    static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }
}
