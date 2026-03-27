using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
    public GameObject notePrefab;
    public float spawnInterval = 2.0f;
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnNote();
            timer = 0;
        }
    }

    void SpawnNote()
    {
        GameObject noteObj;
        
        // 1. 노트 기본형 생성
        if (notePrefab != null)
        {
            noteObj = Instantiate(notePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            noteObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noteObj.transform.position = transform.position;
            noteObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            noteObj.AddComponent<Note>();
            
            if (noteObj.TryGetComponent<BoxCollider>(out var col)) col.isTrigger = true;
            Rigidbody rb = noteObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // 2. 노트 타입 랜덤 지정
        Note noteScript = noteObj.GetComponent<Note>();
        noteScript.type = (NoteType)Random.Range(0, 3);
        
        // 추가: 20% 확률로 연타 노트 생성
        if (Random.value < 0.2f)
        {
            noteScript.hp = 5; // 5번 부쳐야 함
            noteObj.transform.localScale *= 2.0f; // 크기를 2배로
            noteObj.GetComponent<Renderer>().material.color = Color.magenta; // 연타 노트는 보라색
        }
        
        // 3. 임시 시각화 도구 생성
        CreateTemporaryIndicator(noteScript);
    }

    void CreateTemporaryIndicator(Note note)
    {
        // 베기용 (빨간색 얇은 선)
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.transform.SetParent(note.transform);
        line.transform.localPosition = new Vector3(0, 0, -0.16f);
        line.transform.localScale = new Vector3(0.8f, 0.1f, 0.1f);
        line.GetComponent<Renderer>().material.color = Color.black;
        note.slashIndicator = line;

        // 부치기용 (파란색 화살표 모양 - 삼각형 대신 작은 큐브 조합)
        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrow.transform.SetParent(note.transform);
        arrow.transform.localPosition = new Vector3(0, 0, -0.16f);
        arrow.transform.localScale = new Vector3(0.3f, 0.3f, 0.1f);
        arrow.GetComponent<Renderer>().material.color = Color.cyan;
        note.fanIndicator = arrow;

        // 일반용 (하얀색 점)
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.transform.SetParent(note.transform);
        dot.transform.localPosition = new Vector3(0, 0, -0.16f);
        dot.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        dot.GetComponent<Renderer>().material.color = Color.grey;
        note.normalIndicator = dot;
    }
}
