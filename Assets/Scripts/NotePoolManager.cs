using UnityEngine;
using System.Collections.Generic;

public class NotePoolManager : MonoBehaviour
{
    public static NotePoolManager Instance { get; private set; }

    public GameObject notePrefab;
    public int initialPoolSize = 20;
    public int smallPoolSize = 5;

    private Dictionary<NoteType, Queue<GameObject>> pools = new Dictionary<NoteType, Queue<GameObject>>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePool();
    }

    void InitializePool()
    {
        // 씬에서 프리팹 참조가 누락되었을 때를 대비한 자동 연동
        if (notePrefab == null)
        {
            var spawner = FindObjectOfType<NoteSpawner>();
            if (spawner != null)
            {
                notePrefab = spawner.notePrefab;
            }
        }

        if (notePrefab == null)
        {
            Debug.LogWarning("NotePoolManager: notePrefab is null and could not be resolved automatically.");
            return;
        }

        foreach (NoteType type in System.Enum.GetValues(typeof(NoteType)))
        {
            pools[type] = new Queue<GameObject>();

            int poolSize = (type == NoteType.Wall || type == NoteType.Boss) ? smallPoolSize : initialPoolSize;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(notePrefab);
                obj.name = $"Note_{type}_{i}";
                obj.SetActive(false);
                pools[type].Enqueue(obj);
            }
        }
    }

    public GameObject GetNote(NoteType type, Vector3 position, Quaternion rotation)
    {
        GameObject obj;

        if (!pools.ContainsKey(type))
        {
            pools[type] = new Queue<GameObject>();
        }

        if (pools[type].Count > 0)
        {
            obj = pools[type].Dequeue();
        }
        else
        {
            if (notePrefab == null)
            {
                var spawner = FindObjectOfType<NoteSpawner>();
                if (spawner != null)
                {
                    notePrefab = spawner.notePrefab;
                }
            }
            obj = Instantiate(notePrefab);
            obj.name = $"Note_{type}_Dynamic";
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        return obj;
    }

    public void ReturnNote(GameObject note, NoteType type)
    {
        note.SetActive(false);

        if (!pools.ContainsKey(type))
        {
            pools[type] = new Queue<GameObject>();
        }

        if (!pools[type].Contains(note))
        {
            pools[type].Enqueue(note);
        }
    }
}
