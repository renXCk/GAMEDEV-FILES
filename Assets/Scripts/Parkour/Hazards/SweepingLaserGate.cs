using UnityEngine;

// A gate of laser lines that slides side-to-side across the corridor.
//
// DETECTION: Physics.CheckBox
//   Every frame a box the size of the gate is tested against the Player
//   layer. Touching it = fail. Time your run through the open side.
public class SweepingLaserGate : ParkourHazard
{
    public override string CheckType => "CheckBox";

    [Header("Movement")]
    public Vector3 pointA = new Vector3(-2.1f, 4.3f, 30.5f);
    public Vector3 pointB = new Vector3(2.1f, 4.3f, 30.5f);
    public float period = 2.8f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Detection volume (CheckBox)")]
    public Vector3 size = new Vector3(3.6f, 2.6f, 0.2f);

    private float elapsed;

    protected override void Start()
    {
        base.Start();
        elapsed = phaseOffset * period;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position = Vector3.Lerp(pointA, pointB, PingPong01(elapsed, period));

        Touching = Physics.CheckBox(
            transform.position,
            size * 0.5f,
            transform.rotation,
            ParkourLayers.PlayerMask,
            QueryTriggerInteraction.Ignore);

        if (Touching)
            Hit("ran into the sweeping laser gate");
    }

    public override void ResetHazard()
    {
        elapsed = phaseOffset * period;
        transform.position = Vector3.Lerp(pointA, pointB, PingPong01(elapsed, period));
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * (size.y * 0.5f + 0.6f);

    public override void DrawDebug()
    {
        DebugShapes.Box(transform.position, size * 0.5f, transform.rotation, DebugShapes.State(Touching));
        DebugShapes.Line(pointA, pointB, new Color(1f, 0.4f, 0.4f, 0.5f));
    }
}
