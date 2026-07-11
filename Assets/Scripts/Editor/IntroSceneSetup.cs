using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class IntroSceneSetup
{
    const string XROriginPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    [MenuItem("Tools/Setup IntroScene")]
    static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "IntroScene")
        {
            EditorUtility.DisplayDialog("알림", "IntroScene 씬을 열고 실행해주세요.", "OK");
            return;
        }

        CleanupExisting();
        SetupAmbientLighting();
        SetupEventSystem();
        AddXROrigin();
        var canvas = CreateIntroCanvas();
        var (titleGroup, promptGroup) = CreateIntroUI(canvas);
        CreateFireflies();
        CreateIntroManager(titleGroup, promptGroup, canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[IntroSceneSetup] IntroScene 설정 완료!");
    }

    // ─── 기존 오브젝트 정리 (중복 실행 방지) ─────────────────────────────────────

    static void CleanupExisting()
    {
        var toDelete = new[] { "IntroCanvas", "IntroManager", "FireflyParticles" };
        var scene    = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // GetRootGameObjects()로 씬의 루트 오브젝트를 전부 순회해 동명 오브젝트 모두 삭제
        foreach (var root in scene.GetRootGameObjects())
        {
            if (System.Array.IndexOf(toDelete, root.name) >= 0)
                Object.DestroyImmediate(root);
        }
    }

    // ─── 조명 ────────────────────────────────────────────────────────────────────

    static void SetupAmbientLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.043f, 0.051f, 0.165f);
        RenderSettings.ambientEquatorColor = new Color(0.020f, 0.016f, 0.055f);
        RenderSettings.ambientGroundColor  = new Color(0.008f, 0.004f, 0.016f);
        RenderSettings.fog      = true;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.06f);
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.018f;

        // Moonlight: 차가운 달빛, 낮은 강도
        foreach (var l in Object.FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            l.color     = new Color(0.702f, 0.8f, 1.0f);
            l.intensity = 0.25f;
            l.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            EditorUtility.SetDirty(l);
        }
    }

    // ─── EventSystem ─────────────────────────────────────────────────────────────

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
        if (standalone != null) Undo.DestroyObjectImmediate(standalone);
        if (es.GetComponent<XRUIInputModule>() == null)
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
    }

    // ─── XR Origin ───────────────────────────────────────────────────────────────

    static void AddXROrigin()
    {
        // XR Origin이 자체 Main Camera를 포함하므로 씬의 독립 카메라를 먼저 제거
        var standaloneCamera = GameObject.Find("Main Camera");
        if (standaloneCamera != null && standaloneCamera.transform.parent == null)
        {
            Undo.DestroyObjectImmediate(standaloneCamera);
            Debug.Log("[IntroSceneSetup] 독립 Main Camera 제거 완료");
        }

        if (Object.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>() != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[IntroSceneSetup] XR Origin 프리팹을 찾지 못했습니다: " + XROriginPrefabPath);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(instance, "Add XR Origin");

        var locomotion = instance.transform.Find("Locomotion");
        if (locomotion != null)
        {
            locomotion.gameObject.SetActive(false);
            EditorUtility.SetDirty(locomotion.gameObject);
        }
    }

    // ─── Canvas ───────────────────────────────────────────────────────────────────

    static Canvas CreateIntroCanvas()
    {
        var canvasGO = new GameObject("IntroCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create IntroCanvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var defaultRaycaster = canvasGO.GetComponent<GraphicRaycaster>();
        if (defaultRaycaster != null) Undo.DestroyObjectImmediate(defaultRaycaster);
        Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvasGO);

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1400, 900);
        canvasGO.transform.SetPositionAndRotation(
            new Vector3(0f, 1.4f, 1.8f),
            Quaternion.identity);
        canvasGO.transform.localScale = Vector3.one * 0.0017f;

        return canvas;
    }

    // ─── UI 계층 ─────────────────────────────────────────────────────────────────

    static (CanvasGroup titleGroup, CanvasGroup promptGroup) CreateIntroUI(Canvas canvas)
    {
        var root = canvas.GetComponent<RectTransform>();

        // 배경 — 짙은 남색 반투명
        var bgImg = MakeRect("Background", root, Vector2.zero, Vector2.one)
            .gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.08f, 0.94f);

        // 상단/하단 금색 장식선
        MakeRect("TopDecoLine", root, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.84f))
            .gameObject.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f);
        MakeRect("BottomDecoLine", root, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.18f))
            .gameObject.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f);

        // 타이틀 그룹 (페이드 인/아웃 제어)
        var titleRT = MakeRect("TitleGroup", root, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.86f));
        var titleGroup = titleRT.gameObject.AddComponent<CanvasGroup>();

        // 영문 타이틀
        var titleText = MakeTMP("TitleText", titleRT, 90,
            new Vector2(0f, 0.45f), Vector2.one);
        titleText.text      = "Dance of the Wind";
        titleText.color     = new Color(0.95f, 0.82f, 0.35f); // 금색
        titleText.fontStyle = FontStyles.Bold | FontStyles.Italic;

        // 한글 타이틀
        var korText = MakeTMP("KoreanTitle", titleRT, 52,
            Vector2.zero, new Vector2(1f, 0.43f));
        korText.text             = "A VR Rhythm Experience";
        korText.color            = new Color(0.85f, 0.78f, 0.65f); // 크림색
        korText.characterSpacing = 10f;

        // 시작 프롬프트 그룹
        var promptRT = MakeRect("StartPromptGroup", root,
            new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.18f));
        var promptGroup = promptRT.gameObject.AddComponent<CanvasGroup>();

        var promptText = MakeTMP("PromptText", promptRT, 36, Vector2.zero, Vector2.one);
        promptText.text             = "[ Pull Trigger to Start ]";
        promptText.color            = new Color(0.75f, 0.85f, 0.95f); // 달빛색
        promptText.characterSpacing = 3f;

        return (titleGroup, promptGroup);
    }

    // ─── 반딧불 파티클 ───────────────────────────────────────────────────────────

    static void CreateFireflies()
    {
        var go = new GameObject("FireflyParticles");
        Undo.RegisterCreatedObjectUndo(go, "Create FireflyParticles");
        go.transform.SetPositionAndRotation(new Vector3(0f, 1.4f, 2f), Quaternion.identity);

        var ps   = go.AddComponent<ParticleSystem>();

        // URP 파티클 머티리얼 생성 및 저장
        const string matPath = "Assets/Scenes/IntroScene/FireflyMaterial.mat";
        var fireflyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (fireflyMat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes/IntroScene");
            var src = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/VRTemplateAssets/Materials/Particles/ConfettiParticles.mat");
            if (src != null)
            {
                fireflyMat = Object.Instantiate(src);
                fireflyMat.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(fireflyMat, matPath);
                AssetDatabase.SaveAssets();
            }
        }
        if (fireflyMat != null)
            go.GetComponent<ParticleSystemRenderer>().material = fireflyMat;

        var main = ps.main;
        main.startLifetime       = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed          = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize           = new ParticleSystem.MinMaxCurve(0.006f, 0.015f);
        main.maxParticles        = 80;
        main.simulationSpace     = ParticleSystemSimulationSpace.World;
        main.startColor          = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(3.2f, 1.6f, 0.4f);

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.15f;
        noise.frequency   = 0.4f;
        noise.scrollSpeed = 0.12f;
        noise.damping     = true;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 1f, 0.1f),  0f),    // 황록색 (반딧불 특유 색)
                new GradientColorKey(new Color(0.85f, 1f, 0.3f), 0.35f), // 밝은 황록 (최대 발광)
                new GradientColorKey(new Color(0.5f, 0.9f, 0.1f), 0.7f), // 어두운 녹황
                new GradientColorKey(new Color(0.3f, 0.7f, 0.05f), 1f),  // 소멸 직전 짙은 녹색
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.9f,  0.2f),
                new GradientAlphaKey(0.9f,  0.7f),
                new GradientAlphaKey(0f,    1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0f);
        sizeCurve.AddKey(0.25f, 1f);
        sizeCurve.AddKey(0.75f, 1f);
        sizeCurve.AddKey(1f, 0f);
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        EditorUtility.SetDirty(go);
    }

    // ─── IntroManager ────────────────────────────────────────────────────────────

    static void CreateIntroManager(CanvasGroup titleGroup, CanvasGroup promptGroup, Canvas canvas)
    {
        var go = new GameObject("IntroManager");
        Undo.RegisterCreatedObjectUndo(go, "Create IntroManager");
        var mgr = go.AddComponent<IntroManager>();
        mgr.titleGroup       = titleGroup;
        mgr.startPromptGroup = promptGroup;
        mgr.canvasTransform  = canvas.transform;
        mgr.nextSceneName    = "MusicSelectUI";
        EditorUtility.SetDirty(go);
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI MakeTMP(string name, RectTransform parent, float size,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var rt  = MakeRect(name, parent, anchorMin, anchorMax);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return tmp;
    }
}
