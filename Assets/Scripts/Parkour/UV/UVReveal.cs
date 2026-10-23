using UnityEngine;

// Makes an object INVISIBLE unless the UV flashlight is on AND the object is
// inside the light cone with nothing blocking the light.
//
// All renderers and lights under this object are switched off by default.
// Every frame we ask UVFlashlight.Illuminates() about a few sample points on
// the object. If any of them is lit, the object is revealed.
//
// Used by hidden tripwires (LaserBeam), UV route markers and UV shards.
// Revealed objects have no colliders, so they never block rays or the player.
public class UVReveal : MonoBehaviour
{
    [Tooltip("Keeps the object visible briefly after the light leaves it, to avoid flicker.")]
    public float lingerTime = 0.15f;

    [Tooltip("Length of the object along its local X axis. Long objects " +
             "(tripwires) are sampled along this length so lighting ANY part " +
             "of them reveals them. 0 = just the centre.")]
    public float revealLength = 0f;

    public bool IsRevealed { get; private set; }

    private Renderer[] renderers;
    private Light[] lights;
    private float timer;

    private static readonly float[] Samples = { 0f, -0.45f, 0.45f, -0.9f, 0.9f };

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        lights = GetComponentsInChildren<Light>(true);
        Apply(false);
    }

    void OnDisable()
    {
        Apply(false);
    }

    void Update()
    {
        bool lit = IsLit();

        if (lit)
            timer = lingerTime;
        else
            timer -= Time.deltaTime;

        bool show = lit || timer > 0f;

        if (show != IsRevealed)
            Apply(show);
    }

    bool IsLit()
    {
        UVFlashlight light = UVFlashlight.Active;

        if (light == null || !light.IsOn)
            return false;

        Vector3 centre = transform.position;

        if (revealLength <= 0f)
            return light.Illuminates(centre);

        Vector3 halfAxis = transform.right * (revealLength * 0.5f);

        foreach (float t in Samples)
        {
            if (light.Illuminates(centre + halfAxis * t))
                return true;
        }

        return false;
    }

    void Apply(bool visible)
    {
        IsRevealed = visible;

        if (renderers != null)
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = visible;

        if (lights != null)
            foreach (Light l in lights)
                if (l != null) l.enabled = visible;
    }

    public void ForceHide()
    {
        timer = 0f;
        Apply(false);
    }

    void OnDrawGizmos()
    {
        // Scene-view hint so hidden things are easy to find while editing.
        Gizmos.color = new Color(0.6f, 0.36f, 1f, 0.7f);

        if (revealLength > 0f)
        {
            Vector3 half = transform.right * (revealLength * 0.5f);
            Gizmos.DrawLine(transform.position - half, transform.position + half);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
}
