using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    public int score = 0;
    public int combo = 0;
    public int health = 100;
    public int maxHealth = 100;

    [Header("Scoring Settings")]
    public int pointsPerNote = 100;
    public int damagePerMiss = 10;

    [Header("Audio Settings")]
    public AudioClip slashSound; 
    public AudioClip hitSound; 
    public AudioClip fanSound; 
    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        else Destroy(gameObject);
    }

    public void AddScore(NoteType type, float velocity)
    {
        // AddScore에서는 더 이상 PlayHitSound를 호출하지 않고 Note.cs에서 직접 호출하도록 함
        // 속도에 비례해서 가산점 부여 (최대 1.5배)
        float speedBonus = Mathf.Clamp(velocity / 5f, 1f, 1.5f);
        int finalPoints = Mathf.RoundToInt(pointsPerNote * speedBonus);

        score += finalPoints;
        combo++;
        
        Debug.Log($"✨ HIT! 점수: {score} | 콤보: {combo} | 체력: {health}");
    }

    public void PlayHitSound(NoteType type)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = null;

        switch (type)
        {
            case NoteType.Slashing:
                clipToPlay = slashSound;
                break;
            case NoteType.Hit:
                clipToPlay = hitSound;
                break;
            case NoteType.Fanning:
            case NoteType.Boss:
                clipToPlay = fanSound;
                break;
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    public void NoteMissed()
    {
        combo = 0;
        health -= damagePerMiss;
        health = Mathf.Max(0, health);

        Debug.Log($"💔 MISS! 콤보 깨짐 | 체력: {health}");

        if (health <= 0)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        Debug.Log("💀 GAME OVER! 다시 시작하려면 Play 버튼을 누르세요.");
        // 여기에 나중에 게임 오버 UI를 띄우는 로직을 추가합니다.
    }
}
