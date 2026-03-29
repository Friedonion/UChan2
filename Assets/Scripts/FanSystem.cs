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

    void Start()
    {
        CreatePrototypeFan();
        lastPosition = transform.position;
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
            
            // 앞(Z)으로 뻗고 위아래(Y)로 펼쳐짐
            rib.transform.localScale = new Vector3(0.02f, 0.005f, ribLength);
            rib.transform.localPosition = new Vector3(0, 0, ribLength / 2);
            
            if (rib.TryGetComponent<BoxCollider>(out var col)) col.isTrigger = true;
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
        currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    public bool IsOpened => isOpened;
    public Vector3 Velocity => currentVelocity;
    // 수정: 위아래로 펼쳐진 부채의 넓은 면은 옆쪽(X축)을 바라봅니다.
    public Vector3 FanNormal => transform.right; 
}
