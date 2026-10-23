using UnityEngine;

// A glowing wrecking orb on a cable, swinging across the corridor like a
// pendulum. This object is the PIVOT; the ball hangs 'length' below it.
//
// DETECTION: Physics.CheckSphere around the ball.
public class SwingingHazard : ParkourHazard
{
    public override string CheckType => "CheckSphere";

    public float length = 5.8f;
    public float radius = 0.75f;
    public float maxAngle = 40f;
    public float period = 2.6f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    private Quaternion baseRotation;
    private float elapsed;

    public Vector3 BallPosition => transform.position + transform.rotation * Vector3.down * length;

    void Awake()
    {
        baseRotation = transform.localRotation;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        float angle = maxAngle * Mathf.Sin((elapsed / period + phaseOffset) * Mathf.PI * 2f);
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);

        Touching = Physics.CheckSphere(
            BallPosition,
            radius,
            ParkourLayers.PlayerMask,
            QueryTriggerInteraction.Ignore);

        if (Touching)
            Hit("got smashed by the swinging orb");
    }

    public override void ResetHazard()
    {
        elapsed = 0f;
        transform.localRotation = baseRotation;
    }

    protected override Vector3 LabelPosition => BallPosition + Vector3.up * (radius + 0.7f);

    public override void DrawDebug()
    {
        DebugShapes.Line(transform.position, BallPosition, new Color(1f, 1f, 1f, 0.4f));
        DebugShapes.Sphere(BallPosition, radius, DebugShapes.State(Touching));
    }
}
