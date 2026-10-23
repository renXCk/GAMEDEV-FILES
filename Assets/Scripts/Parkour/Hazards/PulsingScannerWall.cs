using UnityEngine;

// A full-width wall of red scanner light that drifts back and forth along
// the corridor and pulses ON / OFF. It blinks just before switching on.
// Run through while it is dark.
//
// DETECTION: Physics.CheckBox (only while the field is ON)
public class PulsingScannerWall : ParkourHazard
{
    public override string CheckType => "CheckBox";

    [Header("Movement")]
    public Vector3 pointA;
    public Vector3 pointB;
    public float movePeriod = 5f;

    [Header("Pulse")]
    public float pulsePeriod = 2.4f;
    [Range(0.1f, 0.9f)] public float onFraction = 0.55f;
    [Tooltip("Blinks this long before switching on.")]
    public float warnTime = 0.4f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Detection volume (CheckBox)")]
    public Vector3 size = new Vector3(7.8f, 5f, 0.3f);

    [Header("Visuals")]
    [Tooltip("Renderers that make up the glowing field (hidden while OFF).")]
    public Renderer[] fieldRenderers;
    public Light fieldLight;

    public bool IsActive { get; private set; }

    private float elapsed;
    private bool visible = true;

    protected override void Start()
    {
        base.Start();
        elapsed = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position = Vector3.Lerp(pointA, pointB, PingPong01(elapsed, movePeriod));

        float t = Mathf.Repeat(elapsed + phaseOffset * pulsePeriod, pulsePeriod);
        float onTime = pulsePeriod * onFraction;

        IsActive = t < onTime;
        bool warning = !IsActive && t > pulsePeriod - warnTime;
        SetVisible(IsActive || (warning && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f));

        Touching = IsActive && Physics.CheckBox(
            transform.position,
            size * 0.5f,
            transform.rotation,
            ParkourLayers.PlayerMask,
            QueryTriggerInteraction.Ignore);

        if (Touching)
            Hit("was caught by the pulsing scanner wall");
    }

    void SetVisible(bool on)
    {
        if (on == visible)
            return;

        visible = on;

        if (fieldRenderers != null)
            foreach (Renderer r in fieldRenderers)
                if (r != null) r.enabled = on;

        if (fieldLight != null)
            fieldLight.enabled = on;
    }

    public override void ResetHazard()
    {
        elapsed = 0f;
    }

    protected override Vector3 LabelPosition => transform.position + Vector3.up * (size.y * 0.5f + 0.6f);

    public override void DrawDebug()
    {
        Color c = IsActive || !Application.isPlaying
            ? DebugShapes.State(Touching)
            : new Color(0.5f, 0.5f, 0.5f, 0.4f);

        DebugShapes.Box(transform.position, size * 0.5f, transform.rotation, c);
        DebugShapes.Line(pointA, pointB, new Color(1f, 0.4f, 0.4f, 0.5f));
    }
}
