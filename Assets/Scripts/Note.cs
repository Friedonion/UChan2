using UnityEngine;

public enum NoteType { Slashing, Fanning, Normal }

public class Note : MonoBehaviour
{
    public NoteType type = NoteType.Normal;
    public float speed = 3.0f;
    public float lifeTime = 5.0f;

    [Header("Visual Elements")]
    public GameObject slashIndicator; // 베기용 선
    public GameObject fanIndicator;   // 부치기용 화살표
    public GameObject normalIndicator; // 일반용 점

    public float minSwingSpeed = 1.0f; 
    public int hp = 1; // 노트의 체력 (연타 노트용)
    private bool isMissed = false;

    void Start()
    {
        SetupVisuals();
        Destroy(gameObject, lifeTime);
    }

    void SetupVisuals()
    {
        // 모든 인디케이터를 일단 끕니다.
        if (slashIndicator) slashIndicator.SetActive(false);
        if (fanIndicator) fanIndicator.SetActive(false);
        if (normalIndicator) normalIndicator.SetActive(false);

        // 타입에 따라 적절한 인디케이터를 켜고 색상을 바꿉니다.
        Renderer renderer = GetComponent<Renderer>();
        
        switch (type)
        {
            case NoteType.Slashing:
                if (slashIndicator) slashIndicator.SetActive(true);
                if (renderer) renderer.material.color = Color.red; // 베기는 빨간색
                break;
            case NoteType.Fanning:
                if (fanIndicator) fanIndicator.SetActive(true);
                if (renderer) renderer.material.color = Color.blue; // 부치기는 파란색
                break;
            case NoteType.Normal:
                if (normalIndicator) normalIndicator.SetActive(true);
                if (renderer) renderer.material.color = Color.white; // 일반은 하얀색
                break;
        }
    }

    void Update()
    {
        transform.Translate(Vector3.back * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider foreign)
    {
        FanSystem fan = foreign.GetComponentInParent<FanSystem>();
        if (fan != null && fan.IsOpened)
        {
            CheckHitSuccess(fan);
        }
    }

    void CheckHitSuccess(FanSystem fan)
    {
        // 휘두르는 속도가 너무 느리면 무시
        if (fan.Velocity.magnitude < minSwingSpeed) return;

        Vector3 moveDir = fan.Velocity.normalized;
        Vector3 fanNormal = fan.FanNormal;
        float alignment = Mathf.Abs(Vector3.Dot(moveDir, fanNormal));

        bool isSuccess = false;

        switch (type)
        {
            case NoteType.Slashing:
                if (alignment < 0.4f) isSuccess = true; 
                break;
            case NoteType.Fanning:
                if (alignment > 0.6f) isSuccess = true; 
                break;
            case NoteType.Normal:
                isSuccess = true;
                break;
        }

        if (isSuccess)
        {
            hp--; // 체력 감소
            
            // 시각적 피드백: 맞을 때마다 조금씩 작아짐
            transform.localScale *= 0.85f;

            if (hp <= 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddScore(type, fan.Velocity.magnitude);
                }
                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"🤜 히트! (남은 횟수: {hp})");
            }
        }
    }

    void OnDestroy()
    {
        // Miss 판정은 플레이어 뒤쪽의 트리거 영역에서 처리하는 것을 추천합니다.
    }
}
