using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("Pause Panel")]
    public GameObject pausePanel;

    [Header("Song Info")]
    public TextMeshProUGUI songNameText;
    public TextMeshProUGUI songBpmText;
    public Image coverImage;

    [Header("Status Display")]
    public TextMeshProUGUI statusScoreText;
    public TextMeshProUGUI statusHpText;

    [Header("Follow Camera")]
    public float distanceFromCamera = 2.0f;

    private bool menuButtonPrev = false;
    private Transform cameraTransform;
    private readonly List<GameObject> hiddenNotes = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[PauseManager] GameManager.Instance is null");
            return;
        }

        bool menuPressed = GetMenuButton();
        if (menuPressed && !menuButtonPrev)
        {
            var state = GameManager.Instance.currentState;
            Debug.Log($"[PauseManager] Menu pressed, state={state}");
            if (state == GameState.Playing)
                Pause();
            else if (state == GameState.Paused)
                Resume();
        }
        menuButtonPrev = menuPressed;
    }

    bool GetMenuButton()
    {
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            Debug.Log("[PauseManager] M key detected");
            return true;
        }
#endif
        var devices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Left | UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);
        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool value) && value)
                return true;
        }
        return false;
    }

    public void Pause()
    {
        GameManager.Instance.PauseGame();
        PositionPanelInFrontOfCamera();
        UpdateSongInfo();
        UpdateStatusDisplay();
        SetMainHudVisible(false);
        SetNotesVisible(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        SetMainHudVisible(true);
        SetNotesVisible(true);
        GameManager.Instance.ResumeGame();
    }

    // 일시정지 패널이 날아오는 노트에 가려지지 않도록, 일시정지 중엔 현재 활성화된 노트들을 숨김
    void SetNotesVisible(bool visible)
    {
        if (!visible)
        {
            hiddenNotes.Clear();
            Note[] activeNotes = FindObjectsOfType<Note>();
            foreach (Note note in activeNotes)
            {
                if (note.gameObject.activeSelf)
                {
                    hiddenNotes.Add(note.gameObject);
                    note.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            foreach (GameObject note in hiddenNotes)
            {
                if (note != null) note.SetActive(true);
            }
            hiddenNotes.Clear();
        }
    }

    // 일시정지 중엔 메인 HUD의 점수/체력을 숨기고, 대신 Pause 패널 안의 표시로 대체
    void UpdateStatusDisplay()
    {
        if (statusScoreText != null) statusScoreText.text = $"SCORE: {GameManager.Instance.score:N0}";
        if (statusHpText != null) statusHpText.text = $"HP: {GameManager.Instance.health}%";
    }

    void SetMainHudVisible(bool visible)
    {
        if (UIManager.Instance == null) return;
        if (UIManager.Instance.scoreText != null) UIManager.Instance.scoreText.gameObject.SetActive(visible);
        if (UIManager.Instance.healthText != null) UIManager.Instance.healthText.gameObject.SetActive(visible);
        if (UIManager.Instance.comboText != null) UIManager.Instance.comboText.gameObject.SetActive(visible);
        if (UIManager.Instance.judgmentText != null) UIManager.Instance.judgmentText.gameObject.SetActive(visible);
    }

    // 설정창이 열려있는 동안엔 점수/체력 표시를 가려서 겹쳐 보이지 않게 함 (SettingsManager에서 호출)
    public void SetStatusDisplayVisible(bool visible)
    {
        if (statusScoreText != null) statusScoreText.gameObject.SetActive(visible);
        if (statusHpText != null) statusHpText.gameObject.SetActive(visible);
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MusicSelectUI");
    }

    public void RestartGame()
    {
        // 현재 곡 정보를 유지한 채 씬 재시작
        SongSelection.Chart = GameManager.Instance.currentChart;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void PositionPanelInFrontOfCamera()
    {
        if (cameraTransform == null || pausePanel == null) return;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Move the parent Canvas transform (not just this child), since the Canvas's own
        // transform is what TrackedDeviceGraphicRaycaster uses to compute the hit-test plane.
        // Moving only the child left the raycast plane behind while the visuals moved,
        // so clicks aimed at the visible buttons never registered.
        Transform canvasTransform = pausePanel.transform.parent != null ? pausePanel.transform.parent : pausePanel.transform;

        canvasTransform.position = cameraTransform.position + forward * distanceFromCamera;
        canvasTransform.rotation = Quaternion.LookRotation(forward);
    }

    void UpdateSongInfo()
    {
        var chart = GameManager.Instance.currentChart;
        if (chart == null) { Debug.LogWarning("[PauseManager] currentChart is null"); return; }

        Debug.Log($"[PauseManager] chart={chart.songName}, coverSprite={chart.coverSprite}");

        if (songNameText != null) songNameText.text = chart.songName;
        if (songBpmText != null) songBpmText.text = $"BPM  {chart.bpm:F0}";
        if (coverImage == null) { Debug.LogWarning("[PauseManager] coverImage ref is null"); return; }

        if (chart.coverSprite != null)
        {
            coverImage.sprite = chart.coverSprite;
            coverImage.color = Color.white;
            coverImage.preserveAspect = true;
            Debug.Log("[PauseManager] Cover sprite set: " + chart.coverSprite.name);
        }
        else
        {
            Debug.LogWarning("[PauseManager] coverSprite is null on chart");
        }
    }
}
