using UnityEngine;

public static class VolumeSettings
{
    const string KEY_MASTER = "vol_master";
    const string KEY_MUSIC  = "vol_music";
    const string KEY_HIT    = "vol_hit";
    const string KEY_EFFECT = "vol_effect_on";
    const string KEY_TRAIL  = "vol_trail_on";
    const string KEY_SYNC_OFFSET = "sync_offset_ms";

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume  { get; private set; } = 1f;
    public static float HitVolume    { get; private set; } = 1f;
    public static bool  HitEffectOn  { get; private set; } = true;
    public static bool  TrailEffectOn { get; private set; } = true;

    // 노트-오디오 싱크 보정값(ms). 헤드셋/기기마다 오디오 지연이 달라 노트 도달 시점을 앞뒤로 조정하기 위함.
    // 양수: 노트가 더 빨리 도달(음악보다 앞서 감), 음수: 노트가 더 늦게 도달.
    public const float SyncOffsetMin = -300f;
    public const float SyncOffsetMax = 300f;
    public static float SyncOffsetMs { get; private set; } = 0f;

    public static void Load()
    {
        MasterVolume  = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        MusicVolume   = PlayerPrefs.GetFloat(KEY_MUSIC,  1f);
        HitVolume     = PlayerPrefs.GetFloat(KEY_HIT,    1f);
        HitEffectOn   = PlayerPrefs.GetInt(KEY_EFFECT,   1) == 1;
        TrailEffectOn = PlayerPrefs.GetInt(KEY_TRAIL,    1) == 1;
        SyncOffsetMs  = PlayerPrefs.GetFloat(KEY_SYNC_OFFSET, 0f);
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

    public static void SetSyncOffset(float ms)
    {
        SyncOffsetMs = Mathf.Clamp(ms, SyncOffsetMin, SyncOffsetMax);
        PlayerPrefs.SetFloat(KEY_SYNC_OFFSET, SyncOffsetMs);
    }

    static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }
}
