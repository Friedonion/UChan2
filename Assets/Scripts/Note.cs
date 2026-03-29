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
    private float hitCooldown = 0.00f; 
    private Vector3 lastHitDirection = Vector3.zero; // 보스 왕복 판정용

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
        transform.Translate(speed * Time.deltaTime * Vector3.back);

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
        if (currentSpeed < minSwingSpeed) return;

        Vector3 moveDir = fan.Velocity.normalized;
        bool isCorrectAction = false;
        bool isCorrectDirection = false;

        // 타입별 판정 로직 분리
        switch (type)
        {
            case NoteType.Slashing:
                // 축(Axis) 판정: 양방향 허용
                if (Mathf.Abs(Vector3.Dot(moveDir, targetDirection)) > 0.5f) isCorrectDirection = true;
                // 날(Edge) 판정
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) < 0.4f) isCorrectAction = true;
                break;

            case NoteType.Fanning:
                // 정방향 판정만 허용
                if (Vector3.Dot(moveDir, targetDirection) > 0.5f) isCorrectDirection = true;
                // 면(Face) 판정
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) > 0.6f) isCorrectAction = true;
                break;

            case NoteType.Hit:
                isCorrectDirection = true; // 방향 무관
                isCorrectAction = true;    // 액션 무관 (접혀있는지는 OnTriggerStay에서 체크됨)
                break;

            case NoteType.Boss:
                // 면(Face) 판정 우선 체크
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) > 0.6f) isCorrectAction = true;

                if (lastHitDirection == Vector3.zero)
                {
                    // [첫 타격] 축(Axis)만 맞으면 어느 쪽이든 인정
                    if (Mathf.Abs(Vector3.Dot(moveDir, targetDirection)) > 0.5f) isCorrectDirection = true;
                }
                else
                {
                    // [이후 타격] 반드시 이전 타격의 반대 방향이어야 함
                    if (Vector3.Dot(moveDir, lastHitDirection) < -0.5f) isCorrectDirection = true;
                }
                break;
        }

        if (isCorrectAction && isCorrectDirection)
        {
            hp--; 
            lastHitTime = Time.time;
            lastHitDirection = moveDir; // 현재 방향 저장 (왕복 판정용)

            // 타격 피드백 (사운드 및 진동)
            if (GameManager.Instance != null) GameManager.Instance.PlayHitSound(type);
            
            // 타입별로 진동 세기 차별화 가능 (옵션)
            float hapticIntensity = (type == NoteType.Boss) ? 0.8f : 0.5f; 
            if (fan != null) fan.TriggerHaptic(hapticIntensity);

            if (type == NoteType.Boss && bossIndicator) bossIndicator.transform.localScale *= 0.65f;

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
