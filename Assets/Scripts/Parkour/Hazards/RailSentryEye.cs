using UnityEngine;

// A sentry eye that slides along a rail and sweeps its gaze left/right.
//
// DETECTION: Physics.Raycast (two kinds)
//   1. Laser pointer: one ray straight out of the lens. The visible magenta
//      beam ends where the ray hits. If the FIRST thing it hits is you = fail.
//   2. Vision cone: if your head or chest is inside the cone (range + angle),
//      a ray is cast from the eye to that point. If the first hit is the
//      player (not cover), the eye locks on. After lockOnTime of continuous
//      sight = fail. The eye/light turn from magenta to red while locking.
//   Cover blocks both rays: hide behind pillars / low walls (slide!).
public class RailSentryEye : ParkourHazard
{
    public override string CheckType => "Raycast";

    [Header("Rail")]
    public Vector3 railA;
    public Vector3 railB;
    public float railPeriod = 5f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Gaze")]
    [Tooltip("The rotating lens. Rays come out of its forward axis.")]
    public Transform head;
    public Vector3 lookDirection = Vector3.back;
    public float sweepAngle = 30f;
    public float sweepPeriod = 3.2f;

    [Header("Vision (Raycast)")]
    public float viewAngle = 40f;
    public float range = 14f;
    [Tooltip("Seconds of continuous sight before you are caught.")]
    public float lockOnTime = 0.35f;

    [Header("Visuals")]
    public Light coneLight;
    public LineRenderer beam;

    public float Lock01 => lockOnTime > 0f ? lockTimer / lockOnTime : 0f;

    private float elapsed;
    private float lockTimer;

    // Rays of this frame, for the debug view.
    private struct SightRay { public Vector3 from, to; public bool player; public bool used; }
    private readonly SightRay[] sightRays = new SightRay[3];

    void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        transform.position = Vector3.Lerp(railA, railB, PingPong01(elapsed + phaseOffset * railPeriod, railPeriod));

        if (head == null)
            head = transform;

        float yaw = Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(0.01f, sweepPeriod)) * sweepAngle;
        head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.LookRotation(lookDirection.normalized);

        bool beamHit = CastBeam();
        bool sees = CheckVisionCone();

        Touching = beamHit || sees;

        if (sees && CanDetect)
            lockTimer += dt;
        else
            lockTimer = Mathf.Max(0f, lockTimer - dt * 2f);

        if (beamHit)
            Hit("crossed the sentry's laser beam");
        else if (lockTimer >= lockOnTime)
        {
            lockTimer = 0f;
            Hit("were spotted by the sentry eye (line of sight)");
        }

        if (coneLight != null)
            coneLight.color = Color.Lerp(ParkourPalette.Sentry, ParkourPalette.Laser, Lock01);
    }

    // 1. The visible laser pointer.
    bool CastBeam()
    {
        Vector3 origin = head.position;
        Vector3 dir = head.forward;

        bool hitSomething = Physics.Raycast(origin, dir, out RaycastHit hit, range,
            ParkourLayers.SightMask, QueryTriggerInteraction.Ignore);

        Vector3 end = hitSomething ? hit.point : origin + dir * range;
        bool hitPlayer = hitSomething && ParkourLayers.IsPlayer(hit.collider);

        if (beam != null)
        {
            beam.SetPosition(0, origin);
            beam.SetPosition(1, end);
        }

        sightRays[0] = new SightRay { from = origin, to = end, player = hitPlayer, used = true };
        return hitPlayer;
    }

    // 2. Line of sight to the player's head and chest inside the cone.
    bool CheckVisionCone()
    {
        sightRays[1].used = sightRays[2].used = false;

        if (Game == null)
            return false;

        Vector3 eye = head.position;
        Vector3[] targets = { Game.PlayerHead, Game.PlayerChest };
        bool seen = false;

        for (int i = 0; i < targets.Length; i++)
        {
            Vector3 toTarget = targets[i] - eye;
            float distance = toTarget.magnitude;

            if (distance > range || distance < 0.01f)
                continue;

            if (Vector3.Angle(head.forward, toTarget) > viewAngle * 0.5f)
                continue;

            bool hitSomething = Physics.Raycast(eye, toTarget / distance, out RaycastHit hit, distance + 0.5f,
                ParkourLayers.SightMask, QueryTriggerInteraction.Ignore);

            bool seesPlayer = hitSomething && ParkourLayers.IsPlayer(hit.collider);

            sightRays[i + 1] = new SightRay
            {
                from = eye,
                to = hitSomething ? hit.point : targets[i],
                player = seesPlayer,
                used = true
            };

            if (seesPlayer)
                seen = true;
        }

        return seen;
    }

    public override void ResetHazard()
    {
        elapsed = 0f;
        lockTimer = 0f;
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * 0.9f;

    public override void DrawDebug()
    {
        Transform h = head != null ? head : transform;
        Vector3 o = h.position;
        Color cone = new Color(1f, 0.3f, 0.85f, 0.6f);

        // Cone edges + range ring.
        float half = viewAngle * 0.5f;
        Vector3[] dirs =
        {
            h.rotation * Quaternion.Euler(0f, -half, 0f) * Vector3.forward,
            h.rotation * Quaternion.Euler(0f, half, 0f) * Vector3.forward,
            h.rotation * Quaternion.Euler(-half, 0f, 0f) * Vector3.forward,
            h.rotation * Quaternion.Euler(half, 0f, 0f) * Vector3.forward,
        };

        foreach (Vector3 d in dirs)
            DebugShapes.Line(o, o + d * range, cone);

        float ringDist = range * Mathf.Cos(half * Mathf.Deg2Rad);
        float ringRadius = range * Mathf.Sin(half * Mathf.Deg2Rad);
        DebugShapes.Circle(o + h.forward * ringDist, h.forward, ringRadius, cone, 32);

        // This frame's rays.
        if (Application.isPlaying)
        {
            foreach (SightRay r in sightRays)
                if (r.used)
                    DebugShapes.Line(r.from, r.to, r.player ? DebugShapes.Hit : DebugShapes.Clear);
        }

        DebugShapes.Line(railA, railB, new Color(1f, 0.4f, 0.9f, 0.5f));
    }
}
