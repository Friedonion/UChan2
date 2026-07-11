using UnityEngine;

public class WindTrailEffect : MonoBehaviour
{
    [Header("Trail Settings")]
    public float speedThreshold = 0.8f;
    public float trailDuration = 1.2f; // 궤적이 허공에 남아있는 시간 (초 단위)
    public Color foldedColor = new Color(0.1f, 0.8f, 1f, 1f); // 비트세이버 스타일 파란색
    public Color slashedColor = new Color(1f, 0.2f, 0.2f, 1f); // 베기 전용 빨간색

    private FanSystem fanSystem;
    private SwordTrail swordTrail;
    private Transform baseTransform;
    private Transform tipTransform;

    void Awake()
    {
        fanSystem = GetComponentInParent<FanSystem>();

        // 비트세이버 궤적을 그리기 위한 두 점(Base, Tip) 생성
        baseTransform = new GameObject("TrailBase").transform;
        baseTransform.SetParent(transform);
        baseTransform.localPosition = Vector3.zero;

        tipTransform = new GameObject("TrailTip").transform;
        tipTransform.SetParent(transform);
        tipTransform.localPosition = new Vector3(0, 0, 0.35f); 

        // 기존 TrailRenderer가 있다면 제거
        TrailRenderer oldTrail = GetComponent<TrailRenderer>();
        if (oldTrail != null) Destroy(oldTrail);

        swordTrail = gameObject.AddComponent<SwordTrail>();
        swordTrail.basePoint = baseTransform;
        swordTrail.tipPoint = tipTransform;
        swordTrail.trailTime = trailDuration;
        swordTrail.trailColor = foldedColor;
    }

    void Start()
    {
        if (fanSystem == null) fanSystem = GetComponentInParent<FanSystem>();
    }

    void Update()
    {
        if (fanSystem == null) return;

        bool isFastEnough = fanSystem.Velocity.magnitude > speedThreshold;

        // 인스펙터에서 실시간으로 수정 가능하도록 매 프레임 업데이트
        swordTrail.trailTime = trailDuration;

        // 부채 길이에 맞춰 Tip 위치 업데이트
        tipTransform.localPosition = new Vector3(0, 0, fanSystem.ribLength);

        if (!fanSystem.IsOpened)
        {
            // 접기 상태: 비트세이버 단봉 스타일 (파란색)
            swordTrail.trailColor = foldedColor;
        }
        else
        {
            // 펼치기 상태: 베기/부치기 상관없이 무조건 빨간색 궤적 유지
            swordTrail.trailColor = slashedColor;
        }

        // 동작 종류(베기/부치기)에 상관없이 속도만 빠르면 무조건 궤적 생성!
        // 이렇게 하면 부채를 돌리거나, 앞으로 밀거나(부치기), 옆으로 휠 때(베기)
        // 궤적이 3D 공간을 가르며 자연스러운 꼬임을 만듭니다.
        // 단, 설정에서 궤적 효과를 껐다면 즉시 궤적을 지우고 새로 그리지 않습니다.
        bool trailEnabled = VolumeSettings.TrailEffectOn;
        swordTrail.SetEmitting(isFastEnough && trailEnabled);
        if (!trailEnabled) swordTrail.ClearTrail();
    }

    public static WindTrailEffect AttachTo(FanSystem fan)
    {
        GameObject obj = new GameObject("WindTrail");
        obj.transform.SetParent(fan.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        return obj.AddComponent<WindTrailEffect>();
    }
}
