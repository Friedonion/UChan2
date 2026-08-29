using UnityEngine;
using System.Collections.Generic;

// 롱노트 가이드라인이 지나가야 하는 중간 지점 하나. 실제 노트로 스폰되지 않는,
// 순수하게 가이드라인 모양을 잡기 위한 가상의 지점이다.
[System.Serializable]
public class LongNoteWaypoint
{
    public float lane;
    public float row;
}

// 빌드 이후에도 교체 가능한 JSON 채보용 데이터 구조.
// ChartDataSO(에디터 저작용)와 별개로, StreamingAssets의 JSON 파일과 1:1로 대응한다.
[System.Serializable]
public class NoteInfo
{
    public float time;        // 타격 시간
    public int lane;          // 0~3 (왼쪽~오른쪽)
    public int row;           // 0~2 (아래~위)
    public string type;       // NoteType 이름 문자열 (예: "Hit", "Wall"). enum 순서가 바뀌어도 안전하도록 문자열로 저장
    public float duration;    // 0 = 즉발 노트, >0 = 롱노트 (duration초 뒤 endLane/endRow 위치에 끝 노트 생성)
    public int endLane = -1;  // 롱노트 끝 노트의 lane. -1이면 lane과 동일 (직선 롱노트)
    public int endRow = -1;   // 롱노트 끝 노트의 row. -1이면 row와 동일
    // 가이드라인이 시작→끝 사이에서 순서대로 지나가야 하는 지점들. 비어있으면 직선.
    // 이 지점들은 실제 노트로 스폰되지 않고 가이드라인 렌더링에만 쓰인다.
    public List<LongNoteWaypoint> midpoints = new List<LongNoteWaypoint>();
    public float[] direction = new float[3];

    [System.NonSerialized] public NoteType typeEnum;
}

[System.Serializable]
public class ChartData
{
    public string songName;
    public float bpm;
    public float offset;
    public float travelTime = 4.0f;
    public string audioFile;  // 같은 폴더 내 오디오 파일명 (예: "song.ogg")
    public string coverFile;  // 같은 폴더 내 커버 이미지 파일명 (예: "cover.png"), 없으면 빈 문자열
    public bool loop = false;       // true면 notes 패턴을 loopPeriod 주기로 무한 반복 (Sync Test 전용)
    public float loopPeriod = 0f;   // 한 패턴이 반복되는 주기(초). 오디오 클립 길이와 정확히 일치해야 끊김 없이 루프됨
    public List<NoteInfo> notes = new List<NoteInfo>();

    [System.NonSerialized] public AudioClip audioClip;
    [System.NonSerialized] public Sprite coverSprite;
}
