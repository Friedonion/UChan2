using UnityEngine;

public enum NoteType { Slashing, Fanning, Hit, Boss }

public class Note : MonoBehaviour
{
    public NoteType type = NoteType.Hit;
    public Vector3 targetDirection = Vector3.right;
    
    [Header("Movement Settings")]
    private float spawnZ;
    private float hitZ = 0f;
    private float hitTime;
    private float travelTime;
    private bool initialized = false;

    [Header("Visual Elements")]
    public GameObject slashIndicator; 
    public GameObject fanIndicator;   
    public GameObject hitIndicator; 
    public GameObject bossIndicator;

    [Header("Fuzzy & Swept Collision Settings")]
    public float hitRadius = 0.65f;              // 노트 스윕 충돌 감지 반경
    public float minSwingSpeed = 0.4f; 
    public int hp = 1; 

    [Header("Rhythm Judgment Settings (All in Degrees & Seconds!)")]
    [Tooltip("Slashing (베기) PERFECT 스윙 허용 오차 각도 (도 단위, 작을수록 엄격)")]
    [Delayed] public float slashingPerfectAngle = 17.5f;
    [Tooltip("Slashing (베기) GREAT 스윙 허용 오차 각도 (도 단위)")]
    [Delayed] public float slashingGreatAngle = 30f;
    [Tooltip("Slashing (베기) GOOD 스윙 허용 오차 각도 (도 단위)")]
    [Delayed] public float slashingFuzzyAngle = 33.3f;

    [Tooltip("Fanning (부치기) PERFECT 스윙 허용 오차 각도 (도 단위, 작을수록 엄격)")]
    [Delayed] public float fanningPerfectAngle = 45f;
    [Tooltip("Fanning (부치기) GREAT 스윙 허용 오차 각도 (도 단위)")]
    [Delayed] public float fanningGreatAngle = 60f;
    [Tooltip("Fanning (부치기) GOOD 스윙 허용 오차 각도 (도 단위)")]
    [Delayed] public float fanningFuzzyAngle = 63.2f;

    [Tooltip("Boss (보스) PERFECT 허용 오차 각도 (도 단위)")]
    [Delayed] public float bossPerfectAngle = 30f;
    [Tooltip("Boss (보스) GREAT 허용 오차 각도 (도 단위)")]
    [Delayed] public float bossGreatAngle = 50f;
    [Tooltip("Boss (보스) GOOD 허용 오차 각도 (도 단위)")]
    [Delayed] public float bossGoodAngle = 70f;

    [Tooltip("찌르기(Thrust) 방지를 위한 Z축 이동 비율 임계값 (0~1 사이, 0.80이면 Z축 성분이 80% 이상일 때 찌르기로 판정하여 판정 제외)")]
    [Delayed] public float maxThrustZRatio = 0.8f;

    private bool isMissed = false;
    private bool isDead = false;

    private float lastHitTime = 0f;
    private float hitCooldown = 0.00f; 
    private Vector3 lastHitDirection = Vector3.zero;

    public void Initialize(NoteType type, Vector3 direction, float hitTime, float travelTime, float spawnZ)
    {
        this.type = type;
        this.targetDirection = direction;
        this.hitTime = hitTime;
        this.travelTime = travelTime;
        this.spawnZ = spawnZ;
        
        if (type == NoteType.Boss) hp = 3;
        else hp = 1;

        // 🛡️ [Rhythm Angle Hierarchy Enforcement at Runtime]
        EnforceAngleHierarchy();

        SetupVisuals();
        initialized = true;
    }

    private void EnforceAngleHierarchy()
    {
        slashingGreatAngle = Mathf.Max(slashingGreatAngle, slashingPerfectAngle);
        slashingFuzzyAngle = Mathf.Max(slashingFuzzyAngle, slashingGreatAngle);

        fanningGreatAngle = Mathf.Max(fanningGreatAngle, fanningPerfectAngle);
        fanningFuzzyAngle = Mathf.Max(fanningFuzzyAngle, fanningGreatAngle);

        bossGreatAngle = Mathf.Max(bossGreatAngle, bossPerfectAngle);
        bossGoodAngle = Mathf.Max(bossGoodAngle, bossGreatAngle);
    }

    public void SetupVisuals()
    {
        if (slashIndicator) slashIndicator.SetActive(false);
        if (fanIndicator) fanIndicator.SetActive(false);
        if (hitIndicator) hitIndicator.SetActive(false);
        if (bossIndicator) bossIndicator.SetActive(false);

        // 🌟 [ROOT ROTATION FOR COLLIDER ALIGNMENT]
        // 기존에는 자식 인디케이터의 localRotation만 돌렸기 때문에,
        // 루트에 붙어있는 BoxCollider는 항상 Quaternion.identity로 고정되어
        // 시각적 화살표 방향과 실제 판정 박스 방향이 불일치하는 괴리감이 생겼습니다.
        // 루트 자체를 targetDirection에 맞게 회전시키면 BoxCollider도 함께 정렬됩니다.
        // 자식 인디케이터는 localRotation = identity로 두어 루트 회전을 그대로 상속합니다.
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.Cross(Vector3.forward, targetDirection));
        transform.rotation = targetRotation;

        switch (type)
        {
            case NoteType.Slashing: if (slashIndicator) { slashIndicator.SetActive(true); slashIndicator.transform.localRotation = Quaternion.identity; } break;
            case NoteType.Fanning: if (fanIndicator) { fanIndicator.SetActive(true); fanIndicator.transform.localRotation = Quaternion.identity; } break;
            case NoteType.Hit: if (hitIndicator) { hitIndicator.SetActive(true); hitIndicator.transform.localRotation = Quaternion.identity; } break;
            case NoteType.Boss: if (bossIndicator) { bossIndicator.SetActive(true); bossIndicator.transform.localRotation = Quaternion.identity; } break;
        }
    }


    void Update()
    {
        if (!initialized) return;

        // 시간 기반 위치 계산 (Beat Saber 방식)
        float currentTime = (float)AudioSettings.dspTime;
        float spawnTime = hitTime - travelTime;
        float progress = (currentTime - spawnTime) / travelTime;

        float currentZ = Mathf.Lerp(spawnZ, hitZ, progress);
        transform.position = new Vector3(transform.position.x, transform.position.y, currentZ);

        // 판정선 근처(Z 차이가 작을 때) 스윕 충돌 강제 검사 (터널링 100% 방지)
        if (!isMissed && !isDead && Mathf.Abs(currentZ - hitZ) < 1.2f)
        {
            // 구 버전 FindObjectsOfType 지원 (유니티 버전에 관계없이 호환되도록 처리)
            FanSystem[] fans = FindObjectsOfType<FanSystem>();
            foreach (var fan in fans)
            {
                if (fan != null && fan.gameObject.activeInHierarchy)
                {
                    // 🌟 [HIT NOTE ORIGINAL RULES RESTORED]
                    // 일반 치기(Hit)는 부채가 접혀있을 때(!IsOpened)만 작동하며,
                    // 베기(Slashing)/부치기(Fanning)는 반드시 부채가 펼쳐져 있어야(IsOpened) 타격이 동작합니다.
                    bool fanStateCorrect = (type == NoteType.Hit) ? !fan.IsOpened : fan.IsOpened;
                    if (fanStateCorrect)
                    {
                        // 🌟 [STATIC TOUCH PREVENT & THRUST PREVENT]
                        // 일반 치기(Hit)는 3D 전체 속도로 검사하지만, 베기(Slashing)/부치기(Fanning)/보스(Boss)는 
                        // Z축 찌르기를 필터링하기 위해 2D 투영 평면(XY 평면) 상에서의 스윙 속도를 검사합니다.
                        float currentSpeed = (type == NoteType.Hit) 
                            ? fan.Velocity.magnitude 
                            : Vector3.ProjectOnPlane(fan.Velocity, Vector3.forward).magnitude;

                        if (currentSpeed < minSwingSpeed) continue;

                        if (CheckSweptCollision(fan, hitRadius, out Vector3 customMoveDir))
                        {
                            CheckHitSuccessWithDirection(fan, customMoveDir);
                            if (isDead) break;
                        }
                    }
                }
            }
        }

        // 판정선을 지나쳤을 때 (progress > 1.0f 면 hitZ를 넘은 것)
        if (!isMissed && progress > 1.1f)
        {
            isMissed = true;
            if (GameManager.Instance != null) GameManager.Instance.NoteMissed();
            Deactivate();
        }
    }

    public void Deactivate()
    {
        initialized = false;
        isMissed = false;
        isDead = false;
        lastHitDirection = Vector3.zero;
        if (NotePoolManager.Instance != null)
        {
            NotePoolManager.Instance.ReturnNote(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerStay(Collider foreign)
    {
        if (isMissed || isDead || !initialized) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        // 🌟 [HIT ZONE GUARD] Update()의 스윕 판정과 동일하게, 판정선 1.2m 이내에서만 처리합니다.
        // 이 체크가 없으면 노트가 아직 멀리 있을 때도 BoxCollider 접촉으로 판정이 터질 수 있습니다.
        if (Mathf.Abs(transform.position.z - hitZ) >= 1.2f) return;

        FanSystem fan = foreign.GetComponentInParent<FanSystem>();
        if (fan == null) return;

        // 🌟 [STATIC TOUCH PREVENT & THRUST PREVENT]
        // Update() 스윕 경로와 동일하게, Hit는 3D 속도, 나머지는 XY 투영 속도로 최소 속도를 검사합니다.
        float currentSpeed = (type == NoteType.Hit)
            ? fan.Velocity.magnitude
            : Vector3.ProjectOnPlane(fan.Velocity, Vector3.forward).magnitude;

        if (currentSpeed < minSwingSpeed) return;

        // 🌟 [FAN STATE CHECK]
        // 일반 치기(Hit)는 부채가 접혀있을 때(!IsOpened)만, 베기/부치기는 펼쳐있어야(IsOpened) 작동합니다.
        bool fanStateCorrect = (type == NoteType.Hit) ? !fan.IsOpened : fan.IsOpened;
        if (!fanStateCorrect) return;

        // 🌟 [SWEPT DIRECTION UNIFICATION]
        // Update()의 스윕 경로와 완전히 동일하게 CheckSweptCollision을 사용해 보정된 moveDir을 얻습니다.
        // 물리 콜라이더가 접촉을 감지했더라도, 수학적 스윕이 확인하지 못하면 타격으로 인정하지 않습니다.
        if (CheckSweptCollision(fan, hitRadius, out Vector3 customMoveDir))
        {
            CheckHitSuccessWithDirection(fan, customMoveDir);
        }
    }


    /// <summary>
    /// 이전 프레임과 현재 프레임의 부채 중심 선분이 그리는 궤적을 5단계로 시간 보간하여,
    /// 해당 궤적(선분)들과 노트 중심 구체간의 최단 거리를 계산해 스윕 충돌 여부를 판정합니다.
    /// </summary>
    public bool CheckSweptCollision(FanSystem fan, float radius, out Vector3 customMoveDir)
    {
        customMoveDir = fan.Velocity.normalized;
        if (customMoveDir.sqrMagnitude < 0.001f)
        {
            customMoveDir = transform.forward; // 기본값
        }

        fan.GetFanSegment(out Vector3 currStart, out Vector3 currEnd);
        fan.GetPrevFanSegment(out Vector3 prevStart, out Vector3 prevEnd);

        Vector3 noteCenter = transform.position;
        int sampleCount = 5;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            Vector3 segStart = Vector3.Lerp(prevStart, currStart, t);
            Vector3 segEnd = Vector3.Lerp(prevEnd, currEnd, t);

            // 선분 [segStart, segEnd]와 구(noteCenter, radius)의 충돌 검사
            Vector3 ab = segEnd - segStart;
            Vector3 ap = noteCenter - segStart;

            float abLenSq = ab.sqrMagnitude;
            float r = 0f;
            if (abLenSq > 0.0001f)
            {
                r = Vector3.Dot(ap, ab) / abLenSq;
            }
            r = Mathf.Clamp01(r);

            Vector3 closestPoint = segStart + r * ab;
            float distSq = (noteCenter - closestPoint).sqrMagnitude;

            if (distSq <= radius * radius)
            {
                // 충돌 지점을 검출했을 때, 그 순간의 보정된 이동 방향을 리턴
                Vector3 centerPrev = (prevStart + prevEnd) * 0.5f;
                Vector3 centerCurr = (currStart + currEnd) * 0.5f;
                Vector3 sweepDir = centerCurr - centerPrev;
                if (sweepDir.sqrMagnitude > 0.001f)
                {
                    customMoveDir = sweepDir.normalized;
                }
                return true;
            }
        }

        return false;
    }

    void CheckHitSuccessWithDirection(FanSystem fan, Vector3 moveDir)
    {
        if (isDead) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        // 🌟 [THRUST/STAB DETECTION & PREVENTION]
        // 베기(Slashing)와 부치기(Fanning)는 3D 공간 상에서 궤적을 가지는 스윙이어야 합니다.
        // 만약 컨트롤러의 움직임이 주로 Z축(전진/후진) 방향이라면(찌르기 동작), 
        // 2D 투영 시 아주 미세한 떨림이 100% 방향 일치로 과장되어 타격으로 오인될 수 있습니다.
        // 이를 방지하기 위해 moveDir의 Z성분 비율이 maxThrustZRatio를 초과하면 찌르기로 판단하여 타격을 차단합니다.
        if (type == NoteType.Slashing || type == NoteType.Fanning)
        {
            if (Mathf.Abs(moveDir.z) > maxThrustZRatio)
            {
                return; // 찌르기는 타격 판정 무시
            }
        }

        bool isCorrectAction = false;
        float angleDiff = Vector3.Angle(moveDir, targetDirection);
        string judgment = "MISS";

        // 내적값 (부채의 옆면 법선과 스윙 방향의 관계)
        float dot = Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal));

        // 🌟 [PLANE PROJECTION FOR 2D ARROW ACCURACY]
        // 3D 스윙 방향(moveDir)에는 깊이(Z축) 성분이 포함되어 있어, 실제 2D 화살표 방향과 일치하더라도 Z축 왜곡에 의해 오차 각도가 인위적으로 커지는 현상이 발생합니다.
        // 이를 극복하기 위해 스윙 방향을 노트의 정면(XY 평면, 즉 Vector3.forward에 수직인 평면)에 투영하여 순수 2D 스윙 성분만을 추출합니다!
        Vector3 projectedMoveDir = Vector3.ProjectOnPlane(moveDir, Vector3.forward).normalized;
        if (projectedMoveDir.sqrMagnitude < 0.001f)
        {
            projectedMoveDir = moveDir; // 예외 예방 fallback
        }

        switch (type)
        {
            case NoteType.Slashing:
                {
                    // 1. 투영된 2D 스윙 방향 오차 검사 (베기는 양방향 궤적 허용)
                    float targetAngleDiff = Mathf.Min(Vector3.Angle(projectedMoveDir, targetDirection), Vector3.Angle(projectedMoveDir, -targetDirection));

                    // 2. 부채 날 정렬 오차 검사 (스윙 방향이 부채 날과 평행해야 함 = 법선과 수직 90도여야 함)
                    float fanAngleDiff = Mathf.Abs(90f - Vector3.Angle(moveDir, fan.FanNormal));

                    // 🌟 [WRIST SNAP DECOUPLING]
                    // 부채를 눕혀서 때리는 꼼수(Flat slap)를 방지하기 위해 날 정렬 오차가 40도 이하인지 검사하되,
                    // 최종 판정 등급(Perfect/Great/Good)은 오직 화살표와 스윙 일치도(targetAngleDiff)로만 결정합니다!
                    if (fanAngleDiff <= 40f && targetAngleDiff <= slashingFuzzyAngle)
                    {
                        isCorrectAction = true;
                        float totalAngleDiff = targetAngleDiff;
                        if (totalAngleDiff <= slashingPerfectAngle) judgment = "PERFECT";
                        else if (totalAngleDiff <= slashingGreatAngle) judgment = "GREAT";
                        else judgment = "GOOD";
                    }
                }
                break;

            case NoteType.Fanning:
                {
                    // 1. 투영된 2D 스윙 방향 오차 검사 (부치기는 화살표 정방향 단방향만 허용)
                    float targetAngleDiff = Vector3.Angle(projectedMoveDir, targetDirection);

                    // 2. 부채 면 정렬 오차 검사 (스윙 방향이 부채 법선과 평행해야 함 = 0도 또는 180도)
                    float fanAngleDiff = Mathf.Min(Vector3.Angle(moveDir, fan.FanNormal), Vector3.Angle(moveDir, -fan.FanNormal));

                    // 🌟 [WRIST SNAP DECOUPLING]
                    // 넓은 부채 면으로 확실히 미는 동작인지 검사하기 위해 정렬 오차가 40도 이하인지 감지하되,
                    // 최종 판정 등급은 오직 스윙 진행 방향 오차(targetAngleDiff)로만 판정합니다!
                    if (fanAngleDiff <= 40f && targetAngleDiff <= fanningFuzzyAngle)
                    {
                        isCorrectAction = true;
                        float totalAngleDiff = targetAngleDiff;
                        if (totalAngleDiff <= fanningPerfectAngle) judgment = "PERFECT";
                        else if (totalAngleDiff <= fanningGreatAngle) judgment = "GREAT";
                        else judgment = "GOOD";
                    }
                }
                break;

            case NoteType.Hit:
                // 🌟 [HIT NOTE ORIGINAL RULES RESTORED]
                // 일반 치기 노트는 방향 제한이나 타이밍 오차 계산 없이, 
                // 부채가 접힌 상태에서 타격이 성공했다면 즉시 100% PERFECT 판정을 부여합니다.
                isCorrectAction = true;
                judgment = "PERFECT";
                break;

            case NoteType.Boss:
                {
                    // 보스: 부채질 하듯이 쳐야 하므로 부채 면 정렬 오차가 60도 이하여야 함 (기존 dot >= 0.5f 와 동일)
                    float fanAngleDiff = Mathf.Min(Vector3.Angle(moveDir, fan.FanNormal), Vector3.Angle(moveDir, -fan.FanNormal));
                    if (fanAngleDiff <= 60f)
                    {
                        isCorrectAction = true;
                        if (lastHitDirection != Vector3.zero)
                        {
                            // 보스는 연타 시 반대 방향으로 휘둘러야 함
                            angleDiff = Vector3.Angle(moveDir, -lastHitDirection);
                        }
                        else
                        {
                            angleDiff = 0f; // 첫 타는 각도 판정 생략 혹은 완화
                        }

                        // 각도 차이에 따른 Fuzzy 판정
                        if (angleDiff <= bossPerfectAngle) judgment = "PERFECT";
                        else if (angleDiff <= bossGreatAngle) judgment = "GREAT";
                        else if (angleDiff <= bossGoodAngle) judgment = "GOOD";
                        else isCorrectAction = false;
                    }
                }
                break;
        }

        if (isCorrectAction)
        {
            hp--; 
            lastHitTime = Time.time;
            lastHitDirection = moveDir;

            // 차별화된 햅틱 피드백 설정
            float hapticIntensity = 0.5f;
            float hapticDuration = 0.1f;

            switch (type)
            {
                case NoteType.Slashing:
                    hapticIntensity = 0.8f;   // 날카롭고 강렬하게
                    hapticDuration = 0.03f;   // 매우 짧게
                    break;
                case NoteType.Fanning:
                    hapticIntensity = 0.4f;   // 묵직하고 다소 길게
                    hapticDuration = 0.18f;
                    break;
                case NoteType.Hit:
                    hapticIntensity = 0.5f;   // 표준 진동
                    hapticDuration = 0.08f;
                    break;
                case NoteType.Boss:
                    // 누적형 햅틱 피드백 (HP가 줄어들 때마다 폭발적 강도 부여)
                    if (hp == 2)
                    {
                        hapticIntensity = 0.6f;
                        hapticDuration = 0.06f;
                    }
                    else if (hp == 1)
                    {
                        hapticIntensity = 0.8f;
                        hapticDuration = 0.10f;
                    }
                    else // hp == 0
                    {
                        hapticIntensity = 0.98f;
                        hapticDuration = 0.18f;
                    }
                    break;
            }

            if (fan != null)
            {
                fan.TriggerHaptic(hapticIntensity, hapticDuration);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayHitSound(type);
                if (hp <= 0)
                {
                    isDead = true;
                    GameManager.Instance.AddScore(type, fan.Velocity.magnitude, judgment);
                    Deactivate();
                }
                else
                {
                    // 보스 연타 중에도 판정은 보여줌
                    GameManager.Instance.ShowTemporaryJudgment(judgment);
                }
            }

            if (type == NoteType.Boss && bossIndicator) 
            {
                bossIndicator.transform.localScale *= 0.65f;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 🛡️ [Rhythm Angle Range Defense - Decoupled for Easy Inspector Input]
        // 인스펙터 입력 편의를 위해 개별 값의 최소/최대 한계(0~90도 / 0~180도)만 검사합니다.
        slashingPerfectAngle = Mathf.Clamp(slashingPerfectAngle, 0f, 90f);
        slashingGreatAngle = Mathf.Clamp(slashingGreatAngle, 0f, 90f);
        slashingFuzzyAngle = Mathf.Clamp(slashingFuzzyAngle, 0f, 90f);

        fanningPerfectAngle = Mathf.Clamp(fanningPerfectAngle, 0f, 90f);
        fanningGreatAngle = Mathf.Clamp(fanningGreatAngle, 0f, 90f);
        fanningFuzzyAngle = Mathf.Clamp(fanningFuzzyAngle, 0f, 90f);

        bossPerfectAngle = Mathf.Clamp(bossPerfectAngle, 0f, 180f);
        bossGreatAngle = Mathf.Clamp(bossGreatAngle, 0f, 180f);
        bossGoodAngle = Mathf.Clamp(bossGoodAngle, 0f, 180f);

        maxThrustZRatio = Mathf.Clamp01(maxThrustZRatio);

        // General validation
        hitRadius = Mathf.Max(0.01f, hitRadius);
        minSwingSpeed = Mathf.Max(0f, minSwingSpeed);

        // 💡 [Delayed] 어트리뷰트 덕분에 플레이어가 입력 중일 때는 OnValidate가 실행되지 않으며,
        // 입력 완료(Enter 또는 포커스 해제) 시에만 확실하게 계층 구조를 강제 정렬합니다!
        EnforceAngleHierarchy();
    }
#endif
}
