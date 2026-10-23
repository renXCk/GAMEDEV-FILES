using UnityEngine;

// One palette for the whole course so the level, hazards and HUD all match.
//   Cyan    = the route (edge strips, checkpoints, HUD accents)
//   Orange  = you can wall-run on this
//   Red     = laser hazards, Magenta = sentry eyes
//   Violet  = UV-only objects (only visible under the UV flashlight)
public static class ParkourPalette
{
    public static readonly Color Surface = Hex("#D8DCE3");
    public static readonly Color SurfaceAlt = Hex("#B9BFC9");
    public static readonly Color Trim = Hex("#1A1D24");
    public static readonly Color Route = Hex("#00E5FF");
    public static readonly Color WallRun = Hex("#FF8A00");
    public static readonly Color Laser = Hex("#FF2040");
    public static readonly Color Sentry = Hex("#FF2BD6");
    public static readonly Color UV = Hex("#9B5CFF");
    public static readonly Color Sky = Hex("#0B1020");
    public static readonly Color Finish = Hex("#3DFF8B");
    public static readonly Color TextMain = Hex("#F2F5FA");
    public static readonly Color TextDim = Hex("#8C95A6");

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
