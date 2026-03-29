using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
    public GameObject notePrefab;
    
    [Header("Note Models")]
    public GameObject slashModel;    // 기본 베기 모델 (회전하여 사용)
    public GameObject fanningModel;  // 부채질 모델
    public GameObject hitModel;      // 타격 모델 (접기 전용)
    public GameObject bossModel;     // 보스 모델 (LR)

    public float spawnInterval = 2.0f;
    private float timer;

    private Vector3[] possibleDirections = new Vector3[]
    {
        Vector3.up, Vector3.down, Vector3.left, Vector3.right,
        (Vector3.up + Vector3.left).normalized,
        (Vector3.up + Vector3.right).normalized,
        (Vector3.down + Vector3.left).normalized,
        (Vector3.down + Vector3.right).normalized
    };

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

        Note noteScript = noteObj.GetComponent<Note>();
        
        // 1. 노트 타입 결정
        float rand = Random.value;
        if (rand < 0.1f) // 10% 확률로 보스(LR) 등장
        {
            noteScript.type = NoteType.Boss;
            noteScript.hp = 3; // 왕복 3회 (편도 6회) 타격 필요
            noteObj.transform.localScale *= 2.5f; 
        }
        else if (rand < 0.4f) // 30% 확률로 부채질(Fanning)
        {
            noteScript.type = NoteType.Fanning;
        }
        else if (rand < 0.7f) // 30% 확률로 베기(Slashing)
        {
            noteScript.type = NoteType.Slashing;
        }
        else // 30% 확률로 타격(Hit - 접기 전용)
        {
            noteScript.type = NoteType.Hit;
        }
        
        // 2. 목표 방향 지정 (랜덤 설정 복구)
        noteScript.targetDirection = possibleDirections[Random.Range(0, possibleDirections.Length)];
        
        // 3. 모델 기반 시각화 생성
        CreateNoteVisuals(noteScript);
    }

    void CreateNoteVisuals(Note note)
    {
        foreach (Transform child in note.transform)
        {
            if (child.name.Contains("Visual")) Destroy(child.gameObject);
        }

        GameObject visualModel = null;

        switch (note.type)
        {
            case NoteType.Slashing:
                visualModel = slashModel;
                if (visualModel)
                {
                    GameObject obj = Instantiate(visualModel, note.transform);
                    obj.name = "Visual_Slashing";
                    note.slashIndicator = obj;
                }
                break;

            case NoteType.Fanning:
                visualModel = fanningModel;
                if (visualModel)
                {
                    GameObject obj = Instantiate(visualModel, note.transform);
                    obj.name = "Visual_Fanning";
                    note.fanIndicator = obj;
                }
                break;

            case NoteType.Hit:
                visualModel = hitModel;
                if (visualModel)
                {
                    GameObject obj = Instantiate(visualModel, note.transform);
                    obj.name = "Visual_Hit";
                    note.hitIndicator = obj;
                }
                break;

            case NoteType.Boss:
                visualModel = bossModel;
                if (visualModel)
                {
                    GameObject obj = Instantiate(visualModel, note.transform);
                    obj.name = "Visual_Boss";
                    note.bossIndicator = obj;
                }
                break;
        }

        note.SetupVisuals();
    }
}
