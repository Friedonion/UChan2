using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class NoteSpawner : MonoBehaviour
{
    private ChartData activeChart; // 현재 플레이 중인 차트 (GameManager로부터 전달받음)
    public GameObject notePrefab;
    public GameObject noteSpawnEffectPrefab;
    [Tooltip("노트 생성 이펙트의 크기를 조절합니다 (예: 2.0 = 두 배)")]
    public float noteSpawnEffectScale = 1.0f;

    [Header("Note Models")]
    public GameObject slashModel;
    public GameObject fanningModel;
    public GameObject hitModel;
    public GameObject bossModel;
    public GameObject wallModel;
    public GameObject holdOpenModel;
    public GameObject holdFoldedModel;

    [Header("Grid Settings")]
    public float laneWidth = 0.5f;
    public float rowHeight = 0.5f;
    public float spawnDistance = 25.0f;

    [Header("Long Note Settings")]
    public float longNoteBeamThickness = 0.2f;
    [Tooltip("롱노트 유지 중 부채가 가이드라인에서 얼마나 벗어나도 되는지의 여유 반경(m)")]
    public float longNoteHoldRadius = 0.6f;

    // 롱노트는 시작/끝 두 개의 스폰 이벤트로 쪼개져서 시간순으로 스케줄된다.
    private class SpawnJob
    {
        public float time;
        public int lane;
        public int row;
        public NoteType type;
        public Vector3 direction;
        public bool isLongNoteEnd;
        public LongNoteLink link; // 롱노트가 아니면 null
    }

    // 시작 job과 끝 job이 같은 가이드라인을 공유하기 위한 연결 고리
    private class LongNoteLink
    {
        public LongNoteGuide guide;
        public Vector3 endSpawnPos; // 끝 노트의 예정 스폰 위치 (endLane/endRow 기준)
        public float endHitTime;    // 끝 노트의 예정 히트 시각
        public List<LongNoteGuide.MidWaypoint> midWaypoints; // 가이드라인이 순서대로 지나가야 하는 가상 지점들
    }

    private List<SpawnJob> jobs = new List<SpawnJob>();
    private int nextJobIndex = 0;
    private bool isPlaying = false;
    private float startTime;

    // Sync Test 전용: chart.loop가 true면 jobs를 유한 리스트가 아니라 loopPeriod 주기로
    // 무한 반복되는 패턴으로 취급한다. 롱노트(link)는 지원하지 않음 - 단순 Hit 노트 전용.
    private bool loopMode = false;
    private float loopPeriod = 0f;
    private List<SpawnJob> patternJobs;
    private int patternCount = 0;

    public void StartPlaying(ChartData chart, float leadInTime = 4.0f)
    {
        if (isPlaying || chart == null) return;
        activeChart = chart;

        // 오디오 시작이 실제로 미래의 시간 (현재시간 + 노트가 쏟아지는 시간)
        startTime = (float)AudioSettings.dspTime + leadInTime;
        isPlaying = true;

        BuildJobs(chart);

        loopMode = chart.loop && chart.loopPeriod > 0f;
        if (loopMode)
        {
            patternJobs = jobs;
            patternCount = patternJobs.Count;
            loopPeriod = chart.loopPeriod;
        }
    }

    private void BuildJobs(ChartData chart)
    {
        jobs.Clear();
        nextJobIndex = 0;

        foreach (var info in chart.notes)
        {
            Vector3 dir = (info.direction != null && info.direction.Length == 3)
                ? new Vector3(info.direction[0], info.direction[1], info.direction[2])
                : Vector3.zero;
            if (dir == Vector3.zero) dir = Vector3.right;

            SpawnJob startJob = new SpawnJob
            {
                time = info.time,
                lane = info.lane,
                row = info.row,
                type = info.typeEnum,
                direction = dir,
                isLongNoteEnd = false
            };

            if (info.duration > 0f)
            {
                LongNoteLink link = new LongNoteLink();
                startJob.link = link;

                int endLane = info.endLane >= 0 ? info.endLane : info.lane;
                int endRow = info.endRow >= 0 ? info.endRow : info.row;
                float endTime = info.time + info.duration;

                link.endSpawnPos = GetSpawnPos(endLane, endRow);
                link.endHitTime = startTime + endTime;

                // 중간 지점들: duration 구간에 걸쳐 순서대로 균등하게 시각을 배분한다.
                // (예: 2개면 1/3, 2/3 지점). 비어있으면 직선 그대로.
                link.midWaypoints = new List<LongNoteGuide.MidWaypoint>();
                if (info.midpoints != null && info.midpoints.Count > 0)
                {
                    int count = info.midpoints.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var wp = info.midpoints[i];
                        float t = info.time + info.duration * (i + 1) / (count + 1);
                        link.midWaypoints.Add(new LongNoteGuide.MidWaypoint
                        {
                            xy = GetSpawnPos(wp.lane, wp.row),
                            hitTime = startTime + t
                        });
                    }
                }

                SpawnJob endJob = new SpawnJob
                {
                    time = endTime,
                    lane = endLane,
                    row = endRow,
                    type = info.typeEnum,
                    direction = dir,
                    isLongNoteEnd = true,
                    link = link
                };

                jobs.Add(startJob);
                jobs.Add(endJob);
            }
            else
            {
                jobs.Add(startJob);
            }
        }

        jobs.Sort((a, b) => a.time.CompareTo(b.time));
    }

    public bool IsActive => isPlaying;

    // 튜토리얼 등에서 연습용 노트를 하나(또는 롱노트 한 쌍) "지금부터 travelTime 후 도달"하도록 즉시 큐에 넣는다.
    // 이미 진행 중인 jobs 타임라인에 시간이 항상 더 큰 항목만 추가되므로, 재정렬해도 이미 스폰된 앞쪽 항목들은 영향받지 않는다.
    public void QueuePracticeNote(NoteType type, int lane, int row, Vector3 direction,
        float holdDuration = 0f, int endLane = -1, int endRow = -1)
    {
        if (!isPlaying || activeChart == null) return;

        float travelTime = activeChart.travelTime;
        float currentRelative = (float)(GameManager.Instance != null ? GameManager.Instance.EffectiveDspTime : AudioSettings.dspTime) - startTime;
        float noteTime = currentRelative + travelTime;
        // Wall은 Vector3.zero가 "기본 벽 크기 사용"이라는 의미로 쓰이므로 다른 타입만 기본 방향으로 보정한다.
        Vector3 dir = (direction == Vector3.zero && type != NoteType.Wall) ? Vector3.right : direction;

        SpawnJob startJob = new SpawnJob { time = noteTime, lane = lane, row = row, type = type, direction = dir, isLongNoteEnd = false };

        if (holdDuration > 0f)
        {
            LongNoteLink link = new LongNoteLink();
            startJob.link = link;

            int eLane = endLane >= 0 ? endLane : lane;
            int eRow = endRow >= 0 ? endRow : row;
            float endTime = noteTime + holdDuration;

            link.endSpawnPos = GetSpawnPos(eLane, eRow);
            link.endHitTime = startTime + endTime;
            link.midWaypoints = new List<LongNoteGuide.MidWaypoint>();

            SpawnJob endJob = new SpawnJob { time = endTime, lane = eLane, row = eRow, type = type, direction = dir, isLongNoteEnd = true, link = link };

            jobs.Add(startJob);
            jobs.Add(endJob);
        }
        else
        {
            jobs.Add(startJob);
        }

        jobs.Sort((a, b) => a.time.CompareTo(b.time));
    }

    public void StopPlaying()
    {
        isPlaying = false;
        activeChart = null;
        jobs.Clear();
        nextJobIndex = 0;
        loopMode = false;
        patternJobs = null;
        patternCount = 0;
        StopAllCoroutines();

        // 화면에 남아있는 모든 노트 비활성화 (풀로 반환)
        Note[] activeNotes = FindObjectsOfType<Note>();
        foreach (Note note in activeNotes)
        {
            if (note.gameObject.activeSelf) note.Deactivate();
        }

        // 남아있는 롱노트 가이드라인도 정리
        LongNoteGuide[] activeGuides = FindObjectsOfType<LongNoteGuide>();
        foreach (var guide in activeGuides)
        {
            Destroy(guide.gameObject);
        }
    }

    void Update()
    {
        if (!isPlaying || activeChart == null) return;

        float currentTime = (float)(GameManager.Instance != null ? GameManager.Instance.EffectiveDspTime : AudioSettings.dspTime) - startTime;

        if (loopMode)
        {
            if (patternCount == 0) return;

            while (true)
            {
                int patternIdx = nextJobIndex % patternCount;
                int loopsSoFar = nextJobIndex / patternCount;
                float jobTime = patternJobs[patternIdx].time + loopsSoFar * loopPeriod;
                if (jobTime - activeChart.travelTime > currentTime) break;

                SpawnJob template = patternJobs[patternIdx];
                SpawnFromJob(new SpawnJob
                {
                    time = jobTime,
                    lane = template.lane,
                    row = template.row,
                    type = template.type,
                    direction = template.direction,
                    isLongNoteEnd = false,
                    link = null
                });
                nextJobIndex++;
            }
            return;
        }

        if (jobs == null || nextJobIndex >= jobs.Count) return;

        while (nextJobIndex < jobs.Count &&
               jobs[nextJobIndex].time - activeChart.travelTime <= currentTime)
        {
            SpawnFromJob(jobs[nextJobIndex]);
            nextJobIndex++;
        }
    }

    void SpawnFromJob(SpawnJob job)
    {
        // 홀드 유지에 실패한 롱노트의 끝 노트는 스폰하지 않는다 - Miss는 실패한 순간 이미 보고됐다.
        if (job.isLongNoteEnd && job.link != null && job.link.guide != null && job.link.guide.HoldFailed)
        {
            Destroy(job.link.guide.gameObject);
            return;
        }

        Vector3 spawnPos = GetSpawnPos(job.lane, job.row);
        float hitTime = startTime + job.time;

        Note note = SpawnSingleNote(job.type, spawnPos, job.direction, hitTime);

        if (job.link != null)
        {
            if (!job.isLongNoteEnd)
            {
                // 롱노트 시작 노트 - 가이드라인을 새로 만들고, 끝 노트가 아직 스폰되기 전이므로
                // 끝 노트의 예정 위치/시간 정보를 넘겨서 가상으로 앞쪽 궤적을 미리 그리게 한다.
                bool requiredOpen = job.type == NoteType.HoldOpen;
                job.link.guide = LongNoteGuide.Create(
                    note.transform,
                    job.link.endSpawnPos,
                    job.link.endHitTime,
                    activeChart.travelTime,
                    spawnDistance,
                    longNoteBeamThickness,
                    requiredOpen,
                    longNoteHoldRadius);
                job.link.guide.SetMidPoints(job.link.midWaypoints);
                note.holdGuide = job.link.guide;
            }
            else if (job.link.guide != null)
            {
                // 롱노트 끝 노트가 실제로 스폰됨 - 가이드라인이 가상 계산 대신 실제 transform을 따라가게 전환
                job.link.guide.AttachEnd(note.transform);
            }
        }
    }

    Note SpawnSingleNote(NoteType type, Vector3 spawnPos, Vector3 dir, float hitTime)
    {
        // 노트가 나타날 때 이펙트 발생
        if (noteSpawnEffectPrefab != null)
        {
            GameObject effectObj = Instantiate(noteSpawnEffectPrefab, spawnPos, Quaternion.identity);
            effectObj.transform.localScale = Vector3.one * noteSpawnEffectScale; // 이펙트 크기 조절 적용
            NoteSpawnEffect effectScript = effectObj.GetComponent<NoteSpawnEffect>();
            if (effectScript != null)
            {
                effectScript.Play(type);
            }
        }

        GameObject noteObj;
        if (NotePoolManager.Instance != null)
        {
            noteObj = NotePoolManager.Instance.GetNote(type, spawnPos, Quaternion.identity);
        }
        else
        {
            noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
            noteObj.name = $"Note_{type}_DynamicFallback";
        }

        Note noteScript = noteObj.GetComponent<Note>();
        AssignModels(noteScript, type);

        noteScript.Initialize(type, dir, hitTime, activeChart.travelTime, spawnDistance);
        return noteScript;
    }

    Vector3 GetSpawnPos(float lane, float row)
    {
        float x = (lane - 1.5f) * laneWidth;
        float y = (row - 0.5f) * rowHeight + 1.0f;
        return new Vector3(x, y, spawnDistance);
    }

    void AssignModels(Note note, NoteType type)
    {
        // 이미 생성된 비주얼 모델이 있는지 확인 (풀링시 중복 생성 방지)
        string modelName = "Visual_" + type.ToString();
        Transform existingModel = note.transform.Find(modelName);

        if (existingModel != null) return; // 이미 있으면 통과

        GameObject visualModel = null;
        switch (type)
        {
            case NoteType.Slashing: visualModel = slashModel; break;
            case NoteType.Fanning: visualModel = fanningModel; break;
            case NoteType.Hit: visualModel = hitModel; break;
            case NoteType.Boss: visualModel = bossModel; break;
            case NoteType.Wall: visualModel = wallModel; break;
            case NoteType.HoldFolded: visualModel = holdFoldedModel; break;
            case NoteType.HoldOpen: visualModel = holdOpenModel; break;
        }

        if (visualModel != null)
        {
            GameObject obj = Instantiate(visualModel, note.transform);
            obj.name = modelName;

            // Fanning 노트 비주얼은 항상 펼쳐진 상태여야 하므로 Animator를 비활성화
            // (HoldOpen/HoldFolded는 StaticFanPose 컴포넌트가 자체적으로 포즈를 고정하므로 여기서 다루지 않는다)
            if (type == NoteType.Fanning)
            {
                Animator anim = obj.GetComponentInChildren<Animator>();
                if (anim != null) anim.speed = 100f;
            }

            // Note 스크립트에 해당 필드를 연결
            if (type == NoteType.Slashing) note.slashIndicator = obj;
            else if (type == NoteType.Fanning) note.fanIndicator = obj;
            else if (type == NoteType.Hit) note.hitIndicator = obj;
            else if (type == NoteType.Boss) note.bossIndicator = obj;
            else if (type == NoteType.Wall) note.wallIndicator = obj;
            else if (type == NoteType.HoldFolded) note.hitIndicator = obj;
            else if (type == NoteType.HoldOpen) note.fanIndicator = obj;
        }
    }
}
