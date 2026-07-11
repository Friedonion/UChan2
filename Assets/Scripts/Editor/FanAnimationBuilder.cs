using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class FanAnimationBuilder
{
    const string ClipPath     = "Assets/Models/Fan/FanAnim.anim";
    const float  OpenTime     = 0f;
    const float  CloseTime    = 0.5f;
    const float  ClosedY      = 70f;   // Group_014 기준 (0~140° 중앙)
    const string PivotPrefix  = "FanPivot_";

    // ── 1단계: 작은살 → Group 자식으로 설정 ────────────────────────────────
    [MenuItem("Tools/Fan Step 1 - Setup Parenting")]
    static void SetupParenting()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("[Fan] 루트를 선택하세요."); return; }

        var groups    = new List<Transform>();
        var smallRibs = new List<Transform>();

        foreach (Transform child in root.transform)
        {
            if (child.name.StartsWith("Group_"))      groups.Add(child);
            if (child.name.StartsWith("부채 작은살")) smallRibs.Add(child);
        }

        groups.Sort((a, b)    => NormalizeAngle(a.localEulerAngles.y)
                                .CompareTo(NormalizeAngle(b.localEulerAngles.y)));
        smallRibs.Sort((a, b) => NormalizeAngle(a.localEulerAngles.y)
                                .CompareTo(NormalizeAngle(b.localEulerAngles.y)));

        int pairs = Mathf.Min(groups.Count, smallRibs.Count);
        for (int i = 0; i < pairs; i++)
        {
            smallRibs[i].SetParent(groups[i], worldPositionStays: true);
            Debug.Log($"[Fan] {smallRibs[i].name} → {groups[i].name}");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Fan] Step 1 완료: {pairs}쌍 매칭. Step 2를 실행하세요.");
    }

    // ── 2단계: Component#2_003 기준 FanPivot 래퍼 생성 ─────────────────────
    [MenuItem("Tools/Fan Step 2 - Setup Pivots")]
    static void SetupPivots()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("[Fan] 루트를 선택하세요."); return; }

        // 나사(스크류) 위치 = 모든 살의 회전 중심
        Transform screw = null;
        foreach (Transform child in root.transform)
            if (child.name == "Component#2_003") { screw = child; break; }
        if (screw == null) { Debug.LogError("[Fan] Component#2_003을 찾을 수 없습니다."); return; }
        Vector3 pivotPos = screw.position;

        int count = 0;

        // Groups (작은살 포함)
        var toWrap = new List<Transform>();
        foreach (Transform child in root.transform)
        {
            if (child.name.StartsWith("Group_") || child.name.StartsWith("부채큰살"))
                toWrap.Add(child);
        }

        foreach (var rib in toWrap)
        {
            // 이미 FanPivot 자식이면 건너뜀
            if (rib.parent.name.StartsWith(PivotPrefix)) continue;

            // FanPivot을 나사 위치에 생성, 회전은 살과 동일하게
            var pivotGO = new GameObject(PivotPrefix + rib.name);
            Undo.RegisterCreatedObjectUndo(pivotGO, "Create FanPivot");
            pivotGO.transform.SetParent(root.transform, false);
            pivotGO.transform.position = pivotPos;
            pivotGO.transform.rotation = rib.rotation; // 살과 같은 방향

            // 살을 FanPivot 자식으로 이동 (월드 위치 유지)
            rib.SetParent(pivotGO.transform, worldPositionStays: true);
            count++;
            Debug.Log($"[Fan] Pivot 생성: {pivotGO.name}  Y={NormalizeAngle(pivotGO.transform.localEulerAngles.y):F1}°");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Fan] Step 2 완료: {count}개 FanPivot 생성. Step 3을 실행하세요.");
    }

    // ── 3단계: FanPivot 회전 애니메이션 생성 ────────────────────────────────
    [MenuItem("Tools/Fan Step 3 - Build Animation")]
    static void Build()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("[Fan] 루트를 선택하세요."); return; }

        // 씬 데이터 먼저 수집
        var pivotData = new List<(string name, float openY)>();
        foreach (Transform child in root.transform)
        {
            if (!child.name.StartsWith(PivotPrefix)) continue;
            pivotData.Add((child.name, NormalizeAngle(child.localEulerAngles.y)));
        }

        if (pivotData.Count == 0)
        {
            Debug.LogError("[Fan] FanPivot_ 오브젝트가 없습니다. Step 2를 먼저 실행하세요.");
            return;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { Debug.LogError($"[Fan] {ClipPath} 없음"); return; }

        // 기존 커브 전체 제거
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, b, null);

        // FanPivot별 커브 추가
        // - Group 계열: 70°로 수렴 (자식 작은살이 world Y=0°에 도달)
        // - 큰살 계열: 0°로 수렴 (작은살과 동일한 world Y에 도달)
        foreach (var (name, openY) in pivotData)
        {
            bool isLargeRib = name.Contains("큰살");
            float closeY = isLargeRib ? 0f : ClosedY;
            var curve = AnimationCurve.EaseInOut(OpenTime, openY, CloseTime, closeY);
            clip.SetCurve(name, typeof(Transform), "localEulerAngles.y", curve);
            Debug.Log($"[Fan] {name}  {openY:F1}° → {closeY:F1}°");
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Fan] Step 3 완료: {pivotData.Count}개 FanPivot 애니메이션 저장.");
    }

    // ── 큰살 Pivot X 보정 (Step 2 이미 실행한 경우 사용) ─────────────────────
    [MenuItem("Tools/Fan Fix - Repair Large Rib Pivots")]
    static void FixLargeRibPivots()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("[Fan] 루트를 선택하세요."); return; }

        var pivots = new List<Transform>();
        foreach (Transform child in root.transform)
            if (child.name.StartsWith(PivotPrefix) && child.name.Contains("큰살"))
                pivots.Add(child);

        if (pivots.Count == 0) { Debug.LogError("[Fan] FanPivot_큰살 오브젝트가 없습니다."); return; }

        foreach (var pivot in pivots)
        {
            float worldY = pivot.eulerAngles.y;

            // 자식들을 루트로 임시 이동 (월드 위치/회전 유지)
            var children = new List<Transform>();
            foreach (Transform sub in pivot) children.Add(sub);
            foreach (var sub in children)
                sub.SetParent(root.transform, worldPositionStays: true);

            // Pivot X를 0으로 보정
            Undo.RecordObject(pivot, "Fix Large Rib Pivot");
            pivot.eulerAngles = new Vector3(0f, worldY, 0f);

            // 자식들 다시 Pivot 아래로 복귀
            foreach (var sub in children)
                sub.SetParent(pivot, worldPositionStays: true);

            Debug.Log($"[Fan] Fixed: {pivot.name}  (0, {worldY:F1}, 0)");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Fan] 큰살 Pivot 보정 완료. Step 3을 다시 실행하세요.");
    }

    // ── 디버그: 큰살 전체 회전값 확인 ──────────────────────────────────────────
    [MenuItem("Tools/Log Large Rib Rotation")]
    static void LogLargeRibRotation()
    {
        var root = Selection.activeGameObject;
        if (root == null) { Debug.LogError("[Fan] 루트를 선택하세요."); return; }
        foreach (Transform child in root.transform)
        {
            if (!child.name.StartsWith("FanPivot_")) continue;
            if (!child.name.Contains("큰살")) continue;
            Vector3 e = child.eulerAngles;
            Debug.Log($"  {child.name}  worldEuler=({e.x:F1}, {e.y:F1}, {e.z:F1})");
            foreach (Transform sub in child)
            {
                Vector3 se = sub.eulerAngles;
                Debug.Log($"    └ {sub.name}  worldEuler=({se.x:F1}, {se.y:F1}, {se.z:F1})");
            }
        }
    }

    static float NormalizeAngle(float angle)
    {
        while (angle > 180f)  angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
