using UnityEngine;

public class BombFuseAnimation : MonoBehaviour
{
    [Header("Fuse Settings")]
    public Transform fuseTip;          // the glowing tip of the fuse
    public Transform fuseMesh;         // the fuse rope mesh
    public float fuseLength = 0.25f;
    public float fuseBurnTime = 4f;

    [Header("Glow Flicker")]
    public Color glowColor = Color.yellow;
    public float flickerIntensity = 1.3f;
    public float flickerSpeed = 20f;

    [Header("Spark Particles")]
    public ParticleSystem sparks;

    [Header("Ticking")]
    public AudioSource tickSource;
    public float tickMinInterval = 0.15f;
    public float tickMaxInterval = 0.35f;

    private Material fuseMat;
    private Color baseEmission;
    private float burnTimer = 0f;
    private float nextTick = 0f;

    void Start()
    {
        // Setup fuse rope
        if (fuseMesh != null)
        {
            fuseMesh.localScale = new Vector3(1, 1, fuseLength);
        }

        // Setup glow
        if (fuseTip != null)
        {
            Renderer r = fuseTip.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                fuseMat = r.material;

                if (fuseMat.HasProperty("_EmissionColor"))
                {
                    baseEmission = fuseMat.GetColor("_EmissionColor");
                }
                else baseEmission = Color.black;

                fuseMat.EnableKeyword("_EMISSION");
            }
        }

        // Setup ticking audio
        if (tickSource != null)
        {
            nextTick = Random.Range(tickMinInterval, tickMaxInterval);
        }

        // Start spark particle system
        if (sparks != null)
            sparks.Play();
    }

    void Update()
    {
        burnTimer += Time.deltaTime;

        float burnT = Mathf.Clamp01(burnTimer / fuseBurnTime);

        // 🔥 1) Shrink the fuse rope
        if (fuseMesh != null)
        {
            float remaining = Mathf.Lerp(fuseLength, 0f, burnT);
            fuseMesh.localScale = new Vector3(1, 1, remaining);
        }

        // 🔥 2) Move fuse tip forward as fuse burns
        if (fuseTip != null)
        {
            fuseTip.localPosition = new Vector3(
                fuseTip.localPosition.x,
                fuseTip.localPosition.y,
                -Mathf.Lerp(0, fuseLength, burnT)
            );
        }

        // 🔥 3) Glow flicker
        if (fuseMat != null)
        {
            float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
            Color glow = glowColor * (1f + flicker * flickerIntensity);
            fuseMat.SetColor("_EmissionColor", baseEmission + glow);
        }

        // 🔥 4) Sparks stay active (optional intensity pulse)
        if (sparks != null)
        {
            var em = sparks.emission;
            em.rateOverTime = Mathf.Lerp(5, 12, burnT);
        }

        // 🔥 5) Ticking sound (faster as burn nears end)
        if (tickSource != null)
        {
            nextTick -= Time.deltaTime;
            if (nextTick <= 0f)
            {
                tickSource.Play();

                float interval = Mathf.Lerp(
                    tickMaxInterval,
                    tickMinInterval,
                    burnT
                );

                nextTick = interval;
            }
        }
    }
}
