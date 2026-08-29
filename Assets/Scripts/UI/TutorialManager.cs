using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using TMPro;

// "Tutorial" 차트(BasicScene, StreamingAssets/Charts/Tutorial)에서만 동작한다.
// 설명 중엔 GameManager를 Pause시켜 시간을 멈추고, 트리거를 당기면 패널을 숨긴 뒤
// 실제 연습용 노트를 NoteSpawner에 큐잉해서 날려보내고, 판정이 끝날 시간만큼 기다린 뒤
// 다시 Pause하고 다음 설명으로 넘어간다.
public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public string title;
        [TextArea(2, 4)] public string body;
        public bool hasPractice;
        public NoteType practiceType;
        [Tooltip("0이면 일반 노트, 0보다 크면 롱노트로 취급 (초 단위 유지 시간)")]
        public float holdDuration;
    }

    [Header("Canvas")]
    public GameObject panelRoot;
    public Transform canvasTransform;
    public float distanceFromPlayer = 2.0f;

    [Header("Text")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public TextMeshProUGUI promptText;

    [Header("Practice Spawn")]
    public int practiceLane = 1;
    public int practiceRow = 0;
    public int practiceEndLane = 3;
    [Tooltip("노트가 도착한 뒤 판정이 끝났다고 보고 다음 단계로 넘어가기까지 추가로 기다리는 여유 시간(초)")]
    public float resolveBuffer = 1.5f;

    [Header("Scene")]
    public string nextSceneName = "MusicSelectUI";

    [Header("Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>
    {
        new TutorialStep {
            title = "우찬2에 오신 걸 환영합니다",
            body  = "부채를 휘둘러 날아오는 노트를 쳐내는 VR 리듬게임입니다.\n트리거를 당기면 다음 설명으로 넘어갑니다."
        },
        new TutorialStep {
            title = "치기 (Hit)",
            body  = "부채를 접은 상태로, 정면에서 오는 노트를 향해 쳐냅니다.\n트리거를 당기면 예시 노트가 날아옵니다.",
            hasPractice = true, practiceType = NoteType.Hit
        },
        new TutorialStep {
            title = "베기 (Slashing)",
            body  = "부채를 편 상태로, 화살표 방향을 따라 대각선으로 베어냅니다.",
            hasPractice = true, practiceType = NoteType.Slashing
        },
        new TutorialStep {
            title = "부치기 (Fanning)",
            body  = "부채를 편 상태로, 화살표가 가리키는 방향을 향해 부채질하듯 밀어냅니다.",
            hasPractice = true, practiceType = NoteType.Fanning
        },
        new TutorialStep {
            title = "보스 (Boss)",
            body  = "부채를 편 상태로 강하게 휘두르세요. 연속으로 맞힐 때는 매번 반대 방향으로 휘둘러야 합니다.",
            hasPractice = true, practiceType = NoteType.Boss
        },
        new TutorialStep {
            title = "롱노트 시작 - 접힘",
            body  = "부채를 접은 상태로 노트에 살짝 닿기만 해도 홀드가 시작됩니다.\n이후 가이드라인을 따라 부채 상태를 유지하세요.",
            hasPractice = true, practiceType = NoteType.HoldFolded, holdDuration = 3f
        },
        new TutorialStep {
            title = "롱노트 시작 - 펼침",
            body  = "부채를 편 상태로 노트에 살짝 닿기만 해도 홀드가 시작됩니다.\n이후 가이드라인을 따라 부채 상태를 유지하세요.",
            hasPractice = true, practiceType = NoteType.HoldOpen, holdDuration = 3f
        },
        new TutorialStep {
            title = "벽 (Wall)",
            body  = "공격할 수 없는 장애물입니다. 머리에 닿으면 미스 처리되니 몸을 움직여 피하세요.",
            hasPractice = true, practiceType = NoteType.Wall
        },
        new TutorialStep {
            title = "준비 완료!",
            body  = "이제 곡을 선택해 플레이를 시작해볼까요?"
        },
    };

    const string TargetSongName = "Tutorial";

    private NoteSpawner spawner;
    private Camera vrCam;

    void Start()
    {
        var xrOrigin = FindObjectOfType<XROrigin>();
        vrCam = xrOrigin != null ? xrOrigin.Camera : Camera.main;
        spawner = FindObjectOfType<NoteSpawner>();

        StartCoroutine(RunGuarded());
    }

    // 다른 곡(Sync Test 등)에서는 이 매니저가 아무것도 하지 않도록, 로드된 차트가 확정될 때까지
    // 기다렸다가 확인한다. (이 가드가 없어서 다른 차트를 플레이할 때도 튜토리얼 패널이 뜨는 버그가 있었음)
    IEnumerator RunGuarded()
    {
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.currentChart != null);

        if (GameManager.Instance.currentChart.songName != TargetSongName)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            yield break;
        }

        StartCoroutine(InitCanvas());
        StartCoroutine(RunSequence());
    }

    // XR 트래킹이 초기화될 때까지 2프레임 대기 후 캔버스를 플레이어 정면에 1회 고정 (IntroManager와 동일 패턴)
    IEnumerator InitCanvas()
    {
        yield return null;
        yield return null;

        if (canvasTransform == null || vrCam == null) yield break;

        Vector3 forward = new Vector3(vrCam.transform.forward.x, 0f, vrCam.transform.forward.z).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

        canvasTransform.position = vrCam.transform.position + forward * distanceFromPlayer;
        canvasTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    IEnumerator RunSequence()
    {
        // spawner.IsActive까지 확인해야 한다: GameManager.StartGame()은 currentState를 Playing으로
        // 동기적으로 먼저 바꾸고, NoteSpawner.StartPlaying()은 그보다 한참 뒤(startDelay 경과 후)에 호출된다.
        // currentState만 보고 여기서 바로 PauseGame()하면 startDelay 대기 코루틴까지 timeScale=0으로 멈춰버려서,
        // 첫 연습 단계가 spawner.IsActive를 기다리다 영원히 풀리지 않는 교착 상태에 빠진다.
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing
            && spawner != null && spawner.IsActive);

        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStep step = steps[i];

            if (GameManager.Instance.currentState == GameState.Playing)
                GameManager.Instance.PauseGame();
            ShowStep(step, i == steps.Count - 1);

            yield return WaitForTriggerPull();

            if (!step.hasPractice) continue;

            HidePanel();

            // 스포너가 아직 준비되지 않았다면(씬 로드 직후 등) 준비될 때까지만 짧게 대기
            yield return new WaitUntil(() => spawner != null && spawner.IsActive);

            if (GameManager.Instance.currentState == GameState.Paused)
                GameManager.Instance.ResumeGame();

            Vector3 dir = (step.practiceType == NoteType.Wall) ? Vector3.zero : Vector3.right;
            spawner.QueuePracticeNote(
                step.practiceType, practiceLane, practiceRow, dir,
                step.holdDuration,
                step.holdDuration > 0f ? practiceEndLane : -1,
                step.holdDuration > 0f ? practiceRow : -1);

            float travelTime = GameManager.Instance.currentChart != null ? GameManager.Instance.currentChart.travelTime : 4f;
            float waitTime = travelTime + step.holdDuration + resolveBuffer;
            yield return new WaitForSeconds(waitTime);
        }

        yield return FinishTutorial();
    }

    void ShowStep(TutorialStep step, bool isLast)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (titleText != null) titleText.text = step.title;
        if (bodyText != null) bodyText.text = step.body;
        if (promptText != null)
        {
            promptText.text = isLast ? "(트리거를 당겨 시작하기)"
                : step.hasPractice ? "(트리거를 당기면 예시 노트가 날아옵니다)"
                : "(트리거를 당겨 계속)";
        }
    }

    void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // 트리거를 "당기는 순간"(rising edge)만 반응한다. Time.timeScale이 0(Pause 중)이어도
    // 프레임 기반 대기(yield return null)라 정상적으로 동작한다.
    IEnumerator WaitForTriggerPull()
    {
        while (GetTriggerHeld()) yield return null; // 이전 트리거 입력이 아직 눌려있으면 먼저 떼기를 기다림

        bool prev = false;
        while (true)
        {
            bool now = GetTriggerHeld();
            if (now && !prev) yield break;
            prev = now;
            yield return null;
        }
    }

    bool GetTriggerHeld()
    {
        bool triggerNow = false;

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) triggerNow = true;
#endif

        var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool lt);
        right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rt);

        return triggerNow || lt || rt;
    }

    IEnumerator FinishTutorial()
    {
        HidePanel();
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Paused)
            GameManager.Instance.ResumeGame();
        yield return new WaitForSeconds(0.3f);
        SceneManager.LoadScene(nextSceneName);
    }
}
