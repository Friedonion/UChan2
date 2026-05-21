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

        for (int i = 0; i < samples.Length; i += 1024)
        {
            float time = (float)i / (sampleRate * autoAudioClip.channels);
            float volume = Mathf.Abs(samples[i]);

            if (volume > autoThreshold && time > lastSpawnTime + autoMinInterval)
            {
                float quantizedTime = Mathf.Round(time / (beatDuration / 2f)) * (beatDuration / 2f);
                if (quantizedTime <= lastSpawnTime) quantizedTime = lastSpawnTime + (beatDuration / 2f);

                NoteData note = new NoteData();
                note.time = quantizedTime;
                
                if (streakRemaining <= 0)
                {
                    currentStreakType = (Random.value > 0.5f) ? 0 : 1;
                    streakRemaining = Random.Range(2, 5);
                }

                if (currentStreakType == 0)
                {
                    note.type = NoteType.Slashing;
                }
                else
                {
                    note.type = (volume > autoThreshold * 1.5f || Random.value > 0.6f) ? NoteType.Hit : NoteType.Fanning;
                }

                streakRemaining--;

                note.lane = Random.Range(0, 4);
                note.row = (note.type == NoteType.Hit) ? 0 : Random.Range(0, 2);
                
                Vector3 randDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                note.direction = (randDir == Vector3.zero) ? Vector3.right : randDir;

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
            }

            // Draw selection glow
            if (selectedNote == note)
            {
                Handles.color = new Color(1f, 0.85f, 0.1f, 1f); // Golden highlight ring
                Handles.DrawWireDisc(new Vector3(noteX, noteY + 17.5f, 0f), Vector3.forward, 15f);
            }

            // Render the note node
            Rect noteNodeRect = new Rect(noteX - 10f, noteY + 7.5f, 20f, 20f);
            EditorGUI.DrawRect(noteNodeRect, nodeColor);

            // Render Swing Angle Direction Indicator line inside/around node
            Vector2 center = new Vector2(noteX, noteY + 17.5f);
            Vector2 arrowEnd = center + (new Vector2(note.direction.x, -note.direction.y) * 11f);
            Handles.color = Color.white;
            Handles.DrawLine(center, arrowEnd);

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
        if (GUILayout.Button("Export JSON", GUILayout.Height(25)))
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
            
            // 5. Direction Angle (Slider <-> Vector3)
            float angle = GetNoteAngle(selectedNote);
            float newAngle = EditorGUILayout.Slider("Swing Angle (deg)", angle, 0f, 360f);
            if (Mathf.Abs(newAngle - angle) > 0.1f)
            {
                SetNoteAngle(selectedNote, newAngle);
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

        string path = EditorUtility.SaveFilePanel("Export Chart JSON Backup", "Assets", activeChart.name + "_backup", "json");
        if (!string.IsNullOrEmpty(path))
        {
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
                    type = (int)note.type,
                    direction = new float[] { note.direction.x, note.direction.y, note.direction.z }
                });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Chart backup exported to:\n{path}", "OK");
        }
    }
}
