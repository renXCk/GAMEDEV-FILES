using UnityEngine;

// Checkpoint gate. When the player's collider is inside the gate volume
// (Physics.CheckBox against the Player layer) it becomes the respawn point.
// The transform position is the spawn point (feet), facing +Z of this object.
public class Checkpoint : MonoBehaviour, IParkourDebug
{
    public int index = 1;
    public Vector3 size = new Vector3(7.6f, 4f, 1.5f);

    [Tooltip("Glow pieces that turn green once reached.")]
    public Renderer[] glowRenderers;
    public Material reachedMaterial;

    public bool Reached { get; private set; }

    public Vector3 SpawnPosition => transform.position + Vector3.up * 0.05f;
    public float SpawnYaw => transform.eulerAngles.y;

    Vector3 Centre => transform.position + Vector3.up * (size.y * 0.5f);

    void OnEnable() => ParkourDebugDraw.Register(this);
    void OnDisable() => ParkourDebugDraw.Unregister(this);

    void Update()
    {
        ParkourGameManager game = ParkourGameManager.Instance;

        if (Reached || game == null || !game.IsRunning)
            return;

        bool inside = Physics.CheckBox(
            Centre, size * 0.5f, transform.rotation,
            ParkourLayers.PlayerMask, QueryTriggerInteraction.Ignore);

        if (inside)
        {
            Reached = true;

            if (reachedMaterial != null && glowRenderers != null)
                foreach (Renderer r in glowRenderers)
                    if (r != null) r.sharedMaterial = reachedMaterial;

            game.ReachCheckpoint(this);
        }
    }

    public void DrawDebug()
    {
        DebugShapes.Box(Centre, size * 0.5f, transform.rotation,
            Reached ? new Color(0.3f, 1f, 0.6f, 0.5f) : new Color(0f, 0.9f, 1f, 0.6f));
    }

    void OnDrawGizmos()
    {
        DebugShapes.UseGizmos = true;
        DrawDebug();
    }
}
