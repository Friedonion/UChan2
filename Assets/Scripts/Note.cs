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

    public float minSwingSpeed = 0.1f; 
    public int hp = 1; 
    private bool isMissed = false;

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

        SetupVisuals();
        initialized = true;
    }

    public void SetupVisuals()
    {
        if (slashIndicator) slashIndicator.SetActive(false);
        if (fanIndicator) fanIndicator.SetActive(false);
        if (hitIndicator) hitIndicator.SetActive(false);
        if (bossIndicator) bossIndicator.SetActive(false);

        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.Cross(Vector3.forward, targetDirection));

        switch (type)
        {
            case NoteType.Slashing: if (slashIndicator) { slashIndicator.SetActive(true); slashIndicator.transform.localRotation = targetRotation; } break;
            case NoteType.Fanning: if (fanIndicator) { fanIndicator.SetActive(true); fanIndicator.transform.localRotation = targetRotation; } break;
            case NoteType.Hit: if (hitIndicator) { hitIndicator.SetActive(true); hitIndicator.transform.localRotation = targetRotation; } break;
            case NoteType.Boss: if (bossIndicator) { bossIndicator.SetActive(true); bossIndicator.transform.localRotation = targetRotation; } break;
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

        // 판정선을 지나쳤을 때 (progress > 1.0f 면 hitZ를 넘은 것)
        if (!isMissed && progress > 1.1f)
        {
            isMissed = true;
            if (GameManager.Instance != null) GameManager.Instance.NoteMissed();
            Destroy(gameObject, 0.1f);
        }
    }

    private void OnTriggerStay(Collider foreign)
    {
        if (isMissed || !initialized) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        FanSystem fan = foreign.GetComponentInParent<FanSystem>();
        if (fan != null)
        {
            bool fanStateCorrect = (type == NoteType.Hit) ? !fan.IsOpened : fan.IsOpened;
            if (fanStateCorrect) CheckHitSuccess(fan);
        }
    }

    void CheckHitSuccess(FanSystem fan)
    {
        float currentSpeed = fan.Velocity.magnitude;
        if (currentSpeed < minSwingSpeed) return;

        Vector3 moveDir = fan.Velocity.normalized;
        bool isCorrectAction = false;
        bool isCorrectDirection = false;

        switch (type)
        {
            case NoteType.Slashing:
                if (Mathf.Abs(Vector3.Dot(moveDir, targetDirection)) > 0.5f) isCorrectDirection = true;
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) < 0.4f) isCorrectAction = true;
                break;
            case NoteType.Fanning:
                if (Vector3.Dot(moveDir, targetDirection) > 0.5f) isCorrectDirection = true;
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) > 0.6f) isCorrectAction = true;
                break;
            case NoteType.Hit:
                isCorrectDirection = true;
                isCorrectAction = true;
                break;
            case NoteType.Boss:
                if (Mathf.Abs(Vector3.Dot(moveDir, fan.FanNormal)) > 0.6f) isCorrectAction = true;
                if (lastHitDirection == Vector3.zero) { if (Mathf.Abs(Vector3.Dot(moveDir, targetDirection)) > 0.5f) isCorrectDirection = true; }
                else { if (Vector3.Dot(moveDir, lastHitDirection) < -0.5f) isCorrectDirection = true; }
                break;
        }

        if (isCorrectAction && isCorrectDirection)
        {
            hp--; 
            lastHitTime = Time.time;
            lastHitDirection = moveDir;

            if (GameManager.Instance != null) GameManager.Instance.PlayHitSound(type);
            if (fan != null) fan.TriggerHaptic(type == NoteType.Boss ? 0.8f : 0.5f);
            if (type == NoteType.Boss && bossIndicator) bossIndicator.transform.localScale *= 0.65f;

            if (hp <= 0)
            {
                if (GameManager.Instance != null) GameManager.Instance.AddScore(type, fan.Velocity.magnitude);
                Destroy(gameObject);
            }
        }
    }
}
