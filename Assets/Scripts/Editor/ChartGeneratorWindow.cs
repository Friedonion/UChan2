using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ChartGeneratorWindow : EditorWindow
{
    [MenuItem("Tools/Trace of Wind/Chart Generator")]
    public static void ShowWindow()
    {
        GetWindow<ChartGeneratorWindow>("Chart Generator");
    }

    public AudioClip audioClip;
    public float bpm = 120f;
    public float threshold = 0.2f;
    public float minInterval = 0.2f; // 최소 노트 간격
    public string saveFileName = "auto_generated_chart.json";

    private void OnGUI()
    {
        GUILayout.Label("Auto Chart Generator", EditorStyles.boldLabel);
        
        audioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", audioClip, typeof(AudioClip), false);
        bpm = EditorGUILayout.FloatField("BPM", bpm);
        threshold = EditorGUILayout.Slider("Sensitivity (Threshold)", threshold, 0.01f, 1.0f);
        minInterval = EditorGUILayout.FloatField("Min Note Interval", minInterval);
        saveFileName = EditorGUILayout.TextField("Save File Name", saveFileName);

        if (GUILayout.Button("Generate Chart"))
        {
            if (audioClip == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign an AudioClip first!", "OK");
                return;
            }
            GenerateChart();
        }
    }

    void GenerateChart()
    {
        float[] samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);

        List<NoteInfo> generatedNotes = new List<NoteInfo>();
        float sampleRate = audioClip.frequency;
        float lastSpawnTime = -minInterval;
        float beatDuration = 60f / bpm;

        // 연속 노트를 위한 변수
        int currentStreakType = -1; // 0: Slashing streak, 1: Fanning/Hit streak
        int streakRemaining = 0;

        for (int i = 0; i < samples.Length; i += 1024)
        {
            float time = (float)i / (sampleRate * audioClip.channels);
            float volume = Mathf.Abs(samples[i]);

            if (volume > threshold && time > lastSpawnTime + minInterval)
            {
                float quantizedTime = Mathf.Round(time / (beatDuration / 2f)) * (beatDuration / 2f);
                if (quantizedTime <= lastSpawnTime) quantizedTime = lastSpawnTime + (beatDuration / 2f);

                NoteInfo info = new NoteInfo();
                info.time = quantizedTime;
                
                // --- 연속 노트 로직 ---
                if (streakRemaining <= 0)
                {
                    // 새로운 스트레이크 시작 (0계열 또는 1&2계열)
                    currentStreakType = (Random.value > 0.5f) ? 0 : 1;
                    streakRemaining = Random.Range(2, 5); // 2~4개 연속 생성
                }

                if (currentStreakType == 0)
                {
                    // 0번(베기) 스트레이크
                    info.type = 0;
                }
                else
                {
                    // 1&2번(부치기/타격) 스트레이크
                    // 볼륨이 크면 타격(2), 아니면 부치기(1)를 섞어서 생성
                    info.type = (volume > threshold * 1.5f || Random.value > 0.6f) ? 2 : 1;
                }

                streakRemaining--;
                // ----------------------

                info.lane = Random.Range(0, 4);
                info.row = (info.type == 2) ? 0 : Random.Range(0, 2); // Hit은 주로 아래쪽(row 0)에 배치
                
                
               
                info.direction = new float[] { Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0 };
                

                generatedNotes.Add(info);
                lastSpawnTime = quantizedTime;
            }
        }

        ChartData data = new ChartData();
        data.songName = audioClip.name;
        data.bpm = bpm;
        data.travelTime = 4.5f; // 기본 속도를 더 여유있게 설정
        data.notes = generatedNotes;

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.dataPath, saveFileName);
        File.WriteAllText(path, json);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"Chart generated at: Assets/{saveFileName}\nTotal Notes: {generatedNotes.Count}\nConsecutive patterns applied!", "OK");
    }
}
