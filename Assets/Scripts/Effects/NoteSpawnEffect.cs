using UnityEngine;

public class NoteSpawnEffect : MonoBehaviour
{
    [Header("Particle Systems")]
    public ParticleSystem inkPS;
    public ParticleSystem neonPS;

    [Header("Effect Settings")]
    public float lifetime = 1.5f;

    // 프리팹 인스턴스화 후 초기화 및 재생
    public void Play(NoteType noteType)
    {
        // 강제 하얀색 네온 설정
        Color whiteNeonColor = Color.white * 2.5f;
        
        if (neonPS != null)
        {
            neonPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var neonMain = neonPS.main;
            neonMain.startColor = whiteNeonColor;
            
            var neonTrails = neonPS.trails;
            if (neonTrails.enabled)
            {
                Gradient trailGradient = new Gradient();
                trailGradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(whiteNeonColor, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                neonTrails.colorOverTrail = trailGradient;
            }

            neonPS.Play();
        }

        if (inkPS != null)
        {
            inkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var inkMain = inkPS.main;
            inkMain.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            inkPS.Play();
        }

        Destroy(gameObject, lifetime);
    }

    private Color GetNeonColorByType(NoteType type)
    {
        // HDR 강도 적용
        float intensity = 2.5f; 
        return Color.white * intensity;
    }
}
