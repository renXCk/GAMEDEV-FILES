using UnityEngine;

// A laser orb bobbing up and down over a gap. Jump while it is low.
//
// DETECTION: Physics.CheckSphere
public class BobbingOrb : ParkourHazard
{
    public override string CheckType => "CheckSphere";

    public float radius = 0.8f;
    public float amplitude = 2f;
    public float period = 2.2f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    private Vector3 basePosition;
    private float elapsed;

    void Awake()
    {
        basePosition = transform.position;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        float s = Mathf.Sin((elapsed / period + phaseOffset) * Mathf.PI * 2f);
        transform.position = basePosition + Vector3.up * (s * amplitude);

        Touching = Physics.CheckSphere(
            transform.position,
            radius,
            ParkourLayers.PlayerMask,
            QueryTriggerInteraction.Ignore);

        if (Touching)
            Hit("flew into a bobbing laser orb");
    }

    public override void ResetHazard()
    {
        elapsed = 0f;
        transform.position = basePosition;
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * (radius + 0.6f);

    public override void DrawDebug()
    {
        DebugShapes.Sphere(transform.position, radius, DebugShapes.State(Touching));

        Vector3 b = Application.isPlaying ? basePosition : transform.position;
        DebugShapes.Line(b + Vector3.up * amplitude, b - Vector3.up * amplitude, new Color(1f, 0.4f, 0.4f, 0.35f));
    }
}
