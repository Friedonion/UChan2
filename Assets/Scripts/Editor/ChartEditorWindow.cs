using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ChartEditorWindow : EditorWindow
{
    [MenuItem("Chart/Chart Editor & Generator")]
    public static void ShowWindow()
    {
        GetWindow<ChartEditorWindow>("Chart Editor & Generator");
    }

    private int activeTab = 0;
    private readonly string[] tabLabels = { "Auto Generator", "Chart Editor" };

    // ==========================================
    // Tab 1: Auto Generator Variables
    // ==========================================
    [Header("Generator Settings")]
    public AudioClip autoAudioClip;
    public float autoBpm = 120f;
    public float autoThreshold = 0.2f;
    public float autoMinInterval = 0.2f;
    public string autoSavePath = "Assets/Data/Charts";
    public string autoFileName = "auto_generated_chart";

    // ==========================================
    // Tab 2: Chart Editor Variables
    // ==========================================
    [Header("Editor Settings")]
    public ChartDataSO activeChart;
    public AudioClip editorAudioClip;
    
    // Audio Player System
    private AudioSource previewSource;
    private GameObject previewObject;
    private bool isAudioPlaying = false;
    
    // Scroll list & Timeline Settings
    private float zoomScale = 150f;
    private bool autoScroll = true;
    private Vector2 timelineScrollPosition = Vector2.zero;
    private NoteData selectedNote = null;
    
    // Drag & Drop State
    private bool isDraggingNote = false;
    private NoteData draggingNote = null;
    private float dragStartMouseX = 0f;
    private float dragStartNoteTime = 0f;
    private int dragStartNoteLane = 0;

    // Temporary variables for Direction conversion
    private Dictionary<NoteData, float> noteAngles = new Dictionary<NoteData, float>();

    void OnEnable()
    {
        // 에디터 활성화 시 씬에 남아 있을 수 있는 미사용 오디오 소스 청소
        CleanAudioSource();
        EditorApplication.update += UpdateEditor;
    }

    void OnDisable()
    {
        CleanAudioSource();
        EditorApplication.update -= UpdateEditor;
    }

    void UpdateEditor()
    {
        if (activeTab == 1 && isAudioPlaying && previewSource != null)
        {
            // 음악 재생 상태를 UI 갱신을 위해 주기적으로 Repaint
            Repaint();
            if (!previewSource.isPlaying)
            {
                isAudioPlaying = false;
            }
        }
    }

    private void CleanAudioSource()
    {
        if (previewSource != null)
        {
            previewSource.Stop();
        }
        
        // 이름으로 기존 오브젝트 찾아 강제 정리
        GameObject oldObj = GameObject.Find("[ChartEditorPreviewAudio]");
        if (oldObj != null)
        {
            DestroyImmediate(oldObj);
        }

        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }
        isAudioPlaying = false;
    }

    private void InitAudioSource()
    {
        CleanAudioSource();
        if (editorAudioClip == null) return;

        previewObject = new GameObject("[ChartEditorPreviewAudio]");
        previewObject.hideFlags = HideFlags.HideAndDontSave;
        previewSource = previewObject.AddComponent<AudioSource>();
        previewSource.clip = editorAudioClip;
        previewSource.playOnAwake = false;
        previewSource.loop = false;
    }

    private void OnGUI()
    {
        Event e = Event.current;

        // 🎹 [GLOBAL FOCUS & SPACEBAR POPUP BUG FIX]
        // 1. Clear keyboard focus whenever the user clicks anywhere in the window area.
        // This ensures the ObjectField loses focus immediately after selection/drag-drop.
        if (e != null && e.type == EventType.MouseDown)
        {
            GUIUtility.keyboardControl = 0;
        }

        // 2. Intercept spacebar at the very top of OnGUI before any EditorGUILayout fields are drawn.
        // Even if an ObjectField has keyboard focus, we swallow the spacebar Event so Unity doesn't open the Object Picker.
        if (activeTab == 1 && e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
        {
            GUIUtility.keyboardControl = 0; // Force clear focus
            if (previewSource != null && previewSource.isPlaying)
            {
                DetectSpacebarTapping(previewSource.time);
            }
            e.Use(); // Swallows the spacebar event to prevent opening the Object Picker/Asset Selector
        }

        GUILayout.Space(10);
        activeTab = GUILayout.Toolbar(activeTab, tabLabels, GUILayout.Height(25));
        GUILayout.Space(10);

        if (activeTab == 0)
        {
            DrawAutoGenerator();
        }
        else
        {
            DrawChartEditor();
        }
    }

    // ==========================================================
    // DRAW AUTO GENERATOR TAB
    // ==========================================================
    private void DrawAutoGenerator()
    {
        GUILayout.Label("Auto Chart Generator (Volume Peak Based)", EditorStyles.boldLabel);
        GUILayout.Space(5);

        autoAudioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", autoAudioClip, typeof(AudioClip), false);
        autoBpm = EditorGUILayout.FloatField("BPM", autoBpm);
        autoThreshold = EditorGUILayout.Slider("Sensitivity (Threshold)", autoThreshold, 0.01f, 1.0f);
        autoMinInterval = EditorGUILayout.FloatField("Min Note Interval", autoMinInterval);
        autoSavePath = EditorGUILayout.TextField("Save Path", autoSavePath);
        autoFileName = EditorGUILayout.TextField("File Name", autoFileName);

        GUILayout.Space(15);
        if (GUILayout.Button("Generate and Save SO", GUILayout.Height(35)))
        {
            if (autoAudioClip == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign an AudioClip first!", "OK");
                return;
            }
            GenerateChart();
        }
    }

    void GenerateChart()
    {
        float[] samples = new float[autoAudioClip.samples * autoAudioClip.channels];
        autoAudioClip.GetData(samples, 0);

        ChartDataSO so = ScriptableObject.CreateInstance<ChartDataSO>();
        so.songName = autoAudioClip.name;
        so.audioClip = autoAudioClip; // 🎵 생성 시 오디오 클립 자동 귀속
        so.bpm = autoBpm;
        so.travelTime = 4.5f;

        float sampleRate = autoAudioClip.frequency;
        float lastSpawnTime = -autoMinInterval;
        float beatDuration = 60f / autoBpm;

        int currentStreakType = -1; 
        int streakRemaining = 0;

        float spawnDist = 25.0f;
        NoteSpawner spawner = FindObjectOfType<NoteSpawner>();
        if (spawner != null) spawnDist = spawner.spawnDistance;
        float wallPassRatio = so.travelTime / Mathf.Max(1f, spawnDist);
        float[] laneWallEndTimes = new float[4] { -1f, -1f, -1f, -1f };
        float[] laneLongNoteEndTimes = new float[4] { -1f, -1f, -1f, -1f };
        float longNoteCooldown = 0f;

        for (int i = 0; i < samples.Length; i += 1024)
        {
            float time = (float)i / (sampleRate * autoAudioClip.channels);
            
            float maxVol = 0f;
            for(int j = 0; j < 1024 && (i+j) < samples.Length; j++) {
                if (Mathf.Abs(samples[i+j]) > maxVol) maxVol = Mathf.Abs(samples[i+j]);
            }
            float volume = maxVol;

            if (volume > autoThreshold && time > lastSpawnTime + autoMinInterval)
            {
                float quantizedTime = Mathf.Round(time / (beatDuration / 2f)) * (beatDuration / 2f);
                if (quantizedTime <= lastSpawnTime) quantizedTime = lastSpawnTime + (beatDuration / 2f);

                NoteData note = new NoteData();
                note.time = quantizedTime;
                
                if (streakRemaining <= 0)
                {
                    // 0: 베기(Slashing), 1: 부치기(Fanning), 2: 치기(Hit) 위주
                    float rand = Random.value;
                    if (rand < 0.35f) currentStreakType = 0; // 35% 베기
                    else if (rand < 0.70f) currentStreakType = 1; // 35% 부치기
                    else currentStreakType = 2; // 30% 치기 위주

                    streakRemaining = Random.Range(2, 5);
                }

                if (currentStreakType == 0)
                {
                    note.type = NoteType.Slashing;
                }
                else if (currentStreakType == 1)
                {
                    // 부치기(Fanning) 위주 (단, 소리가 극단적으로 크면 타격감을 위해 Hit)
                    note.type = (volume > autoThreshold * 1.8f) ? NoteType.Hit : NoteType.Fanning;
                }
                else
                {
                    // 치기(Hit) 위주
                    note.type = (volume > autoThreshold * 1.3f || Random.value > 0.4f) ? NoteType.Hit : NoteType.Fanning;
                }

                // 5% chance to spawn a Wall, but ONLY if no wall is currently active
                bool isAnyWallActive = false;
                for (int l = 0; l < 4; l++)
                {
                    if (quantizedTime < laneWallEndTimes[l]) isAnyWallActive = true;
                }

                int activeLongNotes = 0;
                int activeLongNoteLane = -1;
                for (int l = 0; l < 4; l++)
                {
                    if (quantizedTime < laneLongNoteEndTimes[l])
                    {
                        activeLongNotes++;
                        activeLongNoteLane = l;
                    }
                }

                if (activeLongNotes >= 2)
                {
                    lastSpawnTime = quantizedTime;
                    continue; // Skip note generation entirely, hands are full
                }

                if (activeLongNotes == 0 && !isAnyWallActive && streakRemaining == Random.Range(2, 5) && Random.value < 0.05f) 
                {
                    note.type = NoteType.Wall;
                }
                else if (quantizedTime > longNoteCooldown && activeLongNotes < 2 && Random.value < 0.03f) // 15% -> 3%로 롱노트 확률 대폭 감소
                {
                    note.type = (Random.value < 0.5f) ? NoteType.HoldFolded : NoteType.HoldOpen;
                }

                streakRemaining--;

                List<int> freeLanes = new List<int>();
                for (int l = 0; l < 4; l++)
                {
                    if (quantizedTime >= laneWallEndTimes[l] && quantizedTime >= laneLongNoteEndTimes[l]) 
                        freeLanes.Add(l);
                }

                // 와리가리 방지: 롱노트가 하나 진행중이면 반대쪽 레인만 freeLanes로 남김
                if (activeLongNotes == 1 && activeLongNoteLane != -1)
                {
                    if (activeLongNoteLane <= 1)
                    {
                        freeLanes.Remove(0);
                        freeLanes.Remove(1);
                    }
                    else
                    {
                        freeLanes.Remove(2);
                        freeLanes.Remove(3);
                    }
                }

                // --- 안전 지대(Safe Zone) 로직 ---
                // 가운데 벽이 있을 때 플레이어 동선을 한쪽으로 유도하고
                // 벽 반대편이나 양쪽에 노트가 나오는 불합리한 패턴을 방지합니다.
                bool wallInLeftCenter = (quantizedTime < laneWallEndTimes[1]);
                bool wallInRightCenter = (quantizedTime < laneWallEndTimes[2]);
                bool wallInFarLeft = (quantizedTime < laneWallEndTimes[0]);
                bool wallInFarRight = (quantizedTime < laneWallEndTimes[3]);

                if (wallInLeftCenter)
                {
                    freeLanes.Remove(0); // 왼쪽으로 피할 수 없도록 왼쪽 레인 제거
                    freeLanes.Remove(1);
                }
                if (wallInRightCenter)
                {
                    freeLanes.Remove(2); // 오른쪽으로 피할 수 없도록 오른쪽 레인 제거
                    freeLanes.Remove(3);
                }
                if (wallInFarLeft) freeLanes.Remove(0);
                if (wallInFarRight) freeLanes.Remove(3);

                if (freeLanes.Count == 0)
                {
                    lastSpawnTime = quantizedTime; // 피할 곳이 없거나 막혔으면 노트를 생성하지 않음
                    continue; 
                }

                note.lane = freeLanes[Random.Range(0, freeLanes.Count)];
                note.row = (note.type == NoteType.Hit) ? 0 : Random.Range(0, 2);
                
                if (note.type == NoteType.Wall)
                {
                    note.direction = new Vector3(0.5f, 3f, Random.Range(10f, 30f)); // Default wall: width 0.5 (1 lane), height 3, depth 10~30
                    float wallTimeLength = note.direction.z * wallPassRatio;
                    // 벽은 중심축(Z=0)을 기준으로 앞뒤로 확장되므로 앞면(Front)이 현재 시간에 오도록 중심 시간을 뒤로 미룸
                    // (과거에 이미 생성된 일반 노트들을 벽이 덮치는 현상 방지)
                    note.time = quantizedTime + (wallTimeLength / 2f);
                    laneWallEndTimes[note.lane] = quantizedTime + wallTimeLength + 0.5f; // Add 0.5s padding
                }
                else if (note.type == NoteType.HoldFolded || note.type == NoteType.HoldOpen)
                {
                    note.duration = Random.Range(1.0f, 3.0f);
                    note.endLane = note.lane;

                    if (Random.value < 0.5f)
                    {
                        List<int> possibleEndLanes = new List<int>();
                        if (note.lane > 0 && freeLanes.Contains(note.lane - 1)) possibleEndLanes.Add(note.lane - 1);
                        if (note.lane < 3 && freeLanes.Contains(note.lane + 1)) possibleEndLanes.Add(note.lane + 1);

                        if (possibleEndLanes.Count > 0)
                        {
                            note.endLane = possibleEndLanes[Random.Range(0, possibleEndLanes.Count)];
                            laneLongNoteEndTimes[note.endLane] = quantizedTime + note.duration;
                        }
                    }
                    laneLongNoteEndTimes[note.lane] = quantizedTime + note.duration;
                    longNoteCooldown = quantizedTime + note.duration + Random.Range(2.0f, 5.0f); // 롱노트 종료 후 최소 2~5초 휴식

                    // --- 중간 지점(Midpoints) 곡선 추가 로직 ---
                    if (note.lane != note.endLane)
                    {
                        // 대각선 이동: Smoothstep / 3개의 웨이포인트 추가
                        int steps = 4;
                        for (int k = 1; k < steps; k++)
                        {
                            float t = (float)k / steps;
                            float smoothT = t * t * (3f - 2f * t); // 부드러운 S자 곡선
                            float currentLane = Mathf.Lerp(note.lane, note.endLane, smoothT);
                            
                            // 밖으로 살짝 부풀어오르는 아치 추가
                            float arc = Mathf.Sin(t * Mathf.PI) * 0.3f;
                            float dir = Mathf.Sign(note.endLane - note.lane); // 이동 방향
                            currentLane += arc * -dir; // 이동 방향 반대로 부풀게 함
                            
                            currentLane = Mathf.Clamp(currentLane, 0f, 3f);
                            note.midpoints.Add(new LongNoteWaypoint { lane = currentLane, row = note.row });
                        }
                    }
                    else if (Random.value < 0.5f)
                    {
                        // 직선 유지 시: 50% 확률로 좌우로 살짝 출렁이는 물결
                        int steps = 5;
                        for (int k = 1; k < steps; k++)
                        {
                            float t = (float)k / steps;
                            float wiggle = Mathf.Sin(t * Mathf.PI * 2f) * 0.3f; // 한 바퀴 출렁임
                            float currentLane = Mathf.Clamp(note.lane + wiggle, 0f, 3f);
                            note.midpoints.Add(new LongNoteWaypoint { lane = currentLane, row = note.row });
                        }
                    }
                }
                else
                {
                    Vector3 randDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                    note.direction = (randDir == Vector3.zero) ? Vector3.right : randDir;
                }

                so.notes.Add(note);
                lastSpawnTime = quantizedTime;
            }
        }

        // 폴더 생성 확인
        if (!AssetDatabase.IsValidFolder(autoSavePath))
        {
            string[] folders = autoSavePath.Split('/');
            string current = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string next = Path.Combine(current, folders[i]).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, folders[i]);
                current = next;
            }
        }

        string fullPath = Path.Combine(autoSavePath, autoFileName + ".asset").Replace("\\", "/");
        string absolutePath = Path.GetFullPath(fullPath);

        // 🛡 [Perforce/OS Read-Only Bypass]
        // If file exists, strip Read-Only file attribute before AssetDatabase access
        if (File.Exists(absolutePath))
        {
            try
            {
                File.SetAttributes(absolutePath, FileAttributes.Normal);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ReadOnly Bypass] Failed to set Normal file attribute: {ex.Message}");
            }
        }
        
        ChartDataSO existingSO = AssetDatabase.LoadAssetAtPath<ChartDataSO>(fullPath);
        if (existingSO != null)
        {
            // 💡 [GUID Preservation Hack]
            // Instead of deleting & recreating (which breaks Unity Prefab references and causes file lock errors under Perforce),
            // we overwrite the properties of the existing ScriptableObject asset in-place.
            Undo.RecordObject(existingSO, "Auto Generate Chart Overwrite");
            existingSO.songName = so.songName;
            existingSO.audioClip = so.audioClip;
            existingSO.bpm = so.bpm;
            existingSO.offset = so.offset;
            existingSO.travelTime = so.travelTime;
            existingSO.notes = new List<NoteData>(so.notes);
            
            EditorUtility.SetDirty(existingSO);
            so = existingSO; // For Selection and popup UI below
        }
        else
        {
            AssetDatabase.CreateAsset(so, fullPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"Chart generated at: {fullPath}\nTotal Notes: {so.notes.Count}", "OK");
        Selection.activeObject = so;
    }

    // ==========================================================
    // DRAW CHART EDITOR TAB
    // ==========================================================
    private void DrawChartEditor()
    {
        GUILayout.Label("Interactive DAW Timeline Chart Editor", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 1. 차트 SO 바인딩
        ChartDataSO prevChart = activeChart;
        activeChart = (ChartDataSO)EditorGUILayout.ObjectField("Select Chart SO", activeChart, typeof(ChartDataSO), false);
        if (prevChart != activeChart && activeChart != null)
        {
            noteAngles.Clear();
            selectedNote = null;

            // 🎵 [AUDIO AUTO-LOAD MATCH]
            // SO가 바뀌었을 때 SO 내부에 오디오 클립이 이미 귀속되어 있다면 에디터로 자동 연동합니다.
            if (activeChart.audioClip != null)
            {
                editorAudioClip = activeChart.audioClip;
                InitAudioSource();
            }
        }

        if (activeChart == null)
        {
            EditorGUILayout.HelpBox("Please assign a ChartDataSO to edit notes.", MessageType.Info);
            return;
        }

        // 2. 오디오 클립 및 미디어 플레이어
        GUILayout.Space(10);
        GUILayout.BeginVertical("box");
        GUILayout.Label("Audio Sync & Tapping System", EditorStyles.boldLabel);
        
        AudioClip prevClip = editorAudioClip;
        editorAudioClip = (AudioClip)EditorGUILayout.ObjectField("Sync Audio Clip", editorAudioClip, typeof(AudioClip), false);
        
        if (prevClip != editorAudioClip)
        {
            InitAudioSource();
            
            // 🎵 오디오 클립이 변경되면 activeChart SO에도 이 참조를 자동으로 저장해 줍니다.
            if (activeChart != null)
            {
                Undo.RecordObject(activeChart, "Change Chart Sync Audio");
                activeChart.audioClip = editorAudioClip;
                if (editorAudioClip != null && string.IsNullOrEmpty(activeChart.songName))
                {
                    activeChart.songName = editorAudioClip.name;
                }
                EditorUtility.SetDirty(activeChart);
            }
        }

        if (previewSource == null && editorAudioClip != null)
        {
            InitAudioSource();
        }

        float currTime = 0f;
        float duration = 120f;

        if (editorAudioClip != null)
        {
            duration = editorAudioClip.length;
        }
        else if (activeChart.notes.Count > 0)
        {
            duration = activeChart.notes[activeChart.notes.Count - 1].time + 10f;
        }

        // Audio controls UI
        if (editorAudioClip != null && previewSource != null)
        {
            currTime = previewSource.time;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isAudioPlaying ? "⏸ Pause" : "▶ Play Playback", GUILayout.Width(150), GUILayout.Height(25)))
            {
                GUIUtility.keyboardControl = 0; // Clear focus to avoid spacebar hotkey conflicts
                if (isAudioPlaying)
                {
                    previewSource.Pause();
                    isAudioPlaying = false;
                }
                else
                {
                    previewSource.Play();
                    isAudioPlaying = true;
                }
            }

            if (GUILayout.Button("⏹ Stop & Reset", GUILayout.Width(120), GUILayout.Height(25)))
            {
                GUIUtility.keyboardControl = 0; // Clear focus
                previewSource.Stop();
                previewSource.time = 0f;
                isAudioPlaying = false;
            }

            GUILayout.EndHorizontal();

            // Time Slider
            float newTime = EditorGUILayout.Slider("Audio Timeline", currTime, 0f, duration);
            if (Mathf.Abs(newTime - currTime) > 0.05f)
            {
                previewSource.time = newTime;
                currTime = newTime;
            }

            GUILayout.Label($"Current Time: {currTime:F2}s / {duration:F2}s", EditorStyles.miniLabel);

            // 스페이스바 안내 배너
            EditorGUILayout.HelpBox("🎹 [SPACEBAR HOTKEY]: Press SPACEBAR while audio plays to spawn a note at the playhead!", MessageType.Warning);

            // 키보드 스페이스바 단축키 이벤트 감지
            DetectSpacebarTapping(currTime);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Sync Audio Clip to enable Playback & Spacebar Tapping.", MessageType.Info);
        }
        GUILayout.EndVertical();

        // 3. 차트 글로벌 메타데이터 수정
        GUILayout.Space(10);
        GUILayout.BeginVertical("box");
        GUILayout.Label("Chart Metadata Settings", EditorStyles.boldLabel);
        activeChart.songName = EditorGUILayout.TextField("Song Name", activeChart.songName);
        activeChart.bpm = EditorGUILayout.FloatField("BPM", activeChart.bpm);
        activeChart.offset = EditorGUILayout.FloatField("Offset", activeChart.offset);
        activeChart.travelTime = EditorGUILayout.FloatField("Travel Time (Note Speed)", activeChart.travelTime);
        GUILayout.EndVertical();

        // ==========================================
        // 4. DAW STYLE VISUAL TIMELINE
        // ==========================================
        GUILayout.Space(15);
        GUILayout.BeginVertical("box");
        GUILayout.Label("🎼 Interactive Visual Timeline", EditorStyles.boldLabel);
        
        // Timeline Settings (Zoom and Auto Scroll)
        GUILayout.BeginHorizontal();
        zoomScale = EditorGUILayout.Slider("Zoom Scale (px/sec)", zoomScale, 50f, 500f);
        autoScroll = EditorGUILayout.Toggle("Auto Scroll Playhead", autoScroll);
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        float timelineWidth = duration * zoomScale;
        float timelineHeight = 160f; // Track Height: ruler (20) + 4 lanes * 35 = 160

        // Auto-Scroll calculation
        if (isAudioPlaying && autoScroll && previewSource != null)
        {
            float playheadViewportX = previewSource.time * zoomScale;
            float viewportWidth = position.width - 40f;
            float targetScrollX = playheadViewportX - (viewportWidth / 2f);
            timelineScrollPosition.x = Mathf.Clamp(targetScrollX, 0f, Mathf.Max(0f, timelineWidth - viewportWidth));
        }

        // Timeline Scroll View
        timelineScrollPosition = GUILayout.BeginScrollView(timelineScrollPosition, GUILayout.Height(190));
        
        // Reserve timeline rect
        Rect timelineRect = GUILayoutUtility.GetRect(timelineWidth, timelineHeight);
        GUI.Box(timelineRect, "", GUI.skin.box);

        Event e = Event.current;

        // Draw horizontal Lane Separators and Tracks
        for (int lane = 0; lane < 4; lane++)
        {
            float y = timelineRect.y + 20f + lane * 35f;
            Handles.color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
            Handles.DrawLine(new Vector2(timelineRect.x, y), new Vector2(timelineRect.x + timelineWidth, y));
            
            // Draw sticky Lane labels on the left edge
            GUI.Label(new Rect(timelineRect.x + 5f + timelineScrollPosition.x, y + 8f, 60f, 20f), $"Lane {lane}", EditorStyles.miniLabel);
        }
        Handles.color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
        Handles.DrawLine(new Vector2(timelineRect.x, timelineRect.y + 160f), new Vector2(timelineRect.x + timelineWidth, timelineRect.y + 160f));

        // Draw BPM Grid Lines
        float bpm = activeChart.bpm;
        if (bpm > 0f)
        {
            float beatDuration = 60f / bpm;
            if (beatDuration > 0.05f) // Performance guard
            {
                int maxBeats = Mathf.CeilToInt(duration / beatDuration);
                for (int beat = 0; beat <= maxBeats; beat++)
                {
                    float timeAtBeat = beat * beatDuration;
                    float x = timelineRect.x + timeAtBeat * zoomScale;

                    if (beat % 4 == 0)
                    {
                        Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.6f); // Bar Line
                    }
                    else
                    {
                        Handles.color = new Color(0.4f, 0.4f, 0.4f, 0.2f); // Beat Line
                    }

                    Handles.DrawLine(new Vector2(x, timelineRect.y + 20f), new Vector2(x, timelineRect.y + 160f));

                    // Beat Number Label in the ruler zone
                    if (beat % 4 == 0)
                    {
                        GUI.Label(new Rect(x + 3f, timelineRect.y + 2f, 50f, 18f), $"{beat}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        // Timeline Ruler interaction (Scrubbing)
        Rect rulerRect = new Rect(timelineRect.x, timelineRect.y, timelineWidth, 20f);
        if (rulerRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            {
                GUIUtility.keyboardControl = 0; // Clear focus
                float relativeX = e.mousePosition.x - timelineRect.x;
                float clickedTime = relativeX / zoomScale;
                clickedTime = Mathf.Clamp(clickedTime, 0f, duration);
                if (previewSource != null)
                {
                    previewSource.time = clickedTime;
                }
                e.Use();
            }
        }

        // Culling optimization: only render nodes that are visible in the scrollview
        float visibleLeft = timelineScrollPosition.x;
        float visibleRight = timelineScrollPosition.x + position.width;

        // Draw and Interact with Notes
        List<NoteData> notesToDelete = new List<NoteData>();
        
        for (int i = 0; i < activeChart.notes.Count; i++)
        {
            NoteData note = activeChart.notes[i];
            float noteX = timelineRect.x + note.time * zoomScale;
            float noteY = timelineRect.y + 20f + note.lane * 35f;

            // Viewport clipping check
            if (noteX < visibleLeft - 30f || noteX > visibleRight + 30f)
            {
                continue;
            }

            // Overlap Check (Editor Visualizer)
            bool isOverlapping = false;
            foreach (var other in activeChart.notes)
            {
                if (other == note) continue;
                if (other.lane == note.lane && other.row == note.row)
                {
                    float myEnd = note.time + (note.type == NoteType.Wall ? Mathf.Max(0.1f, note.direction.z * 0.2f) : 0.05f);
                    float otherEnd = other.time + (other.type == NoteType.Wall ? Mathf.Max(0.1f, other.direction.z * 0.2f) : 0.05f);
                    
                    if (note.time >= other.time && note.time < otherEnd) { isOverlapping = true; break; }
                    if (other.time >= note.time && other.time < myEnd) { isOverlapping = true; break; }
                }
            }

            // Determine color based on type
            Color nodeColor = Color.gray;
            switch (note.type)
            {
                case NoteType.Hit:
                    nodeColor = new Color(0.15f, 0.75f, 0.38f); // Vibrant Green
                    break;
                case NoteType.Slashing:
                    nodeColor = new Color(0.92f, 0.25f, 0.25f); // Vibrant Red
                    break;
                case NoteType.Fanning:
                    nodeColor = new Color(0.18f, 0.58f, 0.95f); // Vibrant Blue
                    break;
                case NoteType.Boss:
                    nodeColor = new Color(0.98f, 0.65f, 0.12f); // Vibrant Gold
                    break;
                case NoteType.Wall:
                    nodeColor = new Color(0.6f, 0.2f, 0.8f); // Purple
                    break;
            }

            // Draw selection glow
            if (selectedNote == note)
            {
                Handles.color = new Color(1f, 0.85f, 0.1f, 1f); // Golden highlight ring
                Handles.DrawWireDisc(new Vector3(noteX, noteY + 17.5f, 0f), Vector3.forward, 15f);
            }

            if (isOverlapping)
            {
                Handles.color = Color.red;
                Handles.DrawWireDisc(new Vector3(noteX, noteY + 17.5f, 0f), Vector3.forward, 18f);
                GUI.Label(new Rect(noteX - 6f, noteY - 10f, 20f, 20f), "⚠️", EditorStyles.boldLabel);
            }

            // Render the note node
            float nodeWidth = 20f;
            if (note.type == NoteType.Wall)
            {
                // Z축 길이에 비례하여 타임라인 상의 UI 직사각형 폭을 길게 뻗도록 처리
                nodeWidth = Mathf.Max(20f, note.direction.z * zoomScale * 0.2f); 
            }
            Rect noteNodeRect = new Rect(noteX - 10f, noteY + 7.5f, nodeWidth, 20f);
            EditorGUI.DrawRect(noteNodeRect, nodeColor);

            // Render Swing Angle Direction Indicator line inside/around node
            if (note.type != NoteType.Wall)
            {
                Vector2 center = new Vector2(noteX, noteY + 17.5f);
                Vector2 arrowEnd = center + (new Vector2(note.direction.x, -note.direction.y) * 11f);
                Handles.color = Color.white;
                Handles.DrawLine(center, arrowEnd);
            }

            // Tooltip or small index indicator
            GUI.Label(new Rect(noteX - 12f, noteY - 1f, 30f, 15f), $"#{i}", EditorStyles.miniLabel);

            // Handle Click / Drag Selection
            Rect interactionRect = new Rect(noteX - 15f, noteY + 2f, 30f, 31f);
            if (e.type == EventType.MouseDown && interactionRect.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = 0; // Clear focus
                selectedNote = note;
                isDraggingNote = true;
                draggingNote = note;
                dragStartMouseX = e.mousePosition.x;
                dragStartNoteTime = note.time;
                dragStartNoteLane = note.lane;
                e.Use();
            }
        }

        // Global drag handling
        if (isDraggingNote && draggingNote != null)
        {
            if (e.type == EventType.MouseDrag)
            {
                float deltaX = e.mousePosition.x - dragStartMouseX;
                float deltaTime = deltaX / zoomScale;
                float newTime = dragStartNoteTime + deltaTime;

                // Shift-key Snapping!
                if (e.shift && bpm > 0f)
                {
                    float snapUnit = (60f / bpm) / 4f; // Snap to 1/16th notes
                    newTime = Mathf.Round(newTime / snapUnit) * snapUnit;
                }

                draggingNote.time = Mathf.Max(0f, newTime);

                // Lane dragging
                float relativeMouseY = e.mousePosition.y - timelineRect.y - 20f;
                int newLane = Mathf.Clamp(Mathf.FloorToInt(relativeMouseY / 35f), 0, 3);
                draggingNote.lane = newLane;

                Undo.RecordObject(activeChart, "Drag Note");
                EditorUtility.SetDirty(activeChart);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                isDraggingNote = false;
                draggingNote = null;
                activeChart.notes.Sort((a, b) => a.time.CompareTo(b.time));
                EditorUtility.SetDirty(activeChart);
                e.Use();
            }
        }

        // Draw Vertical Audio Playhead Red Line
        if (previewSource != null)
        {
            float playheadX = timelineRect.x + previewSource.time * zoomScale;
            Handles.color = new Color(0.95f, 0.15f, 0.15f, 0.95f);
            Handles.DrawLine(new Vector2(playheadX, timelineRect.y), new Vector2(playheadX, timelineRect.y + 160f));

            // Custom Polygon Cap
            Vector3[] cap = new Vector3[] {
                new Vector3(playheadX, timelineRect.y, 0f),
                new Vector3(playheadX - 6f, timelineRect.y + 8f, 0f),
                new Vector3(playheadX + 6f, timelineRect.y + 8f, 0f),
                new Vector3(playheadX, timelineRect.y, 0f)
            };
            Handles.DrawAAConvexPolygon(cap);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // ==========================================
        // 5. SPLIT LAYOUT: UTILITIES & NOTE INSPECTOR
        // ==========================================
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        // Left Panel: Global Utilities (Width 45%)
        GUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.45f));
        GUILayout.Label("🛠 General Chart Utilities", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (GUILayout.Button("Add New Note", GUILayout.Height(25)))
        {
            Undo.RecordObject(activeChart, "Add New Note");
            NoteData defaultNote = new NoteData
            {
                time = previewSource != null ? previewSource.time : 0f,
                type = NoteType.Hit,
                lane = Random.Range(0, 4),
                row = 0,
                direction = Vector3.right
            };
            activeChart.notes.Add(defaultNote);
            selectedNote = defaultNote;
            EditorUtility.SetDirty(activeChart);
        }

        if (GUILayout.Button("Sort by Time", GUILayout.Height(25)))
        {
            Undo.RecordObject(activeChart, "Sort Notes");
            activeChart.notes.Sort((a, b) => a.time.CompareTo(b.time));
            EditorUtility.SetDirty(activeChart);
        }

        if (GUILayout.Button("Clear All Notes", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear All Notes", "Are you sure you want to delete ALL notes in this chart?", "Yes, Delete", "Cancel"))
            {
                Undo.RecordObject(activeChart, "Clear All Notes");
                activeChart.notes.Clear();
                noteAngles.Clear();
                selectedNote = null;
                EditorUtility.SetDirty(activeChart);
            }
        }

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Export to StreamingAssets", GUILayout.Height(25)))
        {
            ExportChartToJson();
        }
        if (GUILayout.Button("Save SO", GUILayout.Height(25)))
        {
            EditorUtility.SetDirty(activeChart);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "Chart saved successfully to asset database!", "OK");
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Right Panel: Selected Note Inspector (Width 50%)
        GUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.50f));
        GUILayout.Label("🔬 Selected Note Inspector", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (selectedNote != null && activeChart.notes.Contains(selectedNote))
        {
            int noteIndex = activeChart.notes.IndexOf(selectedNote);
            GUILayout.Label($"Editing Note Index: #{noteIndex}", EditorStyles.miniBoldLabel);
            
            // 1. Time
            selectedNote.time = EditorGUILayout.FloatField("Note Time (s)", selectedNote.time);
            
            // 2. Type Popup
            selectedNote.type = (NoteType)EditorGUILayout.EnumPopup("Note Type", selectedNote.type);
            
            // 3. Lane (0-3)
            selectedNote.lane = EditorGUILayout.IntSlider("Target Lane", selectedNote.lane, 0, 3);
            
            // 4. Row (0-2)
            selectedNote.row = EditorGUILayout.IntSlider("Target Row", selectedNote.row, 0, 2);
            
            // 5. Direction Angle (Slider <-> Vector3) or Wall Scale
            if (selectedNote.type == NoteType.Wall)
            {
                Vector3 wallScale = selectedNote.direction;
                if (wallScale.sqrMagnitude < 0.1f) wallScale = new Vector3(0.5f, 3f, 15f); // default fallback
                wallScale.x = EditorGUILayout.FloatField("Width (Lanes)", wallScale.x);
                wallScale.y = EditorGUILayout.FloatField("Height (Rows)", wallScale.y);
                wallScale.z = EditorGUILayout.FloatField("Length (Duration)", wallScale.z);
                if (wallScale != selectedNote.direction)
                {
                    selectedNote.direction = wallScale;
                    EditorUtility.SetDirty(activeChart);
                }
            }
            else
            {
                float angle = GetNoteAngle(selectedNote);
                float newAngle = EditorGUILayout.Slider("Swing Angle (deg)", angle, 0f, 360f);
                if (Mathf.Abs(newAngle - angle) > 0.1f)
                {
                    SetNoteAngle(selectedNote, newAngle);
                }
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Deselect Note", GUILayout.Height(25)))
            {
                selectedNote = null;
            }
            
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete Note", GUILayout.Height(25)))
            {
                Undo.RecordObject(activeChart, "Delete Selected Note");
                activeChart.notes.Remove(selectedNote);
                if (noteAngles.ContainsKey(selectedNote))
                {
                    noteAngles.Remove(selectedNote);
                }
                selectedNote = null;
                EditorUtility.SetDirty(activeChart);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Label("💡 Pro Tip: Hold SHIFT while dragging a note to snap to 1/16 beat Grid!", EditorStyles.helpBox);
        }
        else
        {
            selectedNote = null; // Guard clean
            EditorGUILayout.HelpBox("Click any note node on the timeline grid to view and edit its parameters in this Inspector.", MessageType.Info);
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DetectSpacebarTapping(float playbackTime)
    {
        // 유니티 GUI 영역에서 키보드 단축키 감지
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
        {
            // 스페이스바 기본 동작(포커스 토글 등)을 씹고 노트 생성을 위해 사용
            e.Use();

            Undo.RecordObject(activeChart, "Spacebar Tapping Note Spawn");

            NoteData tappedNote = new NoteData
            {
                time = Mathf.Round(playbackTime * 100f) / 100f, // 소수점 둘째자리 반올림
                type = NoteType.Hit,
                lane = Random.Range(0, 4),
                row = 0,
                direction = Vector3.right
            };
            
            activeChart.notes.Add(tappedNote);
            
            // 실시간 탭핑 시 시간 순으로 동시 정렬해 주는 것이 시각적으로 좋음
            activeChart.notes.Sort((a, b) => a.time.CompareTo(b.time));

            EditorUtility.SetDirty(activeChart);

            // 탭핑 성공 햅틱 오디오 느낌의 미세 디버그 로그 출력
            Debug.Log($"[Chart Tapping] Note Spawned at {playbackTime:F2}s, Lane: {tappedNote.lane}");
        }
    }

    // 방향 벡터를 각도(0~360도)로 환산
    private float GetNoteAngle(NoteData note)
    {
        if (noteAngles.TryGetValue(note, out float angle))
        {
            return angle;
        }

        Vector3 dir = note.direction;
        float calculatedAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (calculatedAngle < 0f) calculatedAngle += 360f;
        
        noteAngles[note] = calculatedAngle;
        return calculatedAngle;
    }

    // 각도를 방향 벡터로 변환하여 적용
    private void SetNoteAngle(NoteData note, float newAngle)
    {
        noteAngles[note] = newAngle;
        float rad = newAngle * Mathf.Deg2Rad;
        note.direction = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;
        EditorUtility.SetDirty(activeChart);
    }

    private void ExportChartToJson()
    {
        if (activeChart == null) return;

        // 곡 이름으로 StreamingAssets/Charts/<songName>/ 폴더를 만들고 그 안에
        // chart.json + 오디오 + 커버 이미지를 함께 내보낸다.
        // 빌드 후에는 이 폴더의 파일만 교체하면 재빌드 없이 채보가 바뀐다.
        string songFolderName = string.IsNullOrEmpty(activeChart.songName) ? activeChart.name : activeChart.songName;
        foreach (char c in Path.GetInvalidFileNameChars())
            songFolderName = songFolderName.Replace(c, '_');
        songFolderName = songFolderName.TrimEnd('.', ' '); // Windows는 폴더명 끝의 마침표/공백을 조용히 잘라내므로 미리 제거

        string targetFolder = Path.Combine(Application.streamingAssetsPath, "Charts", songFolderName);
        Directory.CreateDirectory(targetFolder);

        ChartData data = new ChartData
        {
            songName = activeChart.songName,
            bpm = activeChart.bpm,
            offset = activeChart.offset,
            travelTime = activeChart.travelTime,
            notes = new List<NoteInfo>()
        };

        foreach (var note in activeChart.notes)
        {
            data.notes.Add(new NoteInfo
            {
                time = note.time,
                lane = note.lane,
                row = note.row,
                type = note.type.ToString(), // enum 순서가 바뀌어도 안전하도록 문자열로 저장
                duration = note.duration,
                endLane = note.endLane,
                endRow = note.endRow,
                midpoints = new List<LongNoteWaypoint>(note.midpoints),
                direction = new float[] { note.direction.x, note.direction.y, note.direction.z }
            });
        }

        if (activeChart.audioClip != null)
        {
            string sourceAudioPath = AssetDatabase.GetAssetPath(activeChart.audioClip);
            if (!string.IsNullOrEmpty(sourceAudioPath) && File.Exists(sourceAudioPath))
            {
                string audioFileName = Path.GetFileName(sourceAudioPath);
                string destAudioPath = Path.Combine(targetFolder, audioFileName);
                if (File.Exists(destAudioPath)) File.SetAttributes(destAudioPath, FileAttributes.Normal);
                File.Copy(sourceAudioPath, destAudioPath, true);
                data.audioFile = audioFileName;
            }
            else
            {
                Debug.LogWarning("[ChartEditorWindow] 오디오 클립의 원본 에셋 파일을 찾을 수 없어 audioFile을 비웁니다.");
            }
        }

        if (activeChart.coverSprite != null && activeChart.coverSprite.texture != null)
        {
            string sourceCoverPath = AssetDatabase.GetAssetPath(activeChart.coverSprite.texture);
            if (!string.IsNullOrEmpty(sourceCoverPath) && File.Exists(sourceCoverPath))
            {
                string coverFileName = Path.GetFileName(sourceCoverPath);
                string destCoverPath = Path.Combine(targetFolder, coverFileName);
                if (File.Exists(destCoverPath)) File.SetAttributes(destCoverPath, FileAttributes.Normal);
                File.Copy(sourceCoverPath, destCoverPath, true);
                data.coverFile = coverFileName;
            }
        }

        string json = JsonUtility.ToJson(data, true);
        string jsonPath = Path.Combine(targetFolder, "chart.json");
        if (File.Exists(jsonPath)) File.SetAttributes(jsonPath, FileAttributes.Normal);
        File.WriteAllText(jsonPath, json);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success",
            $"Chart exported to:\n{jsonPath}\n\n빌드 후에는 이 폴더의 파일만 교체하면 채보가 바뀝니다.", "OK");
    }
}
