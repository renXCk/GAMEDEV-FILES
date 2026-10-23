using UnityEngine;

// Laser arms spinning around a hub, around the object's local UP axis.
//   Upright hub   -> arms sweep flat over the floor (jump over them)
//   Hub tipped 90 -> a "windmill" across the corridor (run through the gap)
//
// DETECTION: Physics.CheckCapsule
//   Each arm is a line segment from the hub to the arm tip. A capsule around
//   that segment is tested against the Player layer every frame.
public class RotatingLaserArm : ParkourHazard
{
    public override string CheckType => "CheckCapsule";

    public float degreesPerSecond = 100f;
    public int armCount = 2;
    public float armStart = 0.3f;
    public float armLength = 3.9f;
    public float beamRadius = 0.12f;

    private Quaternion startRotation;

    void Awake()
    {
        startRotation = transform.rotation;
    }

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);

        Touching = false;

        for (int i = 0; i < armCount; i++)
        {
            GetArm(i, out Vector3 a, out Vector3 b);

            if (Physics.CheckCapsule(a, b, beamRadius, ParkourLayers.PlayerMask, QueryTriggerInteraction.Ignore))
            {
                Touching = true;
                break;
            }
        }

        if (Touching)
            Hit("touched the rotating laser arm");
    }

    // Arm i points along local forward, rotated around local up.
    public void GetArm(int index, out Vector3 a, out Vector3 b)
    {
        Vector3 dir = transform.rotation *
                      Quaternion.Euler(0f, 360f / Mathf.Max(1, armCount) * index, 0f) *
                      Vector3.forward;

        a = transform.position + dir * armStart;
        b = transform.position + dir * armLength;
    }

    public override void ResetHazard()
    {
        transform.rotation = startRotation;
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * 1.4f;

    public override void DrawDebug()
    {
        Color c = DebugShapes.State(Touching);

        for (int i = 0; i < armCount; i++)
        {
            GetArm(i, out Vector3 a, out Vector3 b);
            DebugShapes.Capsule(a, b, beamRadius, c);
        }

        DebugShapes.Circle(transform.position, transform.up, armLength, new Color(1f, 0.5f, 0.2f, 0.35f), 40);
    }
}
