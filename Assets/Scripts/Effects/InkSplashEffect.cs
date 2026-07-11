using UnityEngine;

public class InkSplashEffect : MonoBehaviour
{
    [Header("Ink Splash Settings")]
    public float startSize = 0.1f;
    public float endSize = 0.3f;
    public int particleCount = 8;
    public float lifetime = 0.4f;

    private ParticleSystem ps;

    void Awake()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // URP에서는 기본 메테리얼이 Built-in용이라 분홍색으로 표시됨 — 명시적으로 URP 파티클 셰이더 지정
        var psr = GetComponent<ParticleSystemRenderer>();
        Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpParticleShader != null)
        {
            Material mat = new Material(urpParticleShader);
            mat.SetFloat("_Surface", 1f);           // Transparent
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", 5f);          // SrcAlpha
            mat.SetFloat("_DstBlend", 10f);         // OneMinusSrcAlpha
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            psr.material = mat;
        }

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize, endSize);
        main.startColor = Color.white;
        main.maxParticles = particleCount;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, particleCount)
        });
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaFade = new Gradient();
        alphaFade.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = alphaFade;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.2f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(1f, 0.5f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ps.Stop();
    }

    public void Play(Vector3 position, NoteType noteType, string judgment)
    {
        transform.position = position;

        Color color = GetColorByJudgment(judgment);

        var main = ps.main;
        main.startColor = color;

        ps.Play();
        Destroy(gameObject, lifetime + 0.5f);
    }

    Color GetColorByJudgment(string judgment)
    {
        switch (judgment)
        {
            case "PERFECT": return new Color(1.0f, 0.9f, 0.1f, 1f);
            case "GREAT":   return new Color(0.0f, 0.9f, 1.0f, 1f);
            case "GOOD":    return new Color(0.2f, 0.4f, 1.0f, 1f);
            default:        return new Color(0.05f, 0.05f, 0.1f, 1f);
        }
    }

    public static void Spawn(Vector3 position, NoteType noteType, string judgment)
    {
        GameObject obj = new GameObject("InkSplash");
        InkSplashEffect effect = obj.AddComponent<InkSplashEffect>();
        effect.Play(position, noteType, judgment);
    }
}
