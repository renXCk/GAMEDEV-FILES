using TMPro;
using UnityEngine;

// Base class for everything that can fail the run.
//
// Each subclass does exactly ONE kind of Physics query against the Player
// layer, and names it in CheckType. When it hits, Hit() tells the game
// manager, which shows e.g. "CheckCapsule - Rotating Laser Arm" in the
// detection feed and respawns you at the last checkpoint.
//
// With debug on (G) every hazard draws its volume/rays and shows a floating
// label with its Physics check, for the demo video.
public abstract class ParkourHazard : MonoBehaviour, IParkourDebug
{
    public string hazardName = "Hazard";

    // "CheckBox", "CheckSphere", "CheckCapsule" or "Raycast".
    public abstract string CheckType { get; }

    protected ParkourGameManager Game => ParkourGameManager.Instance;

    protected bool CanDetect =>
        Game != null &&
        Game.CanBeHit &&
        ParkourLayers.PlayerMask != 0;

    // True on frames where the check found the player (debug colour).
    protected bool Touching { get; set; }

    private TextMeshPro label;

    protected virtual void OnEnable() => ParkourDebugDraw.Register(this);
    protected virtual void OnDisable() => ParkourDebugDraw.Unregister(this);

    protected virtual void Start()
    {
        CreateLabel();
    }

    protected void Hit(string detail)
    {
        Touching = true;

        if (CanDetect)
            Game.Fail(this, CheckType, detail);
    }

    // Called by the game manager on a full restart.
    public virtual void ResetHazard() { }

    // Smooth 0..1..0 ping-pong used by moving hazards.
    protected static float PingPong01(float time, float period)
    {
        return 0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / Mathf.Max(0.01f, period));
    }

    // ------------------------------------------------------------------
    // DEBUG
    // ------------------------------------------------------------------

    // Shapes to draw (runtime GL + Scene view gizmos).
    public abstract void DrawDebug();

    // Where the floating "CheckBox" label sits.
    protected virtual Vector3 LabelPosition => transform.position + Vector3.up * 1.2f;

    void CreateLabel()
    {
        if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            return; // TMP Essentials not imported yet

        GameObject go = new GameObject("DebugLabel");
        go.transform.SetParent(transform, false);

        label = go.AddComponent<TextMeshPro>();
        label.text = "<b>" + CheckType + "</b>\n<size=70%>" + hazardName + "</size>";
        label.fontSize = 3f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.95f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(8f, 2f);
        label.gameObject.SetActive(false);
    }

    protected virtual void LateUpdate()
    {
        if (label == null)
            return;

        bool show = ParkourDebugDraw.Enabled;

        if (label.gameObject.activeSelf != show)
            label.gameObject.SetActive(show);

        Camera cam = Camera.main;

        if (show && cam != null)
        {
            label.transform.position = LabelPosition;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
            label.color = Touching ? DebugShapes.Hit : new Color(1f, 0.95f, 0.5f);
        }
    }

    void OnDrawGizmos()
    {
        DebugShapes.UseGizmos = true;
        DrawDebug();
    }
}
