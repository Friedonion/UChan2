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
    public GameObject notePrefab;
    public TextAsset chartJson; 
    
    [Header("Note Models")]
    public GameObject slashModel;
    public GameObject fanningModel;
    public GameObject hitModel;
    public GameObject bossModel;

    [Header("Grid Settings")]
    public float laneWidth = 0.5f;
    public float rowHeight = 0.5f;
    public float spawnDistance = 25.0f; // 거리를 조금 더 늘림 (느린 속도 대비 시야 확보)

    private ChartData chart;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    private float startTime;

    void Start()
    {
        if (chartJson != null)
        {
            LoadChart(chartJson.text);
            StartCoroutine(PlayChart());
        }
    }

    public void LoadChart(string json)
    {
        chart = JsonUtility.FromJson<ChartData>(json);
        // 시간을 기준으로 정렬
        chart.notes.Sort((a, b) => a.time.CompareTo(b.time));
    }

    System.Collections.IEnumerator PlayChart()
    {
        yield return new WaitForSeconds(1.0f);
        
        startTime = (float)AudioSettings.dspTime;
        isPlaying = true;
        
        Debug.Log($"🎵 {chart.songName} 시작! (속도: {chart.travelTime}s)");
    }

    void Update()
    {
        if (!isPlaying || chart == null || nextNoteIndex >= chart.notes.Count) return;

        float currentTime = (float)AudioSettings.dspTime - startTime;

        // JSON에서 가져온 travelTime 사용
        while (nextNoteIndex < chart.notes.Count && 
               chart.notes[nextNoteIndex].time - chart.travelTime <= currentTime)
        {
            SpawnNote(chart.notes[nextNoteIndex]);
            nextNoteIndex++;
        }
    }

    void SpawnNote(NoteInfo info)
    {
        float x = (info.lane - 1.5f) * laneWidth;
        float y = (info.row - 0.5f) * rowHeight + 1.0f;
        Vector3 spawnPos = new Vector3(x, y, spawnDistance);

        GameObject noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
        Note noteScript = noteObj.GetComponent<Note>();

        Vector3 dir = new Vector3(info.direction[0], info.direction[1], info.direction[2]);
        if (dir == Vector3.zero) dir = Vector3.right;

        AssignModels(noteScript, (NoteType)info.type);

        // JSON에서 가져온 travelTime으로 초기화
        noteScript.Initialize((NoteType)info.type, dir, startTime + info.time, chart.travelTime, spawnDistance);
    }

    void AssignModels(Note note, NoteType type)
    {
        // Note 클래스의 인디케이터 필드에 모델을 할당합니다.
        // 프리팹 내부에 미리 있을 수도 있지만, 여기서 동적으로 생성해 줄 수도 있습니다.
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
            obj.name = "Visual_" + type.ToString();
            
            // Note 스크립트의 해당 필드에 연결
            if (type == NoteType.Slashing) note.slashIndicator = obj;
            else if (type == NoteType.Fanning) note.fanIndicator = obj;
            else if (type == NoteType.Hit) note.hitIndicator = obj;
            else if (type == NoteType.Boss) note.bossIndicator = obj;
        }
    }
}
