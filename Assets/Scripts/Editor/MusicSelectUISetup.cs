using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class MusicSelectUISetup
{
    const string XROriginPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    const string RockChartPath          = "Assets/Data/Charts/Rock.asset";
    const string AutoChartPath          = "Assets/Data/Charts/auto_generated_chart.asset";

    [MenuItem("Tools/Setup MusicSelectUI Scene")]
    static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "MusicSelectUI")
        {
            EditorUtility.DisplayDialog("알림", "MusicSelectUI 씬을 열고 실행해주세요.", "OK");
            return;
        }

        SetupEventSystem();
        AddXROrigin();
        var canvas = CreateWorldSpaceCanvas();
        CreateUIHierarchy(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MusicSelectUISetup] 완료! SongSelectManager Inspector에서 Charts와 CardPrefab을 연결해주세요.");
    }

    [MenuItem("Tools/Fix SongCard Prefab Layout")]
    static void FixSongCardPrefab()
    {
        const string prefabPath = "Assets/Prefabs/UI/SongCard.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("알림", "SongCard 프리팹을 찾지 못했습니다: " + prefabPath, "OK");
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot.GetComponent<RectTransform>();

            // 카드 전체 크기 확대
            root.sizeDelta = new Vector2(280, 350);

            // Background: 카드 전체를 채움
            var bg = scope.prefabContentsRoot.transform.Find("Background")?.GetComponent<RectTransform>();
            if (bg != null)
            {
                bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
                bg.offsetMin = bg.offsetMax = Vector2.zero;
            }

            // CoverImage: 상단 72% 차지
            var cover = scope.prefabContentsRoot.transform.Find("CoverImage")?.GetComponent<RectTransform>();
            if (cover != null)
            {
                cover.anchorMin = new Vector2(0f, 0.28f); cover.anchorMax = Vector2.one;
                cover.offsetMin = cover.offsetMax = Vector2.zero;
                cover.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
            }

            // TitleTxt: 중간 영역
            var title = scope.prefabContentsRoot.transform.Find("TitleTxt")?.GetComponent<RectTransform>();
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 0.12f); title.anchorMax = new Vector2(1f, 0.28f);
                title.offsetMin = title.offsetMax = Vector2.zero;
            }

            // BpmText: 하단 영역
            var bpm = scope.prefabContentsRoot.transform.Find("BpmText")?.GetComponent<RectTransform>();
            if (bpm != null)
            {
                bpm.anchorMin = Vector2.zero; bpm.anchorMax = new Vector2(1f, 0.12f);
                bpm.offsetMin = bpm.offsetMax = Vector2.zero;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FixSongCardPrefab] 카드 레이아웃 수정 완료 (280x350, 이미지 상단/텍스트 하단)");
    }

    [MenuItem("Tools/Fix MusicSelectUI Problems")]
    static void Fix()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "MusicSelectUI")
        {
            EditorUtility.DisplayDialog("알림", "MusicSelectUI 씬을 열고 실행해주세요.", "OK");
            return;
        }

        // 1. Main Camera 제거 (XR Origin이 자체 카메라를 가짐)
        var mainCam = GameObject.Find("Main Camera");
        if (mainCam != null && mainCam.GetComponent<Camera>() != null)
        {
            // XR Origin 자식이 아닌 독립 Main Camera만 제거
            bool isXRChild = mainCam.transform.parent != null;
            if (!isXRChild)
            {
                Undo.DestroyObjectImmediate(mainCam);
                Debug.Log("[Fix] Main Camera 제거 완료");
            }
        }

        // 2. Canvas를 World Space로 강제 설정 + 위치/크기 조정
        var canvasGO = GameObject.Find("SongSelectCanvas");
        if (canvasGO != null)
        {
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;

            // 플레이어 정면 1.8m, 눈높이 1.4m
            canvasGO.transform.position   = new Vector3(0f, 1.4f, 1.8f);
            canvasGO.transform.rotation   = Quaternion.identity;
            canvasGO.transform.localScale = Vector3.one * 0.0015f;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1200, 800);

            EditorUtility.SetDirty(canvasGO);
            Debug.Log("[Fix] Canvas 위치/크기 조정 완료");
        }

        // 3. XR Interaction Manager 확인 및 추가
        var xrMgr = Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        if (xrMgr == null)
        {
            var go = new GameObject("XR Interaction Manager");
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            Undo.RegisterCreatedObjectUndo(go, "Add XR Interaction Manager");
            Debug.Log("[Fix] XR Interaction Manager 추가 완료");
        }
        else
        {
            Debug.Log("[Fix] XR Interaction Manager 이미 있음 ✓");
        }

        // 4. XR Origin의 Locomotion 비활성화 (메뉴에서 이동 막기)
        var locomotion = GameObject.Find("Locomotion");
        if (locomotion != null)
        {
            locomotion.SetActive(false);
            EditorUtility.SetDirty(locomotion);
            Debug.Log("[Fix] Locomotion 비활성화 완료 (메뉴 중 이동 방지)");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Fix] 완료!");
    }

    // ─── EventSystem ────────────────────────────────────────────────────────────

    static void SetupEventSystem()
    {
        var es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        var standalone = es.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            Undo.DestroyObjectImmediate(standalone);

        if (es.GetComponent<XRUIInputModule>() == null)
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
    }

    // ─── XR Origin ──────────────────────────────────────────────────────────────

    static void AddXROrigin()
    {
        if (Object.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>() != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[MusicSelectUISetup] XR Origin 프리팹을 찾지 못했습니다: " + XROriginPrefabPath);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(instance, "Add XR Origin");
    }

    // ─── World Space Canvas ─────────────────────────────────────────────────────

    static Canvas CreateWorldSpaceCanvas()
    {
        var canvasGO = new GameObject("SongSelectCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 기본 GraphicRaycaster → TrackedDeviceGraphicRaycaster 교체
        var defaultRaycaster = canvasGO.GetComponent<GraphicRaycaster>();
        if (defaultRaycaster != null)
            Undo.DestroyObjectImmediate(defaultRaycaster);
        Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvasGO);

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // 플레이어 앞 2m, 1.6m 높이에 위치, 실물 크기 1200x800 유닛 → scale 0.002
        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(1200, 800);
        rt.localScale = Vector3.one * 0.002f;
        canvasGO.transform.SetPositionAndRotation(
            new Vector3(0, 1.4f, 2f),
            Quaternion.identity);

        return canvas;
    }

    // ─── UI 계층 ─────────────────────────────────────────────────────────────────

    static void CreateUIHierarchy(Canvas canvas)
    {
        var root = canvas.GetComponent<RectTransform>();

        // 배경 패널 (조선 밤하늘 - 진한 남색)
        var bgPanel = MakeRect("BackgroundPanel", root, Vector2.zero, Vector2.one);
        var bgImg   = bgPanel.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.10f);

        // 타이틀
        var title = MakeTMP("TitleText", root, 52, new Vector2(0, 0.88f), Vector2.one);
        title.text  = "SELECT SONG";
        title.color = new Color(0.95f, 0.80f, 0.30f); // 황금색
        title.fontStyle = FontStyles.Bold;

        // CarouselRoot - 카드들이 여기 동적으로 생성됨
        var carouselRoot = MakeRect("CarouselRoot", root,
            new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.87f));

        // InfoPanel (한지 느낌의 어두운 황갈색 배경)
        var infoPanel = MakeRect("InfoPanel", root,
            new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.33f));
        var infoBg = infoPanel.gameObject.AddComponent<Image>();
        infoBg.color = new Color(0.15f, 0.10f, 0.05f, 0.85f);

        var songNameText = MakeTMP("SongNameText", infoPanel, 56,
            new Vector2(0, 0.45f), Vector2.one);
        songNameText.text      = "Song Name";
        songNameText.color     = new Color(0.95f, 0.88f, 0.65f); // 한지색
        songNameText.fontStyle = FontStyles.Bold;

        var bpmText = MakeTMP("BpmText", infoPanel, 36,
            Vector2.zero, new Vector2(1, 0.42f));
        bpmText.text  = "BPM 000";
        bpmText.color = new Color(0.75f, 0.65f, 0.45f); // 황토색

        // 화살표 버튼 (묵록색 - 조선 단청의 녹색)
        var prevBtn = MakeButton("PrevButton", root, "◀",
            new Vector2(0, 0.38f), new Vector2(0.1f, 0.84f),
            new Color(0.08f, 0.18f, 0.10f));

        var nextBtn = MakeButton("NextButton", root, "▶",
            new Vector2(0.9f, 0.38f), new Vector2(1, 0.84f),
            new Color(0.08f, 0.18f, 0.10f));

        // 연주 시작 버튼 (홍색 - 전통 붉은색)
        var playBtn = MakeButton("PlayButton", root, "PLAY",
            new Vector2(0.35f, 0.01f), new Vector2(0.65f, 0.16f),
            new Color(0.55f, 0.08f, 0.08f));
        playBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // ─── SongSelectManager ─────────────────────────────────────────────────
        var managerGO = new GameObject("SongSelectManager");
        Undo.RegisterCreatedObjectUndo(managerGO, "Create SongSelectManager");

        var manager = managerGO.AddComponent<SongSelectManager>();

        // UI 레퍼런스 자동 연결
        manager.cardContainer = carouselRoot;
        manager.songNameText  = songNameText;
        manager.bpmText       = bpmText;
        manager.prevButton    = prevBtn;
        manager.nextButton    = nextBtn;
        manager.playButton    = playBtn;

        // 차트 에셋 자동 연결
        var rock = AssetDatabase.LoadAssetAtPath<ChartDataSO>(RockChartPath);
        var auto = AssetDatabase.LoadAssetAtPath<ChartDataSO>(AutoChartPath);
        var chartList = new System.Collections.Generic.List<ChartDataSO>();
        if (rock != null) chartList.Add(rock);
        if (auto != null) chartList.Add(auto);
        manager.charts = chartList.ToArray();

        Debug.Log($"[MusicSelectUISetup] 차트 {chartList.Count}개 자동 연결됨. " +
                  "SongCard 프리팹만 Inspector에서 연결해주세요.");
    }

    // ─── 헬퍼 ───────────────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI MakeTMP(string name, RectTransform parent, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rt = MakeRect(name, parent, anchorMin, anchorMax);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return tmp;
    }

    static Button MakeButton(string name, RectTransform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        var rt = MakeRect(name, parent, anchorMin, anchorMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bgColor;

        var btn = rt.gameObject.AddComponent<Button>();

        // ColorBlock으로 hover/press 색상
        var cb = btn.colors;
        cb.highlightedColor = bgColor * 1.3f;
        cb.pressedColor     = bgColor * 0.7f;
        btn.colors = cb;

        var textRT = MakeRect("Text", rt, Vector2.zero, Vector2.one);
        var tmp    = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 60;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return btn;
    }
}
