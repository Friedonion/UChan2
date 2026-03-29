using UnityEngine;

public enum NoteType { Slashing, Fanning, Hit, Boss }

public class Note : MonoBehaviour
{
    public NoteType type = NoteType.Hit;
    public Vector3 targetDirection = Vector3.right; // 기본값: 왼쪽에서 오른쪽으로 베기
    public float speed = 3.0f;
    public float lifeTime = 5.0f;

    [Header("Visual Elements")]
    public GameObject slashIndicator; 
    public GameObject fanIndicator;   
    public GameObject hitIndicator; 
    public GameObject bossIndicator;

    public float minSwingSpeed = 0.1f; 
    public int hp = 1; 
    private bool isMissed = false;

    private float lastHitTime = 0f;
    private float hitCooldown = 0.00f; // 보스 타격 시 연타 방지 쿨타임

    void Start()
    {
        SetupVisuals();
        Destroy(gameObject, lifeTime);
    }

    public void SetupVisuals()
    {
        if (slashIndicator) slashIndicator.SetActive(false);
        if (fanIndicator) fanIndicator.SetActive(false);
        if (hitIndicator) hitIndicator.SetActive(false);
        if (bossIndicator) bossIndicator.SetActive(false);

        // 목표 방향에 맞춰 인디케이터만 회전시킵니다.
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.Cross(Vector3.forward,targetDirection));

        switch (type)
        {
            case NoteType.Slashing:
                if (slashIndicator)
                {
                    slashIndicator.SetActive(true);
                    slashIndicator.transform.localRotation = targetRotation;
                }
                break;
            case NoteType.Fanning:
                if (fanIndicator)
                {
                    fanIndicator.SetActive(true);
                    fanIndicator.transform.localRotation = targetRotation;
                }
                break;
            case NoteType.Hit:
                if (hitIndicator)
                {
                    hitIndicator.SetActive(true);
                    hitIndicator.transform.localRotation = targetRotation;
                }
                break;
            case NoteType.Boss:
                if (bossIndicator)
                {
                    bossIndicator.SetActive(true);
                    bossIndicator.transform.localRotation = targetRotation;
                }
                break;
        }
    }

    void Update()
    {
        transform.Translate(  speed * Time.deltaTime * Vector3.back);

        if (!isMissed && transform.position.z < -1.0f)
        {
            isMissed = true;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NoteMissed();
            }
            Destroy(gameObject, 0.1f);
        }
    }

    private void OnTriggerStay(Collider foreign)
    {
        if (isMissed) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        FanSystem fan = foreign.GetComponentInParent<FanSystem>();
        if (fan != null)
        {
            // 판정 조건: Hit 타입은 부채가 접혀 있어야 함, 나머지는 펴져 있어야 함
            bool fanStateCorrect = (type == NoteType.Hit) ? !fan.IsOpened : fan.IsOpened;
            
            if (fanStateCorrect)
            {
                CheckHitSuccess(fan);
            }
        }
    }

    void CheckHitSuccess(FanSystem fan)
    {
        float currentSpeed = fan.Velocity.magnitude;
        if (currentSpeed < minSwingSpeed)
        {
            return;
        }

        Vector3 moveDir = fan.Velocity.normalized;
        float directionMatch = ((type == NoteType.Slashing)? Mathf.Abs(Vector3.Dot(moveDir, targetDirection)): Vector3.Dot(moveDir, targetDirection));
        bool isCorrectAction = false;
        bool isCorrectDirection = directionMatch > 0.5f; // 60도 범위

        switch (type)
        {
            case NoteType.Slashing:
                // 날(Edge) 판정
                float slashDot = Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal));
                if (slashDot < 0.4f) isCorrectAction = true;
                break;
            case NoteType.Fanning:
                // 면(Face) 판정
                float fanDot = Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal));
                if (fanDot > 0.6f) isCorrectAction = true;
                break;
            case NoteType.Hit:
                isCorrectAction = true; 
                isCorrectDirection = true;
                break;
            case NoteType.Boss:
                fanDot = Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal));
                if (fanDot > 0.6f) isCorrectAction = true;
                break;
        }

        if (isCorrectAction && isCorrectDirection)
        {
            hp--; 
            lastHitTime = Time.time; // 히트 시점 기록
            if (type == NoteType.Boss) transform.localScale *= 0.95f;

            if (hp <= 0)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(type, fan.Velocity.magnitude);
                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"🤜 정확한 히트! (남은 HP: {hp})");
            }
        }
    }
}
