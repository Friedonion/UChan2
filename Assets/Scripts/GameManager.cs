using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;

public enum GameState { Ready, Playing, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private XROrigin xrOrigin;

    [Header("Game State")]
    public GameState currentState = GameState.Ready;
    private bool isFullCombo = true;
    private int maxPossibleScore = 0;

    private double pauseStartDspTime;
    private double accumulatedPausedDsp;

    public double EffectiveDspTime
    {
        get
        {
            double syncOffset = VolumeSettings.SyncOffsetMs / 1000.0;
            if (currentState == GameState.Paused)
                return pauseStartDspTime - accumulatedPausedDsp + syncOffset;
            return AudioSettings.dspTime - accumulatedPausedDsp + syncOffset;
        }
    }
    public int score = 0;
    public int combo = 0;
    public int health = 100;
    public int maxHealth = 100;

    [Header("Debug Settings")]
    [Tooltip("에디터 플레이 모드에서만 동작하는 무적 모드입니다.")]
    public bool isInvincible = false;

    [Header("Scoring Settings")]
    public int pointsPerNote = 100;
    public int damagePerMiss = 10;

    [Header("Audio Settings")]
    public ChartData currentChart; // 차트 데이터 (JSON 채보 로드 결과)
    public AudioClip musicTrack;
    public AudioClip slashSound; 
    public AudioClip hitSound; 
    public AudioClip fanSound; 
    private AudioSource audioSource;
    private AudioSource musicSource;

    [Header("Visual Effects")]
    [Tooltip("기본 수묵화 효과 대신 사용할 타격 이펙트 프리팹을 여기에 넣으세요.")]
    public GameObject customHitEffectPrefab;

    // 노트 하나의 최종 판정 결과(Miss 여부)를 프레임 지연 없이 알림 - TutorialManager가 연습 노트
    // 성공/실패를 감지해 재시도 루프를 도는 데 사용한다. 벽은 AddScore를 타지 않으므로 WallDodged()로 별도 발생.
    public static event System.Action OnNoteResolved;
    public static event System.Action OnNoteMissed;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = false;
            musicSource.playOnAwake = false;

            xrOrigin = FindObjectOfType<XROrigin>();
        }
        else Destroy(gameObject);
    }

    void OnApplyMusicVolume(float v) => musicSource.volume = v;
    void OnApplyHitVolume(float v)  => audioSource.volume  = v;

    void Start()
    {
        VolumeSettings.Load();
        audioSource.volume  = VolumeSettings.HitVolume;
        musicSource.volume  = VolumeSettings.MusicVolume;
        VolumeSettings.OnMusicVolumeChanged += OnApplyMusicVolume;
        VolumeSettings.OnHitVolumeChanged   += OnApplyHitVolume;

        // MusicSelectUI 씬에서 넘어온 곡이 있으면 적용
        if (SongSelection.Chart != null)
        {
            currentChart = SongSelection.Chart;
            musicTrack   = SongSelection.Chart.audioClip;
            
            // 정식으로 씬을 넘어왔을 때만 연습모드 여부에 따라 무적 설정을 확실히 덮어씌움 (에디터 테스트 배려)
            // Sync Test는 무한 루프 테스트라, Tutorial은 연습 중 반복 미스로 체력이 깎여 게임오버로 끊기면 안 되므로 항상 무적.
            isInvincible = SongSelection.IsPracticeMode || currentChart.songName == "Sync Test" || currentChart.songName == "Tutorial";
            
            SongSelection.Chart = null;
        }

        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == "Locomotion" && go.scene == gameObject.scene)
                go.SetActive(false);
        }

        ResetGame();
        StartGame();
    }

    public float startDelay = 3.0f; // 시작 전 대기 시간

    int GetComboMultiplier(int combo)
    {
        if (combo >= 30) return 8;
        if (combo >= 20) return 4;
        if (combo >= 10) return 2;
        return 1;
    }

    void CalculateMaxScore()
    {
        maxPossibleScore = 0;
        if (currentChart == null) return;
        int simCombo = 0;
        foreach (var note in currentChart.notes)
        {
            if (note.typeEnum == NoteType.Wall) continue;
            maxPossibleScore += pointsPerNote * GetComboMultiplier(simCombo);
            simCombo++;
        }
    }

    public void StartGame()
    {
        if (currentState != GameState.Ready) return;
        
        currentState = GameState.Playing;
        isFullCombo = true;
        CalculateMaxScore();
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowStartUI(false);
            UIManager.Instance.UpdateScore(0);
            UIManager.Instance.UpdateCombo(0);
            UIManager.Instance.UpdateHealth(100);
        }
        
        StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        // 1. VR 헤드셋 트래킹 위치가 카메라에 적용될 시간을 잠깐 대기합니다 (씬 로드 직후 0값 방지)
        yield return new WaitForSeconds(0.5f);
        RecenterPlayer();

        // 남은 시작 대기 시간 (UI 카운트다운)
        yield return new WaitForSeconds(startDelay - 0.5f);

        if (currentChart != null)
        {
            NoteSpawner spawner = FindObjectOfType<NoteSpawner>();
            float leadInTime = 4.0f; // 노트가 미리 날아오기 시작할 시간
            if (currentChart != null && currentChart.travelTime > 0)
                leadInTime = currentChart.travelTime;

            // 2. 노트 스포너 시작 (오디오 시작 시간을 미래로 설정)
            if (spawner != null)
            {
                spawner.StartPlaying(currentChart, leadInTime);
            }

            // 3. 노트가 유저에게 도달할 때까지(leadInTime) 기다림
            yield return new WaitForSeconds(leadInTime);
            
            // 4. 노래 재생 시작
            if (musicTrack != null)
            {
                musicSource.clip = musicTrack;
                musicSource.loop = currentChart != null && currentChart.loop;
                musicSource.Play();
                StartCoroutine(CheckMusicEnd());
            }
        }
        else
        {
            Debug.LogWarning("No chart assigned to GameManager!");
        }
    }

    private void RecenterPlayer()
    {
        if (xrOrigin == null) return;

        // 카메라 위치를 (0, y, 0)으로 맞춤
        Vector3 targetPos = new Vector3(0, xrOrigin.Camera.transform.position.y, 0);
        xrOrigin.MoveCameraToWorldLocation(targetPos);
        
        // 정면(Z축) 방향 보정
        float rotationAngleY = xrOrigin.Camera.transform.rotation.eulerAngles.y;
        xrOrigin.RotateAroundCameraUsingOriginUp(-rotationAngleY);
    }

    IEnumerator CheckMusicEnd()
    {
        while (currentState == GameState.Playing && musicSource.isPlaying)
        {
            yield return new WaitForSeconds(1.0f);
        }
        
        if (currentState == GameState.Playing)
        {
            // 노래가 끝남 -> 결과창
            GameOver(true);
        }
    }

    public void AddScore(NoteType type, float velocity, string judgment)
    {
        if (currentState != GameState.Playing) return;

        OnNoteResolved?.Invoke();

        // 1. 판정 배율 결정
        float judgmentMultiplier = 1.0f;
        Color judgmentColor = UIManager.Instance.perfectColor;

        switch (judgment)
        {
            case "PERFECT": 
                judgmentMultiplier = 1.0f; 
                judgmentColor = UIManager.Instance.perfectColor;
                break;
            case "GREAT": 
                judgmentMultiplier = 0.8f; 
                judgmentColor = UIManager.Instance.greatColor;
                break;
            case "GOOD": 
                judgmentMultiplier = 0.5f; 
                judgmentColor = Color.blue; // Good 색상은 파란색으로 임시 지정
                break;
        }

        // 2. 속도 보너스 제거 (1.0 고정)
        float speedBonus = 1.0f;

        // 3. 콤보 배율 결정
        int comboMultiplier = 1;
        if (combo >= 30) comboMultiplier = 8;
        else if (combo >= 20) comboMultiplier = 4;
        else if (combo >= 10) comboMultiplier = 2;

        // 4. 최종 점수 계산
        int finalPoints = Mathf.RoundToInt(pointsPerNote * judgmentMultiplier * speedBonus * comboMultiplier);

        score += finalPoints;
        combo++;
        
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateScore(score);
            UIManager.Instance.UpdateCombo(combo);
            UIManager.Instance.ShowJudgment(judgment, judgmentColor);
        }
    }

    // 보스 연타 등 중간 판정 표시용
    public void ShowTemporaryJudgment(string judgment)
    {
        if (UIManager.Instance == null) return;
        Color color = (judgment == "PERFECT") ? UIManager.Instance.perfectColor : UIManager.Instance.greatColor;
        UIManager.Instance.ShowJudgment(judgment, color);
    }

    public void PlayHitSound(NoteType type)
    {
        if (audioSource == null) return;
        AudioClip clipToPlay = (type == NoteType.Slashing) ? slashSound : (type == NoteType.Hit ? hitSound : fanSound);
        if (clipToPlay != null) audioSource.PlayOneShot(clipToPlay);
    }

    // Wall은 CheckHitSuccessWithDirection/AddScore 경로를 타지 않으므로(공격 대상이 아니라 회피 대상),
    // 안 맞고 끝까지 통과했을 때 Note.cs가 이 메서드로 성공을 알린다.
    public void WallDodged()
    {
        if (currentState != GameState.Playing) return;
        OnNoteResolved?.Invoke();
    }

    public void NoteMissed()
    {
        if (currentState != GameState.Playing) return;

        OnNoteMissed?.Invoke();

        if (isInvincible) return; // 무적(연습) 모드일 경우 미스 판정 및 체력 감소 무시

        isFullCombo = false;
        combo = 0;
        health -= damagePerMiss;
        health = Mathf.Max(0, health);

        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateCombo(0);
            UIManager.Instance.UpdateHealth(health);
            UIManager.Instance.ShowJudgment("MISS", UIManager.Instance.missColor);
        }

        if (health <= 0) GameOver(false);
    }

    void GameOver(bool cleared)
    {
        currentState = GameState.GameOver;
        musicSource.Stop();
        FindObjectOfType<NoteSpawner>()?.StopPlaying();

        float percentage = maxPossibleScore > 0 ? (score / (float)maxPossibleScore) * 100f : 0f;
        percentage = Mathf.Clamp(percentage, 0f, 100f);

        if (UIManager.Instance) UIManager.Instance.ShowResultUI(score, percentage, isFullCombo);
        Debug.Log(cleared ? "STAGE CLEARED!" : "GAME OVER");
    }

    public void PauseGame()
    {
        if (currentState != GameState.Playing) return;
        currentState = GameState.Paused;
        Time.timeScale = 0f;
        musicSource.Pause();
        pauseStartDspTime = AudioSettings.dspTime;
    }

    public void ResumeGame()
    {
        if (currentState != GameState.Paused) return;
        accumulatedPausedDsp += AudioSettings.dspTime - pauseStartDspTime;
        currentState = GameState.Playing;
        Time.timeScale = 1f;
        musicSource.UnPause();
    }

    void OnDestroy()
    {
        VolumeSettings.OnMusicVolumeChanged -= OnApplyMusicVolume;
        VolumeSettings.OnHitVolumeChanged   -= OnApplyHitVolume;
    }

    public void ResetGame()
    {
        score = 0;
        combo = 0;
        health = maxHealth;
        isFullCombo = true;
        maxPossibleScore = 0;
        currentState = GameState.Ready;
        Time.timeScale = 1f;
        
        // 🌟 다시하기 버그 수정: 이전 게임의 일시정지 누적 시간과 노트 찌꺼기를 완벽히 초기화합니다.
        accumulatedPausedDsp = 0;
        pauseStartDspTime = 0;
        FindObjectOfType<NoteSpawner>()?.StopPlaying();

        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateScore(0);
            UIManager.Instance.UpdateCombo(0);
            UIManager.Instance.UpdateHealth(health);
            UIManager.Instance.resultPanel.SetActive(false);
            UIManager.Instance.startPanel.SetActive(true);
        }
    }
}
