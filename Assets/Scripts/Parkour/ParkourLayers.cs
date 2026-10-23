using UnityEngine;

// Central place for the layer masks every detection check uses.
//
// The Player lives on its own "Player" layer (the course builder creates it
// if needed). That lets every Physics query ask a precise question:
//   - CheckSphere / CheckBox / CheckCapsule use PlayerMask
//       -> "is the player's collider inside this hazard volume?"
//   - Sentry raycasts use SightMask
//       -> "what is the FIRST thing this ray hits: cover, or the player?"
//   - Movement probes and the UV light occlusion ray use ObstacleMask
//       -> "is there level geometry here?"
public static class ParkourLayers
{
    public const string PlayerLayerName = "Player";

    public static int PlayerLayer => LayerMask.NameToLayer(PlayerLayerName);

    public static int PlayerMask
    {
        get
        {
            int layer = PlayerLayer;
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    // Everything solid EXCEPT the player.
    public static int ObstacleMask =>
        Physics.DefaultRaycastLayers & ~PlayerMask;

    // Obstacles AND the player. A sight ray reports whichever it hits first.
    public static int SightMask =>
        Physics.DefaultRaycastLayers | PlayerMask;

    public static bool IsPlayer(Collider c)
    {
        return c != null && c.gameObject.layer == PlayerLayer;
    }
}
