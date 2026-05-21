using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class NoteInfo
{
    public float time;       // 타격 시간
    public int lane;         // 0~3 (왼쪽~오른쪽)
    public int row;          // 0~2 (아래~위)
    public int type;         // 0:Slashing, 1:Fanning, 2:Hit, 3:Boss
    public float[] direction; // [x, y, z]
}

[System.Serializable]
public class ChartData
{
    public string songName;
    public float bpm;
    public float offset;
    public float travelTime = 4.0f; // 기본값 4.0초 (JSON에서 덮어쓰기 가능)
    public List<NoteInfo> notes;
}

public class NoteSpawner : MonoBehaviour
{
    private ChartDataSO activeChart; // 현재 플레이 중인 차트 (GameManager로부터 전달받음)
    public GameObject notePrefab;
    
    [Header("Note Models")]
    public GameObject slashModel;
    public GameObject fanningModel;
    public GameObject hitModel;
    public GameObject bossModel;

    [Header("Grid Settings")]
    public float laneWidth = 0.5f;
    public float rowHeight = 0.5f;
    public float spawnDistance = 25.0f; 

    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    private float startTime;

public void StartPlaying(ChartDataSO chart)
{
    if (isPlaying || chart == null) return;
    activeChart = chart;
    nextNoteIndex = 0;
    StartCoroutine(PlayChart());
}

public void StopPlaying()
{
    isPlaying = false;
    activeChart = null;
    StopAllCoroutines();
    
    // 화면에 남아있는 모든 노트 비활성화 (풀로 반환)
    Note[] activeNotes = FindObjectsOfType<Note>();
    foreach (Note note in activeNotes)
    {
        if (note.gameObject.activeSelf) note.Deactivate();
    }
}

System.Collections.IEnumerator PlayChart()
{
    yield return new WaitForSeconds(1.0f);

    startTime = (float)AudioSettings.dspTime;
    isPlaying = true;
}

    void Update()
    {
        if (!isPlaying || activeChart == null || nextNoteIndex >= activeChart.notes.Count) return;

        float currentTime = (float)AudioSettings.dspTime - startTime;

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

        GameObject noteObj;
        if (NotePoolManager.Instance != null)
        {
            noteObj = NotePoolManager.Instance.GetNote(spawnPos, Quaternion.identity);
        }
        else
        {
            noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
        }

        Note noteScript = noteObj.GetComponent<Note>();

        Vector3 dir = info.direction;
        if (dir == Vector3.zero) dir = Vector3.right;

        AssignModels(noteScript, info.type);

        // 전달받은 차트 데이터에서 가져온 travelTime으로 초기화
        noteScript.Initialize(info.type, dir, startTime + info.time, activeChart.travelTime, spawnDistance);
    }

    void AssignModels(Note note, NoteType type)
    {
        // 이미 생성된 비주얼 모델이 있는지 확인 (풀링 시 중복 생성 방지)
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
        }

        if (visualModel != null)
        {
            GameObject obj = Instantiate(visualModel, note.transform);
            obj.name = modelName;
            
            // Note 스크립트의 해당 필드에 연결
            if (type == NoteType.Slashing) note.slashIndicator = obj;
            else if (type == NoteType.Fanning) note.fanIndicator = obj;
            else if (type == NoteType.Hit) note.hitIndicator = obj;
            else if (type == NoteType.Boss) note.bossIndicator = obj;
        }
    }
}
