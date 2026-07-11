using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class NoteInfo
{
    public float time;       // ?��??�간
    public int lane;         // 0~3 (?�쪽~?�른�?
    public int row;          // 0~2 (?�래~??
    public int type;         // 0:Slashing, 1:Fanning, 2:Hit, 3:Boss
    public float[] direction; // [x, y, z]
}

[System.Serializable]
public class ChartData
{
    public string songName;
    public float bpm;
    public float offset;
    public float travelTime = 4.0f; // 기본�?4.0�?(JSON?�서 ??��?�기 가??
    public List<NoteInfo> notes;
}

public class NoteSpawner : MonoBehaviour
{
    private ChartDataSO activeChart; // ?�재 ?�레??중인 차트 (GameManager로�????�달받음)
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

    [Header("Grid Settings")]
    public float laneWidth = 0.5f;
    public float rowHeight = 0.5f;
    public float spawnDistance = 25.0f; 

    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    private float startTime;

public void StartPlaying(ChartDataSO chart, float leadInTime = 4.0f)
{
    if (isPlaying || chart == null) return;
    activeChart = chart;
    nextNoteIndex = 0;
    
    // ?�디?��? ?�작???�제 미래???�간 (?�재?�간 + ?�트가 ?�아???�간)
    startTime = (float)AudioSettings.dspTime + leadInTime;
    isPlaying = true;
}

public void StopPlaying()
{
    isPlaying = false;
    activeChart = null;
    StopAllCoroutines();
    
    // ?�면???�아?�는 모든 ?�트 비활?�화 (?��?반환)
    Note[] activeNotes = FindObjectsOfType<Note>();
    foreach (Note note in activeNotes)
    {
        if (note.gameObject.activeSelf) note.Deactivate();
    }
}

    void Update()
    {
        if (!isPlaying || activeChart == null || nextNoteIndex >= activeChart.notes.Count) return;

        float currentTime = (float)(GameManager.Instance != null ? GameManager.Instance.EffectiveDspTime : AudioSettings.dspTime) - startTime;

        while (nextNoteIndex < activeChart.notes.Count && 
               activeChart.notes[nextNoteIndex].time - activeChart.travelTime <= currentTime)
        {
            SpawnNote(activeChart.notes[nextNoteIndex]);
            nextNoteIndex++;
        }
    }

    void SpawnNote(NoteData info)
    {
        float x = (info.lane - 1.5f) * laneWidth;
        float y = (info.row - 0.5f) * rowHeight + 1.0f;
        Vector3 spawnPos = new Vector3(x, y, spawnDistance);

        // 노드가 나타날 때 이펙트 발생
        if (noteSpawnEffectPrefab != null)
        {
            GameObject effectObj = Instantiate(noteSpawnEffectPrefab, spawnPos, Quaternion.identity);
            effectObj.transform.localScale = Vector3.one * noteSpawnEffectScale; // 이펙트 크기 조절 적용
            NoteSpawnEffect effectScript = effectObj.GetComponent<NoteSpawnEffect>();
            if (effectScript != null)
            {
                effectScript.Play(info.type);
            }
        }

        GameObject noteObj;
        if (NotePoolManager.Instance != null)
        {
            noteObj = NotePoolManager.Instance.GetNote(info.type, spawnPos, Quaternion.identity);
        }
        else
        {
            noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
            noteObj.name = $"Note_{info.type}_DynamicFallback";
        }

        Note noteScript = noteObj.GetComponent<Note>();

        Vector3 dir = info.direction;
        if (dir == Vector3.zero) dir = Vector3.right;

        AssignModels(noteScript, info.type);


        // ?�달받�? 차트 ?�이?�에??가?�온 travelTime?�로 초기??
        noteScript.Initialize(info.type, dir, startTime + info.time, activeChart.travelTime, spawnDistance);
    }

    void AssignModels(Note note, NoteType type)
    {
        // ?��? ?�성??비주??모델???�는지 ?�인 (?��???중복 ?�성 방�?)
        string modelName = "Visual_" + type.ToString();
        Transform existingModel = note.transform.Find(modelName);
        
        if (existingModel != null) return; // ?��? ?�으�??�과

        GameObject visualModel = null;
        switch (type)
        {
            case NoteType.Slashing: visualModel = slashModel; break;
            case NoteType.Fanning: visualModel = fanningModel; break;
            case NoteType.Hit: visualModel = hitModel; break;
            case NoteType.Boss: visualModel = bossModel; break;
            case NoteType.Wall: visualModel = wallModel; break;
        }

        if (visualModel != null)
        {
            GameObject obj = Instantiate(visualModel, note.transform);
            obj.name = modelName;

            // Fanning ?�트 비주?��? ?�적?�로 ?�친 ?�태?�야 ?��?�?Animator�?비활?�화
            if (type == NoteType.Fanning)
            {
                Animator anim = obj.GetComponentInChildren<Animator>();
                if (anim != null) anim.speed = 100f;
            }

            // Note ?�크립트???�당 ?�드???�결
            if (type == NoteType.Slashing) note.slashIndicator = obj;
            else if (type == NoteType.Fanning) note.fanIndicator = obj;
            else if (type == NoteType.Hit) note.hitIndicator = obj;
            else if (type == NoteType.Boss) note.bossIndicator = obj;
            else if (type == NoteType.Wall) note.wallIndicator = obj;
        }
    }
}
