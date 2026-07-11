using UnityEngine;
using Unity.XR.CoreUtils;
using System.Collections;

// SongSelectCanvas에 붙이면 씬 시작 시 XR 카메라 정면 앞에 고정 배치됨
public class CanvasPositioner : MonoBehaviour
{
    public float distanceFromCamera = 2.0f;
    public float heightOffset       = 0.0f;

    IEnumerator Start()
    {
        // XR 트래킹이 초기화될 때까지 2프레임 대기
        yield return null;
        yield return null;

        var xrOrigin = FindObjectOfType<XROrigin>();
        Camera cam = xrOrigin != null ? xrOrigin.Camera : Camera.main;

        if (cam != null)
        {
            // 플레이어가 바라보는 실제 정면 방향 계산 (Y축 무시)
            Vector3 forward = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            // 카메라 위치 기준, 플레이어 시야 정면에 UI 배치
            transform.position = cam.transform.position + forward * distanceFromCamera + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        else
        {
            transform.position = new Vector3(0f, 1.5f + heightOffset, distanceFromCamera);
            transform.rotation = Quaternion.identity;
        }
    }
}
