using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// "Sync Test" 차트(BasicScene, StreamingAssets/Charts/Sync)에서만 동작한다.
// 이 차트는 일정한 박자(Sync.wav의 클릭음)와 정확히 같은 시각에 노트가 z=0에 도착하도록
// 만들어져 있다. 곡 재생 중 -/+ 버튼으로 VolumeSettings.SyncOffsetMs를 실시간으로 조절하면서,
// 노트 도착 시점을 클릭음에 맞춰볼 수 있다.
// (여기서 조절한 값은 전역 설정이라 다른 곡을 플레이할 때도 그대로 적용된다)
public class SyncTestManager : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;
    public Transform canvasTransform;

    [Header("Fixed Placement")]
    [Tooltip("BasicScene 정면(월드 +Z, 리센터된 플레이어 기준) 대비 오른쪽으로 꺾는 각도")]
    public float angleOffsetDegrees = 45f;
    public float distanceFromCenter = 2.0f;
    public float fixedHeight = 1.3f;

    [Header("UI")]
    public TextMeshProUGUI offsetText;
    public Button minusBtn;
    public Button plusBtn;

    const float Step = 2f; // ms
    const string TargetSongName = "Sync Test";

    void Start()
    {
        StartCoroutine(RunGuarded());
    }

    IEnumerator RunGuarded()
    {
        // 다른 곡에서는 이 매니저가 아무것도 하지 않도록, 로드된 차트가 확정될 때까지 기다렸다가 확인한다.
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.currentChart != null);

        if (GameManager.Instance.currentChart.songName != TargetSongName)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            yield break;
        }

        VolumeSettings.Load();
        RefreshText();

        if (minusBtn != null) minusBtn.onClick.AddListener(() => ChangeOffset(-Step));
        if (plusBtn != null) plusBtn.onClick.AddListener(() => ChangeOffset(+Step));

        if (panelRoot != null) panelRoot.SetActive(true);

        // 플레이어 시선/위치를 전혀 참조하지 않고, 리센터된 플레이어 기준 월드 좌표에 완전히 고정 소환한다.
        if (canvasTransform == null) yield break;

        Vector3 dir = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * Vector3.forward;
        Vector3 pos = dir * distanceFromCenter;
        pos.y = fixedHeight;
        canvasTransform.position = pos;
        canvasTransform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void ChangeOffset(float deltaMs)
    {
        VolumeSettings.SetSyncOffset(VolumeSettings.SyncOffsetMs + deltaMs);
        RefreshText();
    }

    void RefreshText()
    {
        if (offsetText == null) return;
        int ms = Mathf.RoundToInt(VolumeSettings.SyncOffsetMs);
        offsetText.text = $"싱크 오프셋: {(ms >= 0 ? "+" : "")}{ms}ms";
    }
}
