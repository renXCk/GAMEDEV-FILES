using UnityEngine;

// Finish gate: Physics.CheckBox against the Player layer ends the run.
public class FinishLine : MonoBehaviour, IParkourDebug
{
    public Vector3 size = new Vector3(7.6f, 4f, 1.5f);

    Vector3 Centre => transform.position + Vector3.up * (size.y * 0.5f);

    void OnEnable() => ParkourDebugDraw.Register(this);
    void OnDisable() => ParkourDebugDraw.Unregister(this);

    void Update()
    {
        ParkourGameManager game = ParkourGameManager.Instance;

        if (game == null || !game.IsRunning)
            return;

        if (Physics.CheckBox(Centre, size * 0.5f, transform.rotation,
                ParkourLayers.PlayerMask, QueryTriggerInteraction.Ignore))
            game.Finish();
    }

    public void DrawDebug()
    {
        DebugShapes.Box(Centre, size * 0.5f, transform.rotation, new Color(0.3f, 1f, 0.55f, 0.7f));
    }

    void OnDrawGizmos()
    {
        DebugShapes.UseGizmos = true;
        DrawDebug();
    }
}
