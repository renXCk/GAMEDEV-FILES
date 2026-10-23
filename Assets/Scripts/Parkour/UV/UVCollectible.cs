using UnityEngine;

// A hidden UV shard. It can only be picked up once the UV flashlight has
// revealed it (UVReveal.IsRevealed). Pickup uses Physics.CheckSphere against
// the Player layer. Collected shards stay collected through respawns.
[RequireComponent(typeof(UVReveal))]
public class UVCollectible : MonoBehaviour, IParkourDebug
{
    public float pickupRadius = 0.9f;
    public float spinSpeed = 120f;
    public float bobHeight = 0.1f;

    public bool Collected { get; private set; }

    private UVReveal reveal;
    private Vector3 basePosition;

    void Awake()
    {
        reveal = GetComponent<UVReveal>();
        basePosition = transform.position;
    }

    void OnEnable() => ParkourDebugDraw.Register(this);
    void OnDisable() => ParkourDebugDraw.Unregister(this);

    void Update()
    {
        if (Collected)
            return;

        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePosition + Vector3.up * Mathf.Sin(Time.time * 2.5f) * bobHeight;

        ParkourGameManager game = ParkourGameManager.Instance;

        // Can't grab what you can't see.
        if (game == null || !game.IsRunning || !reveal.IsRevealed)
            return;

        bool touching = Physics.CheckSphere(
            transform.position,
            pickupRadius,
            ParkourLayers.PlayerMask,
            QueryTriggerInteraction.Ignore);

        if (touching)
        {
            Collected = true;
            reveal.ForceHide();
            reveal.enabled = false;
            game.Collect(this);
        }
    }

    public void DrawDebug()
    {
        if (!Collected)
            DebugShapes.Sphere(transform.position, pickupRadius, new Color(0.6f, 0.36f, 1f, 0.6f));
    }

    void OnDrawGizmos()
    {
        DebugShapes.UseGizmos = true;
        DrawDebug();
    }
}
