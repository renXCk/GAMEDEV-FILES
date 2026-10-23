using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Anything that wants its detection volumes / rays drawn.
public interface IParkourDebug
{
    void DrawDebug();
}

// Runtime debug view for the demo video.
//
//   G   toggle
//
// Draws every hazard volume (CheckBox / CheckSphere / CheckCapsule) and every
// ray (sentry Raycasts, UV light occlusion Raycasts) as GL lines on top of the
// Game view, so it shows up in recordings WITHOUT the Gizmos button.
// Green / white = clear, red = touching the player this frame.
// The same shapes are also drawn as Gizmos in the Scene view.
public class ParkourDebugDraw : MonoBehaviour
{
    public static bool Enabled { get; private set; }

    public Key toggleKey = Key.G;
    public bool startEnabled = false;

    static readonly List<IParkourDebug> drawables = new List<IParkourDebug>();
    static Material lineMaterial;
    static bool initialised;

    public static void Register(IParkourDebug d)
    {
        if (!drawables.Contains(d)) drawables.Add(d);
    }

    public static void Unregister(IParkourDebug d)
    {
        drawables.Remove(d);
    }

    void Awake()
    {
        // Only once per play session, so a restart (R) keeps the debug view on.
        if (!initialised)
        {
            Enabled = startEnabled;
            initialised = true;
        }
    }

    // Domain reload can be disabled in Enter Play Mode settings; reset statics anyway.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        initialised = false;
        Enabled = false;
        drawables.Clear();
    }

    void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
            Enabled = !Enabled;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!Enabled || cam.cameraType != CameraType.Game || !EnsureMaterial())
            return;

        GL.PushMatrix();
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.modelview = cam.worldToCameraMatrix;

        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);

        DebugShapes.UseGizmos = false;

        for (int i = drawables.Count - 1; i >= 0; i--)
        {
            if (drawables[i] is Object o && o == null)
            {
                drawables.RemoveAt(i);
                continue;
            }

            drawables[i].DrawDebug();
        }

        GL.End();
        GL.PopMatrix();
    }

    static bool EnsureMaterial()
    {
        if (lineMaterial != null)
            return true;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return false;

        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        // Always on top, no culling, alpha blended.
        if (lineMaterial.HasProperty("_ZTest"))
        {
            lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        return true;
    }
}

// Shape helpers that work both inside GL.Begin(GL.LINES) (runtime) and in
// OnDrawGizmos (Scene view). Set UseGizmos before drawing.
public static class DebugShapes
{
    public static bool UseGizmos;

    public static readonly Color Clear = new Color(0.3f, 1f, 0.45f, 0.9f);
    public static readonly Color Hit = new Color(1f, 0.15f, 0.15f, 1f);
    public static readonly Color Ray = new Color(1f, 0.9f, 0.3f, 0.8f);
    public static readonly Color Blocked = new Color(0.6f, 0.6f, 0.6f, 0.6f);
    public static readonly Color Cone = new Color(0.75f, 0.5f, 1f, 0.8f);

    public static Color State(bool hit) => hit ? Hit : Clear;

    public static void Line(Vector3 a, Vector3 b, Color c)
    {
        if (UseGizmos)
        {
            Gizmos.color = c;
            Gizmos.DrawLine(a, b);
        }
        else
        {
            GL.Color(c);
            GL.Vertex(a);
            GL.Vertex(b);
        }
    }

    public static void Circle(Vector3 centre, Vector3 normal, float radius, Color c, int segments = 24)
    {
        Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(normal, u);
        Vector3 prev = centre + u * radius;

        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 p = centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
            Line(prev, p, c);
            prev = p;
        }
    }

    public static void Sphere(Vector3 centre, float radius, Color c)
    {
        Circle(centre, Vector3.up, radius, c);
        Circle(centre, Vector3.right, radius, c);
        Circle(centre, Vector3.forward, radius, c);
    }

    public static void Box(Vector3 centre, Vector3 halfExtents, Quaternion rotation, Color c)
    {
        Vector3[] p = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 s = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            p[i] = centre + rotation * Vector3.Scale(s, halfExtents);
        }

        int[,] edges =
        {
            {0,1},{2,3},{4,5},{6,7},
            {0,2},{1,3},{4,6},{5,7},
            {0,4},{1,5},{2,6},{3,7}
        };

        for (int e = 0; e < 12; e++)
            Line(p[edges[e, 0]], p[edges[e, 1]], c);
    }

    public static void Capsule(Vector3 a, Vector3 b, float radius, Color c)
    {
        Vector3 axis = b - a;
        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;

        Circle(a, dir, radius, c, 16);
        Circle(b, dir, radius, c, 16);

        Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized * radius;
        Vector3 v = Vector3.Cross(dir, u.normalized) * radius;

        Line(a + u, b + u, c);
        Line(a - u, b - u, c);
        Line(a + v, b + v, c);
        Line(a - v, b - v, c);

        // Hemispherical end caps (just the outline arcs).
        Line(a, a - dir * radius, c);
        Line(b, b + dir * radius, c);
    }
}
