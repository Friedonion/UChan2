using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SwordTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    public Transform basePoint;
    public Transform tipPoint;
    public float trailTime = 0.3f;
    public int maxSegments = 60;
    public float minDistance = 0.02f;

    [Header("Visuals")]
    public Color trailColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
    
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;
    private Color[] colors;

    struct TrailSegment
    {
        public Vector3 basePos;
        public Vector3 tipPos;
        public float spawnTime;
        public Color color;
    }

    private List<TrailSegment> segments = new List<TrailSegment>();
    private bool isEmitting = false;

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "SwordTrailMesh";
        GetComponent<MeshFilter>().mesh = mesh;
        
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null) {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpShader != null) {
                Material mat = new Material(urpShader);
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_SrcBlend", 5f); // SrcAlpha
                mat.SetFloat("_DstBlend", 1f); // One (Additive)
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.color = Color.white; // 정점 컬러(Vertex Color)가 그대로 나오도록 흰색 베이스 사용
                mr.material = mat;
            }
        }
    }

    public void SetEmitting(bool emit)
    {
        if (emit && !isEmitting) {
            segments.Clear();
            mesh.Clear();
        }
        isEmitting = emit;
    }

    public void ClearTrail()
    {
        segments.Clear();
        mesh.Clear();
    }

    void LateUpdate()
    {
        if (basePoint == null || tipPoint == null) return;

        // Add new segment
        if (isEmitting)
        {
            if (segments.Count == 0 || Vector3.Distance(segments[segments.Count - 1].basePos, basePoint.position) > minDistance || Vector3.Distance(segments[segments.Count - 1].tipPos, tipPoint.position) > minDistance)
            {
                segments.Add(new TrailSegment()
                {
                    basePos = basePoint.position,
                    tipPos = tipPoint.position,
                    spawnTime = Time.time,
                    color = this.trailColor
                });
            }
        }

        // Remove old segments
        while (segments.Count > 0 && Time.time - segments[0].spawnTime > trailTime)
        {
            segments.RemoveAt(0);
        }

        // Keep under max
        while (segments.Count > maxSegments) {
            segments.RemoveAt(0);
        }

        UpdateMesh();
    }

    void UpdateMesh()
    {
        if (segments.Count < 2)
        {
            mesh.Clear();
            return;
        }

        int segmentCount = segments.Count;
        if (vertices == null || vertices.Length != segmentCount * 2)
        {
            vertices = new Vector3[segmentCount * 2];
            uvs = new Vector2[segmentCount * 2];
            colors = new Color[segmentCount * 2];
            triangles = new int[(segmentCount - 1) * 6];
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float t = (Time.time - segments[i].spawnTime) / trailTime; // 0 (new) to 1 (old)
            float alpha = 1f - t;

            vertices[i * 2] = transform.InverseTransformPoint(segments[i].basePos);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(segments[i].tipPos);

            float v = (float)i / (segmentCount - 1);
            uvs[i * 2] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);

            Color segColor = segments[i].color;
            colors[i * 2] = new Color(segColor.r, segColor.g, segColor.b, segColor.a * alpha);
            colors[i * 2 + 1] = new Color(segColor.r, segColor.g, segColor.b, segColor.a * alpha);
        }

        for (int i = 0; i < segmentCount - 1; i++)
        {
            int baseIndex = i * 2;
            int triIndex = i * 6;

            triangles[triIndex] = baseIndex;
            triangles[triIndex + 1] = baseIndex + 1;
            triangles[triIndex + 2] = baseIndex + 2;

            triangles[triIndex + 3] = baseIndex + 2;
            triangles[triIndex + 4] = baseIndex + 1;
            triangles[triIndex + 5] = baseIndex + 3;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
}
