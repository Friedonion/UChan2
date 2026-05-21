using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR; // 안정적인 햅틱을 위해 추가

public class FanSystem : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionReference toggleAction; 
    public InputActionReference hapticAction; // 진동용 액션 (참조용으로 유지)

    [Header("Haptic Settings")]
    public float defaultHapticIntensity = 0.5f;
    public float defaultHapticDuration = 0.1f;

    public void TriggerHaptic(float intensity = -1f, float duration = -1f)
    {
        float finalIntensity = (intensity < 0) ? defaultHapticIntensity : intensity;
        float finalDuration = (duration < 0) ? defaultHapticDuration : duration;

        // 오브젝트 이름이나 액션 이름을 통해 어느 손인지 판별합니다.
        // 보통 "Left Hand", "Right Hand" 등으로 이름이 지어지기 때문입니다.
        bool isLeft = transform.name.ToLower().Contains("left") || 
                      (hapticAction != null && hapticAction.name.ToLower().Contains("left"));
        
        XRNode node = isLeft ? XRNode.LeftHand : XRNode.RightHand;
        
        // 유니티 표준 XR 장치에서 해당 노드의 장치를 찾아 진동을 보냅니다.
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, finalIntensity, finalDuration);
        }
    }

    [Header("Fan Visual Settings")]
    [Range(3, 20)]
    public int ribCount = 8;
    public float maxOpenAngle = 130f;
    public float ribLength = 0.35f;
    public float animationSpeed = 15f;

    private GameObject[] ribs;
    private bool isOpened = false;
    private float currentOpenAmount = 0f;
    
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    [Header("Movement Detection")]
    public float fanningThreshold = 2.0f;

    // --- 부채 판정 보정용 변수 추가 ---
    private const int SWING_BUFFER_SIZE = 5;
    private struct PoseSample
    {
        public Vector3 position;
        public Quaternion rotation;
        public float time;
    }
    private System.Collections.Generic.Queue<PoseSample> poseBuffer = new System.Collections.Generic.Queue<PoseSample>();

    private Vector3 prevSegmentStart;
    private Vector3 prevSegmentEnd;

    void Start()
    {
        CreatePrototypeFan();
        // 🌟 [WRIST SNAP SUPPORT] 손목 까닥임(회전) 반응성을 높이기 위해 피벗이 아닌 부채 중심부(Center) 기준 위치를 사용합니다.
        lastPosition = transform.position + transform.forward * (ribLength * 0.5f);

        // 이전 프레임 세그먼트 초기화
        GetFanSegment(out prevSegmentStart, out prevSegmentEnd);
    }

    void CreatePrototypeFan()
    {
        ribs = new GameObject[ribCount];
        for (int i = 0; i < ribCount; i++)
        {
            GameObject pivot = new GameObject($"RibPivot_{i}");
            pivot.transform.SetParent(this.transform);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;

            GameObject rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = $"Rib_Model_{i}";
            rib.transform.SetParent(pivot.transform);
            
            // 시각적 크기는 유지하되, 콜라이더는 판정을 위해 약간 두껍게 설정
            rib.transform.localScale = new Vector3(0.02f, 0.005f, ribLength);
            rib.transform.localPosition = new Vector3(0, 0, ribLength / 2);
            
            if (rib.TryGetComponent<BoxCollider>(out var col))
            {
                col.isTrigger = true;
                // 콜라이더만 살짝 더 두껍게 (베기/부치기 판정 강화)
                col.size = new Vector3(3.0f, 5.0f, 1.1f); 
            }

            // 빠른 움직임 감지를 위해 Rigidbody 추가 및 설정
            Rigidbody rb = rib.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // ContinuousSpeculative: Kinematic 오브젝트의 빠른 움직임에 가장 적합한 충돌 모드
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            ribs[i] = pivot;
        }
    }

    void Update()
    {
        HandleInput();
        AnimateFan();
        CalculateVelocity();
    }

    void HandleInput()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            float inputVal = toggleAction.action.ReadValue<float>();
            isOpened = inputVal > 0.1f;
        }
    }

    void AnimateFan()
    {
        float targetAmount = isOpened ? 1f : 0f;
        currentOpenAmount = Mathf.Lerp(currentOpenAmount, targetAmount, Time.deltaTime * animationSpeed);
        
        float startAngle = -(maxOpenAngle / 2) * currentOpenAmount;
        float stepAngle = (maxOpenAngle / (ribCount - 1)) * currentOpenAmount;

        for (int i = 0; i < ribCount; i++)
        {
            float targetAngle = startAngle + (stepAngle * i);
            ribs[i].transform.localRotation = Quaternion.Euler(targetAngle, 0, 0);
        }
    }

    void CalculateVelocity()
    {
        // 🌟 [WRIST SNAP DETECTION IMPROVEMENT]
        // 손잡이 피벗(transform.position)은 손목만 까닥일 때 거의 움직이지 않으므로 속도가 매우 낮게 계산됩니다.
        // 이를 극복하기 위해 부채의 중간 지점(Center Point)의 월드 좌표 변화를 기준으로 선속도를 추적합니다!
        // 이렇게 하면 손목을 조금만 스냅해도 부채 날 끝부분과 몸통이 크게 회전하여 높은 속도와 명확한 방향이 검출됩니다.
        Vector3 fanCenter = transform.position + transform.forward * (ribLength * 0.5f);

        // 1. 현재 포즈 샘플 기록
        PoseSample sample = new PoseSample
        {
            position = fanCenter,
            rotation = transform.rotation,
            time = Time.time
        };
        poseBuffer.Enqueue(sample);
        if (poseBuffer.Count > SWING_BUFFER_SIZE)
        {
            poseBuffer.Dequeue();
        }

        // 2. 가중 이동 평균(Weighted Moving Average)으로 스무스 속도 계산
        if (poseBuffer.Count >= 2)
        {
            PoseSample[] samples = poseBuffer.ToArray();
            Vector3 totalVelocity = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 1; i < samples.Length; i++)
            {
                float dt = samples[i].time - samples[i - 1].time;
                if (dt > 0.0001f)
                {
                    Vector3 v = (samples[i].position - samples[i - 1].position) / dt;
                    // 최신 샘플일수록 선형적으로 높은 가중치 부여 (i가 클수록 최신)
                    float weight = i;
                    totalVelocity += v * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight > 0f)
            {
                currentVelocity = totalVelocity / totalWeight;
            }
            else
            {
                currentVelocity = Vector3.zero;
            }
        }
        else
        {
            currentVelocity = (fanCenter - lastPosition) / Time.deltaTime;
        }

        lastPosition = fanCenter;
    }

    // 부채의 피벗(Start)과 살 끝부분(End)을 월드 좌표로 구함
    public void GetFanSegment(out Vector3 start, out Vector3 end)
    {
        start = transform.position;
        // 부채는 로컬 Z축(transform.forward) 방향으로 펼쳐집니다.
        end = transform.position + transform.forward * ribLength;
    }

    public void GetPrevFanSegment(out Vector3 start, out Vector3 end)
    {
        start = prevSegmentStart;
        end = prevSegmentEnd;
    }

    void LateUpdate()
    {
        // LateUpdate에서 현재 프레임 세그먼트를 이전 프레임 값으로 캐싱
        GetFanSegment(out prevSegmentStart, out prevSegmentEnd);
    }

    public bool IsOpened => isOpened;
    public Vector3 Velocity => currentVelocity;
    // 수정: 위아래로 펼쳐진 부채의 넓은 면은 옆쪽(X축)을 바라봅니다.
    public Vector3 FanNormal => transform.right; 

}
