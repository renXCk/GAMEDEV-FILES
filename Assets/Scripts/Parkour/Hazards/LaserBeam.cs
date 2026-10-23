using UnityEngine;

// A single laser beam / tripwire along the object's local X axis.
// Optionally slides back and forth (moveOffset) so it is a moving hazard.
//
// If the object also has a UVReveal, the beam is INVISIBLE unless the UV
// flashlight is on it - but it is ALWAYS armed, seen or not.
//
// DETECTION: Physics.CheckCapsule along the beam.
public class LaserBeam : ParkourHazard
{
    public override string CheckType => "CheckCapsule";

    public float length = 7.6f;
    public float radius = 0.06f;

    [Header("Optional movement")]
    public Vector3 moveOffset = Vector3.zero;
    public float movePeriod = 3f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    private Vector3 basePosition;
    private float elapsed;
    private bool hiddenUV;

    void Awake()
    {
        basePosition = transform.position;
        hiddenUV = GetComponent<UVReveal>() != null;
    }

    void Update()
    {
        if (moveOffset != Vector3.zero)
        {
            elapsed += Time.deltaTime;
            float t = PingPong01(elapsed + phaseOffset * movePeriod, movePeriod) * 2f - 1f;
            transform.position = basePosition + moveOffset * t;
        }

        GetEnds(out Vector3 a, out Vector3 b);

        Touching = Physics.CheckCapsule(a, b, radius, ParkourLayers.PlayerMask, QueryTriggerInteraction.Ignore);

        if (Touching)
            Hit(hiddenUV ? "tripped a hidden UV tripwire" : "touched a laser beam");
    }

    public void GetEnds(out Vector3 a, out Vector3 b)
    {
        Vector3 half = transform.right * (length * 0.5f);
        a = transform.position - half;
        b = transform.position + half;
    }

    public override void ResetHazard()
    {
        elapsed = 0f;
        transform.position = basePosition;
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * 0.6f;

    public override void DrawDebug()
    {
        GetEnds(out Vector3 a, out Vector3 b);
        DebugShapes.Capsule(a, b, radius, DebugShapes.State(Touching));

        if (moveOffset != Vector3.zero)
        {
            Vector3 c = Application.isPlaying ? basePosition : transform.position;
            DebugShapes.Line(c - moveOffset, c + moveOffset, new Color(1f, 0.4f, 0.4f, 0.35f));
        }
    }
}
