using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
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
    [Tooltip("BasicScene 정면(월드 +Z, 리센터된 플레이어 기준) 대비 오른쪽으로 꺾는 각도")]
    public float angleOffsetDegrees = 45f;
    public float distanceFromCenter = 2.0f;
    public float fixedHeight = 1.3f;

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
    [Tooltip("체크 해제하면 Miss여도 재시도 없이 노트를 한 번만 스폰하고 바로 다음 단계로 넘어간다 (디버그용)")]
    public bool retryUntilSuccess = true;

    [Header("Scene")]
    public string nextSceneName = "MusicSelectUI";

    [Header("Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>
    {
        new TutorialStep {
            title = "풍류에 오신 걸 환영합니다",
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
            title = "체력 (HP)",
            body  = "노트를 놓치면 체력이 10 줄어들어요. 체력이 0이 되면 그 자리에서 곡이 끝나버리니 주의하세요.\n(지금은 연습 중이라 실제로 체력이 줄지는 않아요)"
        },
        new TutorialStep {
            title = "롱노트 - 접힘",
            body  = "부채를 접은 상태로 노트에 살짝 닿으면 홀드가 시작됩니다.\n중요한 건 그 다음이에요 - 가이드라인을 따라가는 내내 부채를 접은 상태로 유지해야 합니다.",
            hasPractice = true, practiceType = NoteType.HoldFolded, holdDuration = 3f
        },
        new TutorialStep {
            title = "롱노트 - 펼침",
            body  = "잘하셨습니다. 이번에는 같은 방식으로 부채를 펼친 채 유지해봅시다.",
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

    // 롱노트는 이론상 세 가지 판정(시작 노트 터치 / 가이드라인 유지 / 끝 노트 터치)으로 이루어진다.
    // 앞의 두 개는 결과가 그때그때 이 bool들에 반영되고, 가이드라인 유지 여부는 판정 시점에
    // LongNoteGuide.HoldFailed를 직접 읽어서 확인한다(별도 bool로 안 들고 있어도 즉시 조회 가능).
    // 일반 노트는 시작 판정 하나뿐이므로 endOk는 애초에 "필요 없음(true)"으로 시작한다.
    private bool practiceStartOk;
    private bool practiceEndOk;

    // Wall 전용: 벽은 "부딪히면 그 즉시 확정 실패"라서 롱노트처럼 Endpoint까지 기다렸다가 판정할
    // 이유가 없다(기다려봤자 결과가 바뀌지 않음). 반대로 "안 맞고 통과"는 벽 길이만큼 판정선을
    // 완전히 지나가야 WallDodged가 불리는데, 이 통과 소요 시간은 travelTime만으로는 못 구해서
    // 고정 대기 대신 실제 결과 이벤트(성공/실패)가 올 때까지 직접 기다린다.
    private bool practiceMissed;

    void OnEnable()
    {
        GameManager.OnNoteResolved += HandlePracticeResolved;
        GameManager.OnNoteMissed += HandlePracticeMissed;
    }

    void OnDisable()
    {
        GameManager.OnNoteResolved -= HandlePracticeResolved;
        GameManager.OnNoteMissed -= HandlePracticeMissed;
    }

    // OnNoteResolved는 롱노트일 때 시작 노트에서 한 번, 끝 노트에서 한 번, 순서대로 두 번 온다.
    void HandlePracticeResolved()
    {
        if (!practiceStartOk) practiceStartOk = true;
        else practiceEndOk = true;
    }

    void HandlePracticeMissed() => practiceMissed = true;

    void Start()
    {
        HidePanel();
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

        PlaceCanvas();
        StartCoroutine(RunSequence());
    }

    // SyncTestManager와 동일한 방식: 플레이어 시선/위치를 전혀 참조하지 않고, 리센터된 플레이어
    // 기준 월드 좌표(정면 대비 angleOffsetDegrees만큼 우측)에 완전히 고정 소환한다.
    void PlaceCanvas()
    {
        if (canvasTransform == null) return;

        Vector3 dir = Quaternion.Euler(0f, angleOffsetDegrees, 0f) * Vector3.forward;
        Vector3 pos = dir * distanceFromCenter;
        pos.y = fixedHeight;
        canvasTransform.position = pos;
        canvasTransform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    IEnumerator RunSequence()
    {
        // currentState만 확정되면 바로 시작한다(=거의 즉시). spawner.IsActive는 아직 기다리지 않는다 -
        // GameManager.StartGame()이 currentState를 Playing으로 동기적으로 먼저 바꾸고,
        // NoteSpawner.StartPlaying()은 그보다 한참 뒤(startDelay≈3초 경과 후)에 호출되기 때문에,
        // 여기서 spawner.IsActive까지 기다리면 연습이 필요 없는 첫 인트로 패널조차 그 3초를 그대로 먹는다.
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing);

        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStep step = steps[i];

            // 연습이 있는 스텝은 노트를 스폰해야 하니 spawner가 준비될 때까지 기다린다.
            if (step.hasPractice)
                yield return new WaitUntil(() => spawner != null && spawner.IsActive);

            // spawner가 아직 준비 안 된 상태(=GameManager의 시작 카운트다운이 아직 안 끝남)에서
            // PauseGame()을 걸면 그 카운트다운 코루틴 자체가 timeScale=0에 걸려 영원히 멈춰버린다.
            // 그래서 준비되기 전까지는(=주로 첫 인트로 패널) Pause 없이 패널만 먼저 보여준다.
            if (GameManager.Instance.currentState == GameState.Playing && spawner != null && spawner.IsActive)
                GameManager.Instance.PauseGame();
            ShowStep(step, i == steps.Count - 1);

            yield return WaitForTriggerPull();

            if (!step.hasPractice) continue;

            HidePanel();

            if (GameManager.Instance.currentState == GameState.Paused)
                GameManager.Instance.ResumeGame();

            Vector3 dir = (step.practiceType == NoteType.Wall) ? Vector3.zero : Vector3.right;
            bool isHoldStep = step.holdDuration > 0f;
            bool isWallStep = step.practiceType == NoteType.Wall;
            float travelTime = GameManager.Instance.currentChart != null ? GameManager.Instance.currentChart.travelTime : 4f;
            float naturalDuration = travelTime + step.holdDuration; // 노트가 스폰돼서 Endpoint에 도달하기까지 걸리는 시간

            // 판정 도중에는 아무것도 확인하지 않고, 노트가 실제로 Endpoint에 도달할 시간만큼 딱 한 번
            // 기다린 뒤(코루틴 폴링 없이 고정 대기) 그 시점에 판정을 확인한다. 실패가 그 전에 이미
            // 정해졌어도(예: 홀드 중간에 놓침) Endpoint 전에는 재시도하거나 넘어가지 않는다 - 그래야
            // 화면에서 노트가 갑자기 끊기는 느낌 없이 항상 끝까지 보여진다.
            // 벽(Wall)도 이 원칙은 같다: 부딪힌 순간 판정 자체는 이미 확정이지만(Note.cs가 Miss여도
            // Deactivate는 끝까지 미룸), 벽 오브젝트는 몸을 다 지나칠 때까지 화면에 남아있는다.
            // 그래서 판정 이벤트를 받은 뒤에도 실제로 그 벽 Note가 비활성화될 때까지 한 번 더 기다린다
            // (벽 길이 때문에 소요 시간을 travelTime만으로 못 구해서 고정 시간 대신 오브젝트 상태를 직접 본다).
            // retryUntilSuccess가 꺼져있으면(임시 테스트 모드) 한 번만 스폰하고 결과와 상관없이 넘어간다.
            while (true)
            {
                practiceStartOk = false;
                practiceEndOk = !isHoldStep; // 일반 노트는 끝 판정이 애초에 없으므로 항상 충족
                practiceMissed = false;
                spawner.QueuePracticeNote(
                    step.practiceType, practiceLane, practiceRow, dir,
                    step.holdDuration,
                    isHoldStep ? practiceEndLane : -1,
                    isHoldStep ? practiceRow : -1);

                LongNoteGuide guide = null;
                Note wallNote = null;
                if (isHoldStep)
                {
                    yield return null;
                    yield return null; // NoteSpawner가 잡을 한두 프레임 여유
                    guide = FindObjectOfType<LongNoteGuide>();
                }
                else if (isWallStep)
                {
                    yield return null;
                    yield return null;
                    foreach (var n in FindObjectsOfType<Note>())
                    {
                        if (n.type == NoteType.Wall) { wallNote = n; break; }
                    }
                }

                if (isWallStep)
                {
                    yield return new WaitUntil(() => practiceStartOk || practiceMissed);
                    if (wallNote != null)
                        yield return new WaitUntil(() => wallNote == null || !wallNote.gameObject.activeInHierarchy);
                }
                else
                {
                    yield return new WaitForSeconds(naturalDuration);
                }

                // Endpoint 판정: 세 가지(시작/가이드라인/끝)가 전부 true일 때만 성공.
                bool holdOk = guide == null || !guide.HoldFailed;
                bool success = practiceStartOk && holdOk && practiceEndOk;

                if (success || !retryUntilSuccess) break;

                yield return new WaitForSeconds(0.5f); // 재도전 전 짧게 텀을 둔다
            }

            yield return new WaitForSeconds(resolveBuffer);
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
