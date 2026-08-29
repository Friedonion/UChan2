using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class NoteData
{
    public float time;       // 타격 시간
    public int lane;         // 0~3 (왼쪽~오른쪽)
    public int row;          // 0~2 (아래~위)
    public NoteType type;    // NoteType enum 사용
    public Vector3 direction; // 타격 방향
    public float duration;   // 0 = 즉발 노트, >0 = 롱노트 (duration초 뒤 endLane/endRow 위치에 끝 노트 생성)
    public int endLane = -1; // 롱노트 끝 노트의 lane. -1이면 lane과 동일 (직선 롱노트)
    public int endRow = -1;  // 롱노트 끝 노트의 row. -1이면 row와 동일
    // 가이드라인이 시작→끝 사이에서 순서대로 지나가야 하는 지점들. 비어있으면 직선.
    public List<LongNoteWaypoint> midpoints = new List<LongNoteWaypoint>();
}

[CreateAssetMenu(fileName = "NewChart", menuName = "TraceOfWind/ChartData")]
public class ChartDataSO : ScriptableObject
{
    public string songName;
    public AudioClip audioClip;
    public Sprite coverSprite;
    public float bpm;
    public float offset;
    public float travelTime = 4.0f;
    public List<NoteData> notes = new List<NoteData>();
}
