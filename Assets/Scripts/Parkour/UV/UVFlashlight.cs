using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's UV flashlight.
//
//   F   toggle
//
// The course is lit normally; this light is NOT for seeing the route. It
// reveals UV-only things: hidden tripwires, route markers and UV shards.
// Illuminates(point) is the "is this inside my light cone?" test that every
// UVReveal asks each frame:
//   1. Light is on
//   2. point is within the light's range
//   3. angle between the light's forward and the point <= spotAngle / 2
//   4. Physics.Raycast from the light to the point hits no geometry in between
[DefaultExecutionOrder(-50)] // clear last frame's debug rays before UVReveals run
public class UVFlashlight : MonoBehaviour, IParkourDebug
{
    public static UVFlashlight Active { get; private set; }

    public Light spotLight;
    public Key toggleKey = Key.F;
    public bool startOn = false;

    [Header("Battery")]
    public float maxBattery = 100f;
    public float drainPerSecond = 9f;
    public float rechargePerSecond = 14f;
    [Tooltip("Can't switch back on below this fraction after running flat.")]
    public float minBatteryToTurnOn = 0.1f;
    [Tooltip("Below this fraction the light starts to flicker.")]
    public float lowBatteryFraction = 0.2f;

    public bool IsOn { get; private set; }
    public float Battery { get; private set; }
    public float Battery01 => maxBattery > 0f ? Battery / maxBattery : 0f;

    private float baseIntensity;

    private struct RevealRay { public Vector3 from, to; public bool blocked; }
    private readonly List<RevealRay> rays = new List<RevealRay>();

    void Awake()
    {
        Active = this;

        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);

        if (spotLight != null)
            baseIntensity = spotLight.intensity;

        Battery = maxBattery;
        SetOn(startOn);
    }

    void OnEnable() => ParkourDebugDraw.Register(this);
    void OnDisable() => ParkourDebugDraw.Unregister(this);

    void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    void Update()
    {
        rays.Clear();

        Keyboard kb = Keyboard.current;
        ParkourGameManager game = ParkourGameManager.Instance;
        bool canToggle = game == null || game.IsRunning;

        if (canToggle && kb != null && kb[toggleKey].wasPressedThisFrame)
            SetOn(!IsOn);

        float dt = Time.deltaTime;

        if (IsOn)
        {
            Battery = Mathf.Max(0f, Battery - drainPerSecond * dt);

            if (Battery <= 0f)
                SetOn(false);
        }
        else
        {
            Battery = Mathf.Min(maxBattery, Battery + rechargePerSecond * dt);
        }

        // Low battery flicker.
        if (IsOn && spotLight != null)
        {
            if (Battery01 < lowBatteryFraction)
            {
                float n = Mathf.PerlinNoise(Time.time * 12f, 0f);
                spotLight.intensity = baseIntensity * (n > 0.35f ? 1f : 0.25f);
            }
            else
            {
                spotLight.intensity = baseIntensity;
            }
        }
    }

    public void SetOn(bool on)
    {
        if (on && Battery01 < minBatteryToTurnOn)
            on = false;

        IsOn = on;

        if (spotLight != null)
            spotLight.enabled = on;
    }

    // Is this world point lit by the UV light right now?
    public bool Illuminates(Vector3 point)
    {
        if (!IsOn || spotLight == null)
            return false;

        Vector3 origin = spotLight.transform.position;
        Vector3 toPoint = point - origin;
        float distance = toPoint.magnitude;

        // 2. Range
        if (distance > spotLight.range)
            return false;

        if (distance < 0.001f)
            return true;

        // 3. Cone angle
        if (Vector3.Angle(spotLight.transform.forward, toPoint) > spotLight.spotAngle * 0.5f)
            return false;

        // 4. Occlusion: Raycast against level geometry (not the player).
        bool blocked = Physics.Raycast(
            origin,
            toPoint / distance,
            distance - 0.05f,
            ParkourLayers.ObstacleMask,
            QueryTriggerInteraction.Ignore);

        rays.Add(new RevealRay { from = origin, to = point, blocked = blocked });
        return !blocked;
    }

    public void DrawDebug()
    {
        if (!IsOn || spotLight == null)
            return;

        Transform t = spotLight.transform;
        float half = spotLight.spotAngle * 0.5f;
        float range = spotLight.range;

        float ringDist = range * Mathf.Cos(half * Mathf.Deg2Rad);
        float ringRadius = range * Mathf.Sin(half * Mathf.Deg2Rad);
        DebugShapes.Circle(t.position + t.forward * ringDist, t.forward, ringRadius, DebugShapes.Cone, 32);

        foreach (RevealRay r in rays)
            DebugShapes.Line(r.from, r.to, r.blocked ? DebugShapes.Blocked : DebugShapes.Ray);
    }
}
