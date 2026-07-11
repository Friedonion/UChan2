using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VRPointer : MonoBehaviour
{
    public float rayLength   = 5f;
    public Color normalColor = new Color(0.5f, 0.5f, 1f, 0.8f);

    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth    = 0.005f;
        line.endWidth      = 0.002f;
        line.useWorldSpace = true;
        line.material      = new Material(Shader.Find("Sprites/Default"));
        line.startColor    = normalColor;
        line.endColor      = normalColor;
    }

    void Update()
    {
        line.SetPosition(0, transform.position);
        line.SetPosition(1, transform.position + transform.forward * rayLength);
    }
}
