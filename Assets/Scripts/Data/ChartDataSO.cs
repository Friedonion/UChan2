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
}

[CreateAssetMenu(fileName = "NewChart", menuName = "TraceOfWind/ChartData")]
public class ChartDataSO : ScriptableObject
{
    public string songName;
    public AudioClip audioClip; // 🎵 음원 에셋 직접 참조 추가
    public float bpm;
    public float offset;
    public float travelTime = 4.0f;
    public List<NoteData> notes = new List<NoteData>();
}
