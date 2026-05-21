using UnityEngine;
using System.Collections.Generic;

public class NotePoolManager : MonoBehaviour
{
    public static NotePoolManager Instance { get; private set; }

    public GameObject notePrefab;
    public int initialPoolSize = 20;

    private Queue<GameObject> pool = new Queue<GameObject>();

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

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(notePrefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetNote(Vector3 position, Quaternion rotation)
    {
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
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
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        return obj;
    }

    public void ReturnNote(GameObject note)
    {
        note.SetActive(false);
        if (!pool.Contains(note))
        {
            pool.Enqueue(note);
        }
    }
}
