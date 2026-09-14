using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Master Volume")]
    public TextMeshProUGUI masterVolumeText;
    public Button masterMinusBtn;
    public Button masterPlusBtn;

    [Header("Music Volume")]
    public TextMeshProUGUI musicVolumeText;
    public Button musicMinusBtn;
    public Button musicPlusBtn;

    [Header("Hit Volume")]
    public TextMeshProUGUI hitVolumeText;
    public Button hitMinusBtn;
    public Button hitPlusBtn;

    [Header("Hit Effect")]
    public Button hitEffectOnBtn;
    public Button hitEffectOffBtn;
    public Image  hitEffectOnBg;
    public Image  hitEffectOffBg;

    [Header("Trail Effect")]
    public Button trailEffectOnBtn;
    public Button trailEffectOffBtn;
    public Image  trailEffectOnBg;
    public Image  trailEffectOffBg;

    [Header("Sync Offset")]
    public TextMeshProUGUI syncOffsetText;
    public Button syncMinusBtn;
    public Button syncPlusBtn;

    const float Step = 0.05f;
    const float SyncStep = 10f; // ms

    void Start()
    {
        VolumeSettings.Load();
        settingsPanel.SetActive(false);

        // 비활성 부모 안에 있을 수 있으므로 Resources로 탐색
        foreach (var btn in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (btn.name == "SettingsButton" && btn.gameObject.scene == gameObject.scene)
            {
                btn.onClick.AddListener(OpenSettings);
                break;
            }
        }

        var closeBtn = settingsPanel.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSettings);

        masterMinusBtn.onClick.AddListener(() => ChangeVolume(0, -Step));
        masterPlusBtn.onClick.AddListener(()  => ChangeVolume(0, +Step));
        musicMinusBtn.onClick.AddListener(()  => ChangeVolume(1, -Step));
        musicPlusBtn.onClick.AddListener(()   => ChangeVolume(1, +Step));
        hitMinusBtn.onClick.AddListener(()    => ChangeVolume(2, -Step));
        hitPlusBtn.onClick.AddListener(()     => ChangeVolume(2, +Step));

        hitEffectOnBtn.onClick.AddListener(()  => SetHitEffect(true));
        hitEffectOffBtn.onClick.AddListener(() => SetHitEffect(false));

        if (trailEffectOnBtn != null) trailEffectOnBtn.onClick.AddListener(()  => SetTrailEffect(true));
        if (trailEffectOffBtn != null) trailEffectOffBtn.onClick.AddListener(() => SetTrailEffect(false));

        if (syncMinusBtn != null) syncMinusBtn.onClick.AddListener(() => ChangeSyncOffset(-SyncStep));
        if (syncPlusBtn  != null) syncPlusBtn.onClick.AddListener(()  => ChangeSyncOffset(+SyncStep));

        RefreshAllUI();
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        if (PauseManager.Instance != null) PauseManager.Instance.SetStatusDisplayVisible(false);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        if (PauseManager.Instance != null) PauseManager.Instance.SetStatusDisplayVisible(true);
    }

    void ChangeVolume(int type, float delta)
    {
        switch (type)
        {
            case 0: VolumeSettings.SetMaster(VolumeSettings.MasterVolume + delta); break;
            case 1: VolumeSettings.SetMusic (VolumeSettings.MusicVolume  + delta); break;
            case 2: VolumeSettings.SetHit   (VolumeSettings.HitVolume    + delta); break;
        }
        RefreshAllUI();
    }

    void SetHitEffect(bool on)
    {
        VolumeSettings.SetHitEffect(on);
        RefreshHitEffectUI();
    }

    void SetTrailEffect(bool on)
    {
        VolumeSettings.SetTrailEffect(on);
        RefreshTrailEffectUI();
    }

    void ChangeSyncOffset(float deltaMs)
    {
        VolumeSettings.SetSyncOffset(VolumeSettings.SyncOffsetMs + deltaMs);
        RefreshAllUI();
    }

    void RefreshAllUI()
    {
        masterVolumeText.text = $"{Mathf.RoundToInt(VolumeSettings.MasterVolume * 100)}%";
        musicVolumeText.text  = $"{Mathf.RoundToInt(VolumeSettings.MusicVolume  * 100)}%";
        hitVolumeText.text    = $"{Mathf.RoundToInt(VolumeSettings.HitVolume    * 100)}%";
        RefreshHitEffectUI();
        RefreshTrailEffectUI();
        RefreshSyncOffsetUI();
    }

    void RefreshSyncOffsetUI()
    {
        if (syncOffsetText == null) return;
        int ms = Mathf.RoundToInt(VolumeSettings.SyncOffsetMs);
        syncOffsetText.text = $"{(ms >= 0 ? "+" : "")}{ms}ms";
    }

    void RefreshHitEffectUI()
    {
        bool on = VolumeSettings.HitEffectOn;
        hitEffectOnBg.color  = on  ? new Color(0.2f, 0.7f, 0.4f) : new Color(0.15f, 0.15f, 0.15f);
        hitEffectOffBg.color = !on ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.15f, 0.15f, 0.15f);
    }

    void RefreshTrailEffectUI()
    {
        if (trailEffectOnBg == null || trailEffectOffBg == null) return; // 씬에 트레일 이펙트 UI가 아직 없음

        bool on = VolumeSettings.TrailEffectOn;
        trailEffectOnBg.color  = on  ? new Color(0.2f, 0.7f, 0.4f) : new Color(0.15f, 0.15f, 0.15f);
        trailEffectOffBg.color = !on ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.15f, 0.15f, 0.15f);
    }
}
