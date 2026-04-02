using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;

public enum GameState { Ready, Playing, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private XROrigin xrOrigin;

    [Header("Game State")]
    public GameState currentState = GameState.Ready;
    public int score = 0;
    public int combo = 0;
    public int health = 100;
    public int maxHealth = 100;

    [Header("Scoring Settings")]
    public int pointsPerNote = 100;
    public int damagePerMiss = 10;

    [Header("Audio Settings")]
    public AudioClip musicTrack;
    public AudioClip slashSound; 
    public AudioClip hitSound; 
    public AudioClip fanSound; 
    private AudioSource audioSource;
    private AudioSource musicSource;

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

    void Start()
    {
        ResetGame();
        // UI가 로딩될 시간을 아주 잠깐 주고 바로 시작
        StartCoroutine(AutoStart());
    }

    IEnumerator AutoStart()
    {
        yield return new WaitForSeconds(0.5f);
        StartGame();
    }

    public void StartGame()
    {
        if (currentState != GameState.Ready) return;

        // 플레이어 리센터
        RecenterPlayer();
        
        currentState = GameState.Playing;
        if (UIManager.Instance) 
        {
            UIManager.Instance.ShowStartUI(false);
            UIManager.Instance.UpdateScore(0);
            UIManager.Instance.UpdateCombo(0);
            UIManager.Instance.UpdateHealth(100);
        }
        
        // NoteSpawner에게 시작 신호를 보냄
        FindObjectOfType<NoteSpawner>()?.StartPlaying();

        if (musicTrack != null)
        {
            musicSource.clip = musicTrack;
            musicSource.Play();
            StartCoroutine(CheckMusicEnd());
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

    public void AddScore(NoteType type, float velocity)
    {
        if (currentState != GameState.Playing) return;

        float speedBonus = Mathf.Clamp(velocity / 5f, 1f, 1.5f);
        int finalPoints = Mathf.RoundToInt(pointsPerNote * speedBonus);

        score += finalPoints;
        combo++;
        
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateScore(score);
            UIManager.Instance.UpdateCombo(combo);
            // 판정 UI (간단하게 PERFECT로 표시, 나중에 세분화 가능)
            UIManager.Instance.ShowJudgment("PERFECT", UIManager.Instance.perfectColor);
        }
    }

    public void PlayHitSound(NoteType type)
    {
        if (audioSource == null) return;
        AudioClip clipToPlay = (type == NoteType.Slashing) ? slashSound : (type == NoteType.Hit ? hitSound : fanSound);
        if (clipToPlay != null) audioSource.PlayOneShot(clipToPlay);
    }

    public void NoteMissed()
    {
        if (currentState != GameState.Playing) return;

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
        
        // 노트 생성 중단 및 화면 청소
        FindObjectOfType<NoteSpawner>()?.StopPlaying();
        
        if (UIManager.Instance) UIManager.Instance.ShowResultUI(score);
        Debug.Log(cleared ? "🏆 STAGE CLEARED!" : "💀 GAME OVER");
    }

    public void ResetGame()
    {
        score = 0;
        combo = 0;
        health = maxHealth;
        currentState = GameState.Ready;
        
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateScore(0);
            UIManager.Instance.UpdateCombo(0);
            UIManager.Instance.UpdateHealth(health);
            UIManager.Instance.resultPanel.SetActive(false);
        }
    }
}
