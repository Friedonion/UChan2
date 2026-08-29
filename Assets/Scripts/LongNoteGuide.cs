using UnityEngine;
using System.Collections.Generic;

// 롱노트의 시작 노트와 끝 노트를 잇는 파란색 가이드라인(빔).
// 중간에 순서대로 지나가야 하는 가상 지점(MidWaypoint)들을 넣으면 여러 개의 빔 조각으로
// 꺾인 경로를 그릴 수 있다. 이 지점들은 실제 노트로 스폰되지 않는다 - 순수하게 가이드라인
// 모양을 잡기 위한 값이다.
//
// 근점 → 중간 지점들 → 원점을 Catmull-Rom 스플라인으로 잇고, 그 곡선을 잘게 쪼갠 사각 단면
// "관(tube)" 하나의 연속된 메시로 직접 그린다. 각 단면(ring)은 앞/뒤 조각이 정점을 공유하므로
// 개별 Cube를 이어붙일 때 생기는 겹친 끝면·이음매가 애초에 존재하지 않는다.
public class LongNoteGuide : MonoBehaviour
{
    // 가이드라인이 지나가야 하는 가상 중간 지점 하나. 실제로 스폰되는 노트가 없으므로
    // Note.Update()와 동일한 수식으로 매 프레임 가상 위치를 계산한다.
    public struct MidWaypoint
    {
        public Vector3 xy;      // 이 지점의 (x, y) - z는 시간에 따라 매 프레임 계산됨
        public float hitTime;   // 이 지점을 지나야 하는 시각
    }

    private const int SubdivisionsPerSection = 8; // 중간 지점 구간 하나를 잘게 쪼개는 개수

    private Transform startNoteTransform;
    private Transform endNoteTransform; // 끝 노트가 아직 스폰되지 않았으면 null

    // 끝 노트가 스폰되기 전, 같은 위치에 미리 나타날 것처럼 가상 궤적을 계산하기 위한 정보
    private Vector3 pendingEndXY;
    private float endHitTime;
    private float travelTime;
    private float spawnZ;

    private List<MidWaypoint> midWaypoints = new List<MidWaypoint>();
    private List<Vector3> pathPoints = new List<Vector3>(); // 근점 → 중간 지점들 → 원점
    private List<Vector3> samples = new List<Vector3>();    // pathPoints를 스플라인으로 잘게 쪼갠 단면 중심점들

    private float beamThickness = 0.12f;
    private static Material sharedMaterialFolded;
    private static Material sharedMaterialOpen;

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Vector3[] vertices;
    private int ringCount;

    // 🌟 [성능 최적화] 매 프레임 FindObjectsOfType 호출을 막기 위한 정적 캐싱 배열 (양손 부채 2개)
    private static FanSystem[] cachedFans;

    // 시작 노트가 사라진 뒤에는 판정선(z=0)에 앞쪽 끝을 고정시키기 위한 상태
    private bool startConsumed = false;
    private Vector3 lastKnownStartPos;

    private GameObject targetOrb;

    // 홀드 유지 검사 - 시작 노트가 성공적으로 타격된 뒤(BeginHold)부터 매 프레임 부채 상태/위치를 검사한다.
    private bool requiredOpen;
    private float holdRadius;
    private bool holdActive = false;
    public bool HoldFailed { get; private set; }

    public static LongNoteGuide Create(Transform start, Vector3 endSpawnPos, float endHitTime, float travelTime, float spawnZ, float beamThickness, bool requiredOpen, float holdRadius)
    {
        GameObject go = new GameObject("LongNoteGuide");
        LongNoteGuide guide = go.AddComponent<LongNoteGuide>();
        guide.Init(start, endSpawnPos, endHitTime, travelTime, spawnZ, beamThickness, requiredOpen, holdRadius);
        return guide;
    }

    private void Init(Transform start, Vector3 endSpawnPos, float endHitTime, float travelTime, float spawnZ, float beamThickness, bool requiredOpen, float holdRadius)
    {
        startNoteTransform = start;
        pendingEndXY = endSpawnPos;
        this.endHitTime = endHitTime;
        this.travelTime = travelTime;
        this.spawnZ = spawnZ;
        this.beamThickness = beamThickness;
        this.requiredOpen = requiredOpen;
        this.holdRadius = holdRadius;
        lastKnownStartPos = start.position;

        mesh = new Mesh();
        mesh.name = "LongNoteGuideMesh";
        mesh.MarkDynamic();

        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetSharedMaterial(this.requiredOpen);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // 타겟 위치(Z=0)를 보여줄 시각적 가이드 구슬(Orb) 생성
        targetOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetOrb.name = "TargetOrb";
        targetOrb.transform.SetParent(this.transform);
        targetOrb.transform.localScale = Vector3.one * (beamThickness * 0.8f); // 기존 1.5f에서 0.8f로 축소
        Destroy(targetOrb.GetComponent<Collider>()); // 충돌체 제거
        MeshRenderer orbRenderer = targetOrb.GetComponent<MeshRenderer>();
        orbRenderer.sharedMaterial = meshRenderer.sharedMaterial;
        orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        orbRenderer.receiveShadows = false;
    }

    // 시작 노트가 성공적으로 타격되면 Note.cs가 호출한다 - 이 순간부터 홀드 유지 검사가 시작된다.
    public void BeginHold()
    {
        holdActive = true;
    }

    // 실패 시 빔을 회색조 반투명으로 변경하고, 타겟 구슬은 아예 숨긴다.
    private void SetVisualStateFailed()
    {
        Color failedColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        if (meshRenderer != null) 
        {
            meshRenderer.material.color = failedColor;
        }
        if (targetOrb != null) 
        {
            targetOrb.SetActive(false);
        }
    }

    // 시작 노트를 놓쳤을 경우 호출되어 롱노트 자체를 즉시 실패 처리한다.
    public void ForceFail()
    {
        HoldFailed = true;
        holdActive = false;
        SetVisualStateFailed();
    }

    public void AttachEnd(Transform end)
    {
        endNoteTransform = end;
    }

    // 가이드가 순서대로 지나가야 하는 중간 지점들을 지정한다. 비어있거나 null이면 직선(구간 1개).
    public void SetMidPoints(List<MidWaypoint> waypoints)
    {
        midWaypoints = waypoints ?? new List<MidWaypoint>();
        RebuildTopology();
    }

    // 중간 지점 개수로 정해지는 단면(ring) 개수에 맞춰 정점/삼각형 배열을 새로 만든다.
    // 위치는 매 프레임 바뀌지만 위상(몇 번째 정점이 몇 번째 삼각형을 이루는지)은 고정이다.
    private void RebuildTopology()
    {
        int sectionCount = midWaypoints.Count + 1;
        ringCount = midWaypoints.Count == 0 ? 2 : sectionCount * SubdivisionsPerSection + 1;

        vertices = new Vector3[ringCount * 4];

        int quadCount = (ringCount - 1) * 4;
        int[] triangles = new int[quadCount * 6];
        int ti = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            int baseCur = i * 4;
            int baseNext = (i + 1) * 4;
            for (int k = 0; k < 4; k++)
            {
                int a = baseCur + k;
                int b = baseCur + (k + 1) % 4;
                int c = baseNext + (k + 1) % 4;
                int d = baseNext + k;

                triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    private static Material GetSharedMaterial(bool isRequiredOpen)
    {
        Material targetMat = isRequiredOpen ? sharedMaterialOpen : sharedMaterialFolded;
        if (targetMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            targetMat = new Material(shader);

            if (targetMat.HasProperty("_Surface"))
            {
                targetMat.SetFloat("_Surface", 1f); // Transparent
                targetMat.SetFloat("_ZWrite", 0f);
                targetMat.SetFloat("_SrcBlend", 5f);  // SrcAlpha
                targetMat.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
                targetMat.renderQueue = 3000;
                targetMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            // 관 형태라 삼각형 감기 순서에 따라 안쪽/바깥쪽 중 한쪽만 보일 수 있으므로 양면 렌더링.
            if (targetMat.HasProperty("_Cull")) targetMat.SetFloat("_Cull", 0f); // Off

            // 빔 색상: 펼치기(requiredOpen)일 때는 마젠타, 접기일 때는 시안
            Color color = isRequiredOpen ? new Color(1f, 0f, 1f, 0.2f) : new Color(0f, 1f, 1f, 0.2f);
            if (targetMat.HasProperty("_BaseColor")) targetMat.SetColor("_BaseColor", color);
            else targetMat.color = color;

            if (isRequiredOpen) sharedMaterialOpen = targetMat;
            else sharedMaterialFolded = targetMat;
        }
        return targetMat;
    }

    void LateUpdate()
    {
        if (startNoteTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        // 끝 노트가 이미 스폰됐고 히트/미스로 사라졌다면 가이드라인도 함께 소멸
        if (endNoteTransform != null && !endNoteTransform.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 nearPos = GetNearPos();
        Vector3 farPos = GetFarPos();

        BuildSamples(nearPos, farPos);
        BuildRingVertices();

        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        Vector3 target = GetHoldTargetPosition();
        
        // 홀드 유지 중일 때는 CheckHold에서 구슬 위치를 빔에 밀착(Snap)시키고, 아닐 때는 기본 위치(target)로.
        if (holdActive) 
        {
            CheckHold(target);
        }
        else if (targetOrb != null)
        {
            targetOrb.transform.position = target;
        }
    }

    // 롱노트 유예 시간 타이머
    private float outOfRadiusTimer = 0f;
    private const float GRACE_PERIOD = 0.15f; // 0.15초의 유예 시간

    // 부채가 요구된 접힘 상태를 유지하면서, 지금 판정선에 도달한 가이드 지점 근처(holdRadius 이내)에
    // 있는지 검사한다. 만족하는 부채가 하나도 없으면 즉시 Miss 처리하고 가이드를 숨긴다.
    private void CheckHold(Vector3 defaultTarget)
    {
        bool isHolding = false;
        FanSystem holdingFan = null;
        Vector3 bestOrbPos = defaultTarget;

        // 🌟 [성능 최적화] 씬에 있는 부채(2개)를 한 번만 찾아서 캐싱해두고 재사용합니다.
        // 씬 재로드 시 배열은 남아있지만 안의 객체가 null이 되므로 반드시 [0] == null 체크를 포함해야 합니다!
        if (cachedFans == null || cachedFans.Length == 0 || cachedFans[0] == null)
        {
            cachedFans = FindObjectsOfType<FanSystem>();
        }

        foreach (var fan in cachedFans)
        {
            if (fan == null || !fan.gameObject.activeInHierarchy) continue;

            bool foldOk = requiredOpen ? fan.IsOpened : !fan.IsOpened;
            if (!foldOk) continue;

            fan.GetFanSegment(out Vector3 segStart, out Vector3 segEnd);
            Vector3 fanCenter = (segStart + segEnd) * 0.5f;

            // 내 부채 중심과 빔(가이드라인) 전체 궤적 사이의 가장 가까운 점을 찾음 (3D 리본 방식)
            Vector3 closestPointOnBeam = FindClosestPointOnSpline(fanCenter);

            if (DistancePointToSegment(closestPointOnBeam, segStart, segEnd) <= holdRadius) 
            {
                isHolding = true;
                holdingFan = fan;
                bestOrbPos = closestPointOnBeam; // 닿아있는 실제 위치로 구슬 이동 예약
                break;
            }
        }

        // 구슬(Orb)의 시각적 위치 업데이트 - 닿았으면 부채를 따라가고, 아니면 원래 판정선에 머문다.
        if (targetOrb != null)
        {
            targetOrb.transform.position = bestOrbPos;
        }

        if (isHolding)
        {
            outOfRadiusTimer = 0f; // 다시 범위 내로 들어오면 유예 시간 초기화
            if (holdingFan != null) holdingFan.TriggerHaptic(0.5f, 0.05f); // 닿아있는 동안 징~ 진동
            if (targetOrb != null) targetOrb.transform.localScale = Vector3.one * (beamThickness * 1.2f); // 기존 2.0f에서 1.2f로 축소 (잘 잡고 있을 때)
        }
        else
        {
            outOfRadiusTimer += Time.deltaTime;
            if (targetOrb != null) targetOrb.transform.localScale = Vector3.one * (beamThickness * 0.5f); // 기존 1.0f에서 0.5f로 축소 (놓쳤을 때)

            if (outOfRadiusTimer > GRACE_PERIOD)
            {
                // 유예 시간 내에 복구하지 못함 - 최종 실패 처리
                HoldFailed = true;
                holdActive = false;
                SetVisualStateFailed();
                if (GameManager.Instance != null) GameManager.Instance.NoteMissed();
            }
        }
    }

    // 부채 중심 좌표(point)와 샘플들(samples, 빔을 이루는 미세 선분들) 중 가장 가까운 점을 찾는다.
    private Vector3 FindClosestPointOnSpline(Vector3 point)
    {
        if (samples.Count == 0) return point;
        
        Vector3 closest = samples[0];
        float minSqDist = (point - closest).sqrMagnitude;
        
        for (int i = 0; i < samples.Count - 1; i++)
        {
            Vector3 a = samples[i];
            Vector3 b = samples[i+1];
            
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            Vector3 closestOnSeg = a;
            
            if (lenSq > 0.0001f)
            {
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSq);
                closestOnSeg = a + t * ab;
            }
            
            float sqDist = (point - closestOnSeg).sqrMagnitude;
            if (sqDist < minSqDist)
            {
                minSqDist = sqDist;
                closest = closestOnSeg;
            }
        }
        return closest;
    }

    // samples는 근점→원점 순서이며 이미 판정선(z<=0 근방)에 도달한 지점들이 앞쪽에 몰려있다.
    // 그 중 가장 마지막(=가장 최근에 도달한) 지점을 지금 순간의 "여기 있어야 할 위치"로 삼는다.
    private Vector3 GetHoldTargetPosition()
    {
        const float epsilon = 0.05f;
        Vector3 target = samples[0];
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].z <= epsilon) target = samples[i];
            else break;
        }
        return target;
    }

    private static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = p - a;
        float abLenSq = ab.sqrMagnitude;
        float t = abLenSq > 0.0001f ? Vector3.Dot(ap, ab) / abLenSq : 0f;
        t = Mathf.Clamp01(t);
        Vector3 closest = a + t * ab;
        return Vector3.Distance(p, closest);
    }

    // 근점 → 중간 지점들 → 원점을 스플라인으로 잘게 쪼갠 단면 중심점 목록을 만든다.
    private void BuildSamples(Vector3 nearPos, Vector3 farPos)
    {
        samples.Clear();

        if (midWaypoints.Count == 0)
        {
            samples.Add(nearPos);
            samples.Add(farPos);
            return;
        }

        pathPoints.Clear();
        pathPoints.Add(nearPos);
        for (int i = 0; i < midWaypoints.Count; i++)
            pathPoints.Add(GetMidPos(midWaypoints[i]));
        pathPoints.Add(farPos);

        int sectionCount = pathPoints.Count - 1;
        samples.Add(pathPoints[0]);

        for (int i = 0; i < sectionCount; i++)
        {
            Vector3 p0 = pathPoints[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pathPoints[i];
            Vector3 p2 = pathPoints[i + 1];
            Vector3 p3 = pathPoints[Mathf.Min(i + 2, pathPoints.Count - 1)];

            for (int s = 1; s <= SubdivisionsPerSection; s++)
            {
                float t = (float)s / SubdivisionsPerSection;
                samples.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
    }

    private void BuildRingVertices()
    {
        float hw = beamThickness * 0.5f;

        // 1. Z=0 평면을 통과하는 정확한 교차점(Clip Point) 찾기
        Vector3 clipPoint = Vector3.zero;
        Vector3 clipTangent = Vector3.forward;
        bool foundClip = false;

        for (int i = 0; i < ringCount - 1; i++)
        {
            if (samples[i].z <= 0f && samples[i + 1].z > 0f)
            {
                float t = (0f - samples[i].z) / (samples[i + 1].z - samples[i].z);
                clipPoint = Vector3.Lerp(samples[i], samples[i + 1], t);
                clipTangent = (samples[i + 1] - samples[i]).normalized;
                foundClip = true;
                break;
            }
        }

        // 2. 단면(Ring) 렌더링
        for (int i = 0; i < ringCount; i++)
        {
            Vector3 p = samples[i];
            
            Vector3 tangent;
            if (i == 0) tangent = samples[1] - samples[0];
            else if (i == ringCount - 1) tangent = samples[i] - samples[i - 1];
            else tangent = samples[i + 1] - samples[i - 1];

            int b = i * 4;

            if (p.z > 0f)
            {
                // 정상 범위 (판정선 이전): 원래 곡선 위치대로 렌더링
                Quaternion rot = tangent.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(tangent) : Quaternion.identity;
                Vector3 right = rot * Vector3.right;
                Vector3 up = rot * Vector3.up;

                vertices[b + 0] = p + right * hw + up * hw;
                vertices[b + 1] = p - right * hw + up * hw;
                vertices[b + 2] = p - right * hw - up * hw;
                vertices[b + 3] = p + right * hw - up * hw;
            }
            else
            {
                // Z <= 0 영역 진입 (판정선 뒤로 넘어간 과거의 점들)
                if (foundClip)
                {
                    // 모든 과거의 링들을 유일한 교차점(clipPoint) 위치 하나로 몰아넣어서 포갭니다.
                    // 이렇게 하면 링 간의 거리가 0이 되어 메시(면적)가 완전히 투명하게 찌그러지며, 
                    // 마지막 링만이 Z > 0 링과 정상적인 절단면으로 이어지게 됩니다.
                    Quaternion rot = clipTangent.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(clipTangent) : Quaternion.identity;
                    Vector3 right = rot * Vector3.right;
                    Vector3 up = rot * Vector3.up;

                    vertices[b + 0] = clipPoint + right * hw + up * hw;
                    vertices[b + 1] = clipPoint - right * hw + up * hw;
                    vertices[b + 2] = clipPoint - right * hw - up * hw;
                    vertices[b + 3] = clipPoint + right * hw - up * hw;
                }
                else
                {
                    // 모든 점이 Z <= 0 인 경우 (롱노트 전체가 내 등 뒤로 지나가버림)
                    vertices[b + 0] = Vector3.zero;
                    vertices[b + 1] = Vector3.zero;
                    vertices[b + 2] = Vector3.zero;
                    vertices[b + 3] = Vector3.zero;
                }
            }
        }
    }

    // 표준 uniform Catmull-Rom 스플라인 - p1과 p2 사이를 p0, p3(양옆 이웃 점)로 접선을 잡아 보간한다.
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private Vector3 GetNearPos()
    {
        // 한 번 소비(히트/미스)되면 이후로는 시작 노트의 activeInHierarchy를 다시 보지 않는다.
        // 노트 오브젝트가 풀링으로 재사용되어 다른 노트가 되어버릴 수 있기 때문.
        if (!startConsumed)
        {
            if (startNoteTransform.gameObject.activeInHierarchy)
            {
                lastKnownStartPos = startNoteTransform.position;
            }
            else
            {
                startConsumed = true;
            }
        }

        if (startConsumed)
        {
            // 판정선의 역할: 시작 노트가 사라진 뒤에는 앞쪽 끝이 판정선(z=0)에 고정된다.
            return new Vector3(lastKnownStartPos.x, lastKnownStartPos.y, 0f);
        }

        return lastKnownStartPos;
    }

    private Vector3 GetFarPos()
    {
        if (endNoteTransform != null)
        {
            return endNoteTransform.position;
        }

        // 끝 노트가 아직 스폰되기 전 - Note.Update()와 동일한 수식으로 가상 위치를 계산해서
        // 노트가 실제로 스폰되는 순간 값이 매끄럽게 이어지도록 한다.
        double now = GameManager.Instance != null ? GameManager.Instance.EffectiveDspTime : AudioSettings.dspTime;
        float spawnTime = endHitTime - travelTime;
        float progress = (float)((now - spawnTime) / travelTime);
        float z = Mathf.LerpUnclamped(spawnZ, 0f, progress);

        return new Vector3(pendingEndXY.x, pendingEndXY.y, z);
    }

    // 중간 지점은 실제로 스폰되는 노트가 없으므로 가상 궤적을 항상 계산한다.
    private Vector3 GetMidPos(MidWaypoint wp)
    {
        double now = GameManager.Instance != null ? GameManager.Instance.EffectiveDspTime : AudioSettings.dspTime;
        float spawnTime = wp.hitTime - travelTime;
        float progress = (float)((now - spawnTime) / travelTime);
        float z = Mathf.LerpUnclamped(spawnZ, 0f, progress); // 클램핑 제거 (음수 허용)

        return new Vector3(wp.xy.x, wp.xy.y, z);
    }
}
