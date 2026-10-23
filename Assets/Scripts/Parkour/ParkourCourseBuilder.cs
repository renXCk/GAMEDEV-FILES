using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Editor-side generator for the NEON LASER CORRIDOR parkour course.
// Add this to an empty GameObject, press BUILD COURSE, then save the scene.
// Everything it creates is a normal scene object, so you can move / resize /
// ProBuilder-edit it afterwards. Materials and the post-processing profile are
// saved as assets in Assets/Parkour/Generated so you can tweak them too.
//
// One long white corridor (8 m wide, open roof) over a laser pit.
// Player runs +Z. Falling into the pit = fail.
//
//   S1  z   0-46   Warm-up      low laser (CheckCapsule), sweeping gate (CheckBox)     -> CP1
//   S2  z  46-100  Wall-run     2 wall-run gaps, rotating arm (CheckCapsule),
//                               double-jump gap                                         -> CP2
//   S3  z 100-152  Slide/vault  slide bars, vault box, pulsing scanner (CheckBox),
//                               mantle wall, windmill (CheckCapsule), orbs (CheckSphere) -> CP3
//   S4  z 152-190  UV tunnel    hidden tripwires (CheckCapsule), UV markers, UV shards  -> CP4
//   S5  z 190-252  Sentries     rail sentry eyes (Raycast) + cover, swinging orb
//                               (CheckSphere), kicker ramp, FINISH
public class ParkourCourseBuilder : MonoBehaviour
{
    [Header("Player")]
    public Vector3 playerStart = new Vector3(0f, 0.05f, 1f);

    [Header("Look")]
    public bool buildScenery = true;
    public int scenerySeed = 1337;

    const string RootName = "ParkourCourse";
    const string PlayerName = "Player";
    const string GameName = "ParkourGame";
    const string PostName = "PostFX";
    const string SunName = "Sun";
    const string GeneratedFolder = "Assets/Parkour/Generated";

    // Corridor dimensions.
    const float HalfW = 4f;          // inner half width
    const float PitY = -6f;          // top of the laser pit floor
    const float WallTop = 12f;
    const float CourseStart = -2.5f;
    const float CourseEnd = 252f;
    const float SceneryFloorY = -14f;

    // Thickness of the glowing edge strips.
    const float Strip = 0.07f;

#if UNITY_EDITOR

    // ---- Materials (saved as assets) ---------------------------------
    Material mSurface, mSurfaceAlt, mTrim, mVoid;
    Material mRoute, mWallRunPanel, mWallRunStrip, mFinish;
    Material mLaser, mLaserHalo, mSentry, mUV, mUVHalo, mPitGlow;

    // ---- Hierarchy ---------------------------------------------------
    Transform root, env, hazards, uvRoot, lightsRoot, hints;
    int checkpointCount;

    // ==================================================================
    // BUILD
    // ==================================================================

    [ContextMenu("BUILD COURSE")]
    public void BuildCourse()
    {
        int playerLayer = EnsureLayer(ParkourLayers.PlayerLayerName);

        ClearGeneratedLevel();
        EnsureFolder(GeneratedFolder);
        CreateMaterials();

        root = NewChild(RootName, transform);
        env = NewChild("Environment", root);
        hazards = NewChild("Hazards", root);
        uvRoot = NewChild("UV_Hidden", root);
        hints = NewChild("FloorHints", root);
        lightsRoot = NewChild("Lights", root);
        checkpointCount = 0;

        BuildCorridorShell();
        BuildSection1();
        BuildSection2();
        BuildSection3();
        BuildSection4();
        BuildSection5();
        BuildCorridorLights();

        if (buildScenery)
            BuildScenery();

        BuildPlayer(playerLayer);
        BuildGame();
        BuildLighting();
        BuildPostProcessing();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Selection.activeGameObject = root.gameObject;

        if (!TmpReady)
            Debug.LogWarning("TextMeshPro essentials are not imported, so the HUD won't show. " +
                             "Window > TextMeshPro > Import TMP Essential Resources, then BUILD COURSE again.");

        Debug.Log(
            "NEON LASER CORRIDOR BUILT.\n" +
            "Save the scene (Ctrl+S), press Play and click the Game view to lock the mouse. G = debug view."
        );
    }

    // ==================================================================
    // CORRIDOR SHELL
    // ==================================================================

    void BuildCorridorShell()
    {
        Transform shell = NewChild("Corridor", env);
        float length = CourseEnd - CourseStart;
        float midZ = (CourseStart + CourseEnd) * 0.5f;

        // Laser pit floor + glowing grid (purely visual, the kill height is in the game manager).
        Box("PitFloor", shell, new Vector3(0f, PitY - 0.5f, midZ), new Vector3(HalfW * 2f, 1f, length), mVoid, true);

        Transform grid = NewChild("PitGrid", shell);
        for (float z = 0f; z < CourseEnd; z += 4f)
            Box("Grid_" + z, grid, new Vector3(0f, PitY + 0.02f, z), new Vector3(HalfW * 2f, 0.04f, 0.08f), mPitGlow, false);
        Box("Grid_L", grid, new Vector3(-2f, PitY + 0.02f, midZ), new Vector3(0.08f, 0.04f, length), mPitGlow, false);
        Box("Grid_R", grid, new Vector3(2f, PitY + 0.02f, midZ), new Vector3(0.08f, 0.04f, length), mPitGlow, false);

        // Side walls in 16 m segments. They cast no shadows, so the sun still
        // lights the floor of this open-roof corridor.
        float wallH = WallTop - PitY;
        float wallY = (WallTop + PitY) * 0.5f;

        for (float z = CourseStart; z < CourseEnd; z += 16f)
        {
            float segLen = Mathf.Min(16f, CourseEnd - z);
            float cz = z + segLen * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject w = Box("Wall_" + (side < 0 ? "L_" : "R_") + z, shell,
                    new Vector3(side * (HalfW + 0.25f), wallY, cz), new Vector3(0.5f, wallH, segLen), mSurface, true);
                NoShadows(w);

                // Neon accent line high on the wall.
                Box("WallGlow", w.transform.parent, new Vector3(side * (HalfW - 0.01f), 8.5f, cz),
                    new Vector3(0.04f, 0.06f, segLen), mRoute, false);
            }
        }

        // Ribs and roof beams every 8 m: rhythm = sense of speed.
        for (float z = 0f; z < CourseEnd; z += 8f)
        {
            for (int side = -1; side <= 1; side += 2)
                Box("Rib", shell, new Vector3(side * (HalfW - 0.06f), wallY, z), new Vector3(0.12f, wallH, 0.3f), mTrim, false);

            Box("RoofBeam", shell, new Vector3(0f, WallTop - 0.2f, z), new Vector3(HalfW * 2f + 1f, 0.4f, 0.4f), mTrim, false);
            Box("RoofBeamGlow", shell, new Vector3(0f, WallTop - 0.42f, z), new Vector3(HalfW * 2f, 0.04f, 0.08f), mRoute, false);
        }

        // End caps.
        Box("Wall_Start", shell, new Vector3(0f, wallY, CourseStart - 0.25f), new Vector3(HalfW * 2f + 1f, wallH, 0.5f), mSurfaceAlt, true);
        Box("Wall_End", shell, new Vector3(0f, wallY, CourseEnd + 0.25f), new Vector3(HalfW * 2f + 1f, wallH, 0.5f), mSurfaceAlt, true);
    }

    // ==================================================================
    // SECTIONS
    // ==================================================================

    // S1 - Warm-up: run, sprint, jump. Low laser, sweeping gate.
    void BuildSection1()
    {
        Transform s = NewChild("S1_WarmUp", env);

        Platform("StartPad", s, new Vector3(0f, 0f, 3.5f), new Vector3(8f, 1f, 11f));
        StartArch(s, new Vector3(0f, 0f, 6f), 8f, 4f, mRoute);

        Ramp("Ramp_1", s, new Vector3(0f, 0f, 9f), new Vector3(0f, 3f, 17f), 8f);

        Platform("Plat_1A", s, new Vector3(0f, 3f, 20.5f), new Vector3(8f, 1f, 7f));
        BuildLaserBeam("Low Laser", hazards, new Vector3(0f, 3.4f, 22.2f), 7.8f, new Vector3(0f, 0.15f, 0f), 2f, false);

        // 3 m gap
        Platform("Plat_1B", s, new Vector3(0f, 3f, 30.5f), new Vector3(8f, 1f, 7f));
        BuildLaserGate("Sweeping Laser Gate",
            new Vector3(-2.1f, 4.3f, 30.5f), new Vector3(2.1f, 4.3f, 30.5f), new Vector3(3.6f, 2.6f, 0.2f), 2.8f);

        // 4 m gap with a 1 m drop
        Platform("Plat_1C", s, new Vector3(0f, 2f, 42f), new Vector3(8f, 1f, 8f));
        BuildCheckpoint(new Vector3(0f, 2f, 43.5f));

        BuildShard("Shard_1_BehindStart", new Vector3(3.2f, 0.7f, -1.4f));
    }

    // S2 - Wall-run hall: two wall-run gaps, rotating arm island, double jump.
    void BuildSection2()
    {
        Transform s = NewChild("S2_WallRun", env);

        // Gap A (z 46-58) - wall-run on the RIGHT wall.
        WallRunPanel("WallRun_Right", s, 1, 45.5f, 58.5f, -1f, 6.5f);

        Platform("Island_2A", s, new Vector3(0f, 1.5f, 61f), new Vector3(8f, 1f, 6f));
        BuildRotatingArm("Rotating Laser Arm", new Vector3(0f, 2.05f, 61f), Quaternion.identity, 2, 3.9f, 110f);
        BuildShard("Shard_2_Island", new Vector3(-3.3f, 2.1f, 58.6f));

        // Gap B (z 64-76) - wall-run on the LEFT wall.
        WallRunPanel("WallRun_Left", s, -1, 63.5f, 76.5f, -1.5f, 6f);

        Platform("Plat_2B", s, new Vector3(0f, 1f, 81f), new Vector3(8f, 1f, 10f));
        Chevrons(new Vector3(0f, 1f, 85f), mRoute, hints);

        // 7 m gap up 2 m: needs the double jump (or a ledge grab).
        Platform("Plat_2C", s, new Vector3(0f, 3f, 96.5f), new Vector3(8f, 1f, 7f));
        BuildCheckpoint(new Vector3(0f, 3f, 97.5f));
    }

    // S3 - Slide & vault: slide bars, vault box, scanner wall, mantle, windmill, orb gap.
    void BuildSection3()
    {
        Transform s = NewChild("S3_SlideVault", env);
        const float floor = 3f;
        const float upper = 4.6f;

        Platform("Plat_3A", s, new Vector3(0f, floor, 115f), new Vector3(8f, 1f, 30f));

        SlideBar("SlideBar_1", s, 104f, floor);

        VaultBox("VaultBox", s, new Vector3(0f, floor, 109f), 1f);
        BuildShard("Shard_3_BehindVault", new Vector3(3.3f, floor + 0.45f, 110.2f));

        BuildScannerWall("Pulsing Scanner Wall",
            new Vector3(0f, floor + 2.5f, 114.5f), new Vector3(0f, floor + 2.5f, 121.5f), new Vector3(7.8f, 5f, 0.3f));

        SlideBar("SlideBar_2", s, 126f, floor);

        // Chest-high ledge (1.6 m): mantle with Space.
        Platform("Plat_3B", s, new Vector3(0f, upper, 135f), new Vector3(8f, 2.6f, 10f));

        // Windmill: arms spin across the corridor (local up = +Z).
        BuildRotatingArm("Laser Windmill", new Vector3(0f, upper + 3.2f, 136f), Quaternion.Euler(90f, 0f, 0f), 2, 3.9f, 70f);

        // Orb gap (z 140-146): 2 rows x 3 orbs bobbing out of phase.
        float[] xs = { -2.6f, 0f, 2.6f };
        float[] rows = { 142f, 144.5f };
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < xs.Length; i++)
                BuildOrb("Bobbing Orb " + (r * 3 + i + 1), new Vector3(xs[i], upper + 0.7f, rows[r]), 0.8f, 2f, 2.2f, (i * 0.33f + r * 0.5f) % 1f);

        Platform("Plat_3C", s, new Vector3(0f, upper, 149f), new Vector3(8f, 1f, 6f));
        BuildCheckpoint(new Vector3(0f, upper, 150f));
    }

    // S4 - UV tunnel: roofed (darker), hidden tripwires, UV markers + shards.
    void BuildSection4()
    {
        Transform s = NewChild("S4_UVTunnel", env);
        const float floor = 4.6f;

        Platform("Plat_4A", s, new Vector3(0f, floor, 171f), new Vector3(8f, 1f, 38f));

        // Roof makes this the dark part of the course (it DOES cast shadows).
        Box("Roof", s, new Vector3(0f, 10.75f, 171f), new Vector3(HalfW * 2f + 1f, 0.5f, 38f), mTrim, true);
        Box("RoofEdgeGlow_In", s, new Vector3(0f, 10.45f, 152.1f), new Vector3(HalfW * 2f, 0.08f, 0.08f), mUV, false);
        Box("RoofEdgeGlow_Out", s, new Vector3(0f, 10.45f, 189.9f), new Vector3(HalfW * 2f, 0.08f, 0.08f), mUV, false);

        // Hidden tripwires (always armed, only visible under UV).
        BuildLaserBeam("UV Tripwire Low", hazards, new Vector3(0f, floor + 0.35f, 159f), 7.8f, Vector3.zero, 1f, true);
        BuildLaserBeam("UV Tripwire Chest", hazards, new Vector3(0f, floor + 1.25f, 165f), 7.8f, Vector3.zero, 1f, true);
        BuildLaserBeam("UV Tripwire Moving", hazards, new Vector3(0f, floor + 0.9f, 171f), 7.8f, new Vector3(0f, 0.55f, 0f), 2.6f, true);
        BuildLaserBeam("UV Tripwire Sliding", hazards, new Vector3(0f, floor + 1f, 177f), 3.8f, new Vector3(1.9f, 0f, 0f), 3.2f, true);
        BuildLaserBeam("UV Tripwire Low 2", hazards, new Vector3(0f, floor + 0.35f, 183f), 7.8f, Vector3.zero, 1f, true);

        // UV route markers telling you what each wire needs.
        UVMarker("UV_Marker_Jump", new Vector3(0f, floor, 156.6f));
        UVMarker("UV_Marker_Slide", new Vector3(0f, floor, 162.6f));
        UVMarker("UV_Marker_Time", new Vector3(0f, floor, 168.6f));
        UVMarker("UV_Marker_Dodge", new Vector3(0f, floor, 174.6f));
        UVMarker("UV_Marker_Jump2", new Vector3(0f, floor, 180.6f));

        // UV shards + markers pointing at them.
        BuildShard("Shard_4_LeftWall", new Vector3(-3.3f, floor + 0.6f, 161f));
        BuildShard("Shard_5_RightWall", new Vector3(3.3f, floor + 0.6f, 173f));
        BuildShard("Shard_6_High", new Vector3(0f, floor + 2f, 186f));
        UVMarker("UV_Arrow_Shard4", new Vector3(-1.8f, floor, 160f), -60f);
        UVMarker("UV_Arrow_Shard5", new Vector3(1.8f, floor, 172f), 60f);
        UVMarker("UV_Arrow_Shard6", new Vector3(0f, floor, 185f));

        BuildCheckpoint(new Vector3(0f, floor, 189f));
    }

    // S5 - Sentries: cover pillars, low wall (slide/crouch), swinging orb, finish jump.
    void BuildSection5()
    {
        Transform s = NewChild("S5_Sentries", env);
        const float floor = 4.6f;

        Platform("Plat_5A", s, new Vector3(0f, floor, 211f), new Vector3(8f, 1f, 42f));

        // Sentry 1: ceiling rail across the corridor, looking back at you.
        Box("Cover_1", s, new Vector3(-2.2f, floor + 1.8f, 196.5f), new Vector3(1.4f, 3.6f, 1.4f), mSurfaceAlt, true);
        Box("Cover_2", s, new Vector3(2.2f, floor + 1.8f, 200.5f), new Vector3(1.4f, 3.6f, 1.4f), mSurfaceAlt, true);
        BuildSentry("Sentry Eye 1",
            new Vector3(-3f, floor + 4.6f, 205f), new Vector3(3f, floor + 4.6f, 205f),
            new Vector3(0f, -0.45f, -1f), 25f, 2.6f, 45f, 16f, 4.5f);

        // Sentry 2: rides a wall rail; stay LOW behind the low wall.
        Box("LowWall", s, new Vector3(0.6f, floor + 0.7f, 218f), new Vector3(0.4f, 1.4f, 22f), mSurfaceAlt, true);
        BuildSentry("Sentry Eye 2",
            new Vector3(3.55f, floor + 1.5f, 212f), new Vector3(3.55f, floor + 1.5f, 224f),
            new Vector3(-1f, 0f, 0f), 30f, 3f, 40f, 9f, 6f);

        // Swinging orb across the corridor.
        BuildSwing("Swinging Orb", new Vector3(0f, floor + 7.2f, 231f), 5.8f, 0.75f, 42f, 2.6f);

        // Kicker ramp + final gap + finish.
        Ramp("Ramp_Kicker", s, new Vector3(0f, floor, 232f), new Vector3(0f, 6f, 237f), 8f);
        Platform("FinishPad", s, new Vector3(0f, 5.5f, 246.75f), new Vector3(8f, 1f, 9.5f), mSurface);
        StartArch(s, new Vector3(0f, 5.5f, 246f), 8f, 4f, mFinish);

        GameObject finish = new GameObject("FinishLine");
        Undo.RegisterCreatedObjectUndo(finish, "Create FinishLine");
        finish.transform.SetParent(s);
        finish.transform.position = new Vector3(0f, 5.5f, 246f);
        finish.AddComponent<FinishLine>();
    }

    void BuildCorridorLights()
    {
        Color warm = new Color(1f, 0.93f, 0.85f);

        for (float z = 4f; z < CourseEnd; z += 14f)
        {
            if (z > 152f && z < 190f)
                continue; // the UV tunnel gets its own dim lights

            Color c = Mathf.RoundToInt(z / 14f) % 2 == 0 ? ParkourPalette.Route : warm;
            Lamp("Lamp_" + z, new Vector3(0f, 9f, z), c, 4f, 18f);
        }

        for (float z = 158f; z < 190f; z += 12f)
            Lamp("Lamp_UV_" + z, new Vector3(0f, 9.5f, z), ParkourPalette.UV, 1.5f, 12f);
    }

    // ==================================================================
    // HAZARD BUILDERS
    // ==================================================================

    void BuildLaserBeam(string name, Transform parent, Vector3 pos, float length, Vector3 moveOffset, float period, bool hidden)
    {
        Transform t = NewChild(name, parent);
        t.position = pos;

        LocalBox("Beam", t, Vector3.zero, new Vector3(length, 0.035f, 0.035f), hidden ? mUV : mLaser, false);
        LocalBox("Halo", t, Vector3.zero, new Vector3(length, 0.1f, 0.1f), hidden ? mUVHalo : mLaserHalo, false);

        bool moving = moveOffset != Vector3.zero;

        // Emitters: on the walls for full-width beams (always visible, a hint
        // that "something" is there), or riding along with short moving beams.
        if (length >= HalfW * 2f - 0.5f && !moving)
        {
            for (int side = -1; side <= 1; side += 2)
                Box("Emitter", parent, new Vector3(side * (HalfW - 0.08f), pos.y, pos.z), new Vector3(0.16f, 0.2f, 0.2f), mTrim, false);
        }
        else
        {
            LocalBox("Emitter_A", t, new Vector3(-length * 0.5f, 0f, 0f), new Vector3(0.14f, 0.16f, 0.16f), mTrim, false);
            LocalBox("Emitter_B", t, new Vector3(length * 0.5f, 0f, 0f), new Vector3(0.14f, 0.16f, 0.16f), mTrim, false);
        }

        if (hidden)
            t.gameObject.AddComponent<UVReveal>().revealLength = length;

        LaserBeam beam = t.gameObject.AddComponent<LaserBeam>();
        beam.hazardName = name;
        beam.length = length;
        beam.moveOffset = moveOffset;
        beam.movePeriod = period;
    }

    void BuildLaserGate(string name, Vector3 a, Vector3 b, Vector3 size, float period)
    {
        Transform t = NewChild(name, hazards);
        t.position = a;

        for (int i = 0; i < 6; i++)
        {
            float y = -size.y * 0.5f + 0.2f + i * ((size.y - 0.4f) / 5f);
            LocalBox("Line_" + i, t, new Vector3(0f, y, 0f), new Vector3(size.x, 0.035f, 0.035f), mLaser, false);
        }

        LocalBox("Haze", t, Vector3.zero, new Vector3(size.x, size.y, 0.02f), mLaserHalo, false);
        LocalBox("Post_L", t, new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(0.12f, size.y + 0.2f, 0.12f), mTrim, false);
        LocalBox("Post_R", t, new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.12f, size.y + 0.2f, 0.12f), mTrim, false);
        AddLight(t, "Glow", Vector3.zero, LightType.Point, ParkourPalette.Laser, 2f, 4f, 0f, false);

        SweepingLaserGate g = t.gameObject.AddComponent<SweepingLaserGate>();
        g.hazardName = name;
        g.pointA = a;
        g.pointB = b;
        g.size = size;
        g.period = period;
    }

    void BuildScannerWall(string name, Vector3 a, Vector3 b, Vector3 size)
    {
        Transform t = NewChild(name, hazards);
        t.position = a;

        var field = new System.Collections.Generic.List<Renderer>();
        field.Add(LocalBox("Field", t, Vector3.zero, new Vector3(size.x, size.y, 0.04f), mLaserHalo, false).GetComponent<Renderer>());

        for (int i = 0; i < 8; i++)
        {
            float y = -size.y * 0.5f + 0.15f + i * ((size.y - 0.3f) / 7f);
            field.Add(LocalBox("ScanLine_" + i, t, new Vector3(0f, y, 0f), new Vector3(size.x, 0.03f, 0.03f), mLaser, false).GetComponent<Renderer>());
        }

        // Frame stays visible while the field is OFF, so you can see where it is.
        LocalBox("Frame_L", t, new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(0.15f, size.y + 0.3f, 0.3f), mTrim, false);
        LocalBox("Frame_R", t, new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.15f, size.y + 0.3f, 0.3f), mTrim, false);
        LocalBox("Frame_Glow_L", t, new Vector3(-size.x * 0.5f + 0.08f, 0f, 0f), new Vector3(0.03f, size.y, 0.1f), mLaser, false);
        LocalBox("Frame_Glow_R", t, new Vector3(size.x * 0.5f - 0.08f, 0f, 0f), new Vector3(0.03f, size.y, 0.1f), mLaser, false);

        Light glow = AddLight(t, "FieldLight", Vector3.zero, LightType.Point, ParkourPalette.Laser, 3f, 6f, 0f, false);

        PulsingScannerWall w = t.gameObject.AddComponent<PulsingScannerWall>();
        w.hazardName = name;
        w.pointA = a;
        w.pointB = b;
        w.size = size;
        w.fieldRenderers = field.ToArray();
        w.fieldLight = glow;
    }

    void BuildRotatingArm(string name, Vector3 centre, Quaternion rotation, int arms, float length, float speed)
    {
        Transform t = NewChild(name, hazards);
        t.SetPositionAndRotation(centre, rotation);

        RotatingLaserArm arm = t.gameObject.AddComponent<RotatingLaserArm>();
        arm.hazardName = name;
        arm.armCount = arms;
        arm.armLength = length;
        arm.degreesPerSecond = speed;

        float mid = (arm.armStart + length) * 0.5f;
        float len = length - arm.armStart;

        for (int i = 0; i < arms; i++)
        {
            Quaternion r = Quaternion.Euler(0f, 360f / arms * i, 0f);

            // Cylinder is Y-up, so tip it to lie along the arm's Z axis.
            LocalPrim(PrimitiveType.Cylinder, "Arm_" + i, t, r * new Vector3(0f, 0f, mid),
                new Vector3(arm.beamRadius * 1.2f, len * 0.5f, arm.beamRadius * 1.2f), mLaser, r * Quaternion.Euler(90f, 0f, 0f));
            LocalPrim(PrimitiveType.Cylinder, "ArmHalo_" + i, t, r * new Vector3(0f, 0f, mid),
                new Vector3(arm.beamRadius * 3f, len * 0.5f, arm.beamRadius * 3f), mLaserHalo, r * Quaternion.Euler(90f, 0f, 0f));
        }

        LocalPrim(PrimitiveType.Cylinder, "Hub", t, Vector3.zero, new Vector3(0.6f, 0.15f, 0.6f), mTrim, Quaternion.identity);
        AddLight(t, "Glow", Vector3.zero, LightType.Point, ParkourPalette.Laser, 2.5f, 6f, 0f, false);
    }

    void BuildOrb(string name, Vector3 pos, float radius, float amplitude, float period, float phase)
    {
        Transform t = NewChild(name, hazards);
        t.position = pos;

        LocalPrim(PrimitiveType.Sphere, "Core", t, Vector3.zero, Vector3.one * radius * 1.6f, mLaser, Quaternion.identity);
        LocalPrim(PrimitiveType.Sphere, "Halo", t, Vector3.zero, Vector3.one * radius * 2f, mLaserHalo, Quaternion.identity);
        AddLight(t, "Glow", Vector3.zero, LightType.Point, ParkourPalette.Laser, 1.5f, 3.5f, 0f, false);

        BobbingOrb o = t.gameObject.AddComponent<BobbingOrb>();
        o.hazardName = name;
        o.radius = radius;
        o.amplitude = amplitude;
        o.period = period;
        o.phaseOffset = phase;
    }

    void BuildSwing(string name, Vector3 pivotPos, float length, float radius, float maxAngle, float period)
    {
        Box("SwingMount", env, pivotPos + Vector3.up * 0.2f, new Vector3(HalfW * 2f + 0.5f, 0.4f, 0.6f), mTrim, false);

        Transform pivot = NewChild(name, hazards);
        pivot.position = pivotPos;

        LocalPrim(PrimitiveType.Cylinder, "Cable", pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.06f, length * 0.5f, 0.06f), mTrim, Quaternion.identity);
        LocalPrim(PrimitiveType.Sphere, "Ball", pivot, new Vector3(0f, -length, 0f), Vector3.one * radius * 2f, mLaser, Quaternion.identity);
        LocalPrim(PrimitiveType.Sphere, "Halo", pivot, new Vector3(0f, -length, 0f), Vector3.one * radius * 2.5f, mLaserHalo, Quaternion.identity);
        AddLight(pivot, "Glow", new Vector3(0f, -length, 0f), LightType.Point, ParkourPalette.Laser, 3f, 6f, 0f, false);

        SwingingHazard sw = pivot.gameObject.AddComponent<SwingingHazard>();
        sw.hazardName = name;
        sw.length = length;
        sw.radius = radius;
        sw.maxAngle = maxAngle;
        sw.period = period;
    }

    void BuildSentry(string name, Vector3 railA, Vector3 railB, Vector3 lookDir,
                     float sweep, float sweepPeriod, float viewAngle, float range, float railPeriod)
    {
        // Static rail visual.
        Vector3 railDir = railB - railA;
        GameObject rail = Box(name + " Rail", env, (railA + railB) * 0.5f + Vector3.up * 0.4f,
            new Vector3(0.1f, 0.1f, railDir.magnitude + 0.8f), mTrim, false);
        rail.transform.rotation = Quaternion.LookRotation(railDir.normalized);

        Transform t = NewChild(name, hazards);
        t.position = railA;

        LocalPrim(PrimitiveType.Sphere, "Body", t, Vector3.zero, Vector3.one * 0.55f, mTrim, Quaternion.identity);
        LocalBox("Hanger", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.3f, 0.08f), mTrim, false);

        Transform head = NewChild("Head", t);
        head.rotation = Quaternion.LookRotation(lookDir.normalized);
        LocalPrim(PrimitiveType.Sphere, "Lens", head, new Vector3(0f, 0f, 0.24f), new Vector3(0.26f, 0.26f, 0.12f), mSentry, Quaternion.identity);
        LocalPrim(PrimitiveType.Cylinder, "Ring", head, new Vector3(0f, 0f, 0.2f), new Vector3(0.42f, 0.02f, 0.42f), mSentry, Quaternion.Euler(90f, 0f, 0f));

        Light cone = AddLight(head, "ConeLight", new Vector3(0f, 0f, 0.32f), LightType.Spot, ParkourPalette.Sentry, 25f, range, viewAngle, true);

        LineRenderer lr = t.gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = 0.035f;
        lr.sharedMaterial = mSentry;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.SetPosition(0, railA);
        lr.SetPosition(1, railA + lookDir.normalized);

        RailSentryEye s = t.gameObject.AddComponent<RailSentryEye>();
        s.hazardName = name;
        s.railA = railA;
        s.railB = railB;
        s.railPeriod = railPeriod;
        s.head = head;
        s.lookDirection = lookDir;
        s.sweepAngle = sweep;
        s.sweepPeriod = sweepPeriod;
        s.viewAngle = viewAngle;
        s.range = range;
        s.coneLight = cone;
        s.beam = lr;
    }

    // ==================================================================
    // PICKUPS / CHECKPOINTS / MARKERS
    // ==================================================================

    void BuildCheckpoint(Vector3 pos)
    {
        checkpointCount++;

        Transform t = NewChild("Checkpoint_" + checkpointCount, env);
        t.position = pos;

        Renderer[] glow =
        {
            LocalBox("Post_L", t, new Vector3(-HalfW + 0.25f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), mRoute, false).GetComponent<Renderer>(),
            LocalBox("Post_R", t, new Vector3(HalfW - 0.25f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), mRoute, false).GetComponent<Renderer>(),
            LocalBox("FloorLine", t, new Vector3(0f, 0.01f, 0f), new Vector3(HalfW * 2f - 0.5f, 0.02f, 0.15f), mRoute, false).GetComponent<Renderer>(),
        };

        AddLight(t, "Glow", new Vector3(0f, 2.5f, 0f), LightType.Point, ParkourPalette.Route, 2f, 6f, 0f, false);

        Checkpoint cp = t.gameObject.AddComponent<Checkpoint>();
        cp.index = checkpointCount;
        cp.glowRenderers = glow;
        cp.reachedMaterial = mFinish;
    }

    void BuildShard(string name, Vector3 pos)
    {
        Transform t = NewChild(name, uvRoot);
        t.position = pos;

        LocalPrim(PrimitiveType.Cube, "Crystal", t, Vector3.zero, Vector3.one * 0.3f, mUV, Quaternion.Euler(45f, 0f, 45f));
        LocalPrim(PrimitiveType.Sphere, "Halo", t, Vector3.zero, Vector3.one * 0.6f, mUVHalo, Quaternion.identity);
        AddLight(t, "Glow", Vector3.zero, LightType.Point, ParkourPalette.UV, 1.5f, 2.5f, 0f, false);

        t.gameObject.AddComponent<UVReveal>();
        t.gameObject.AddComponent<UVCollectible>();
    }

    // A UV-only floor marker (two chevrons). Hidden until the UV light hits it.
    void UVMarker(string name, Vector3 pos, float yaw = 0f)
    {
        Transform t = NewChild(name, uvRoot);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        Chevron(t, new Vector3(0f, 0.03f, -0.5f), mUV);
        Chevron(t, new Vector3(0f, 0.03f, 0.1f), mUV);

        t.gameObject.AddComponent<UVReveal>();
    }

    // Visible chevron row pointing +Z.
    void Chevrons(Vector3 pos, Material mat, Transform parent)
    {
        Transform t = NewChild("Chevrons", parent);
        t.position = pos;

        for (int i = 0; i < 3; i++)
            Chevron(t, new Vector3(0f, 0.03f, i * 0.6f), mat);
    }

    // ">" shape pointing +Z made of two thin bars.
    void Chevron(Transform parent, Vector3 localPos, Material mat)
    {
        LocalPrim(PrimitiveType.Cube, "Chev_L", parent, localPos + new Vector3(-0.2f, 0f, 0f), new Vector3(0.1f, 0.02f, 0.55f), mat, Quaternion.Euler(0f, 35f, 0f));
        LocalPrim(PrimitiveType.Cube, "Chev_R", parent, localPos + new Vector3(0.2f, 0f, 0f), new Vector3(0.1f, 0.02f, 0.55f), mat, Quaternion.Euler(0f, -35f, 0f));
    }

    static bool TmpReady => TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null;

    // ==================================================================
    // HUD FONT
    // ==================================================================

    const string FontFolder = "Assets/Parkour/Fonts";

    // Copies Inter SemiBold (SIL Open Font License, shipped inside the
    // com.unity.dt.app-ui package) into the project and makes a dynamic TMP
    // font asset from it. Returns null (= HUD uses the TMP default) if missing.
    static TMP_FontAsset BuildHudFont()
    {
        string assetPath = FontFolder + "/Inter-SemiBold SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        EnsureFolder(FontFolder);
        string ttfPath = FontFolder + "/Inter-SemiBold.ttf";

        if (!System.IO.File.Exists(ttfPath))
        {
            string[] pkgs = System.IO.Directory.Exists("Library/PackageCache")
                ? System.IO.Directory.GetDirectories("Library/PackageCache", "com.unity.dt.app-ui@*")
                : new string[0];

            string src = pkgs.Length > 0 ? System.IO.Path.Combine(pkgs[0], "PackageResources/Fonts/Inter-SemiBold.ttf") : null;

            if (src == null || !System.IO.File.Exists(src))
            {
                Debug.LogWarning("Inter font not found - the HUD will use the TMP default font.");
                return null;
            }

            System.IO.File.Copy(src, ttfPath);
            AssetDatabase.ImportAsset(ttfPath);
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
            return null;

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (fa == null)
            return null;

        fa.name = "Inter-SemiBold SDF";
        AssetDatabase.CreateAsset(fa, assetPath);

        // The atlas texture and material must live inside the font asset.
        fa.atlasTextures[0].name = "Inter-SemiBold Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = "Inter-SemiBold Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        AssetDatabase.SaveAssets();
        return fa;
    }

    // ==================================================================
    // GEOMETRY PIECES
    // ==================================================================

    void StartArch(Transform parent, Vector3 basePos, float width, float height, Material glow)
    {
        Transform a = NewChild("Arch", parent);
        a.position = basePos;

        float half = width * 0.5f - 0.15f;
        LocalBox("Bar", a, new Vector3(0f, height, 0f), new Vector3(width, 0.25f, 0.25f), mTrim, false);
        LocalBox("Post_L", a, new Vector3(-half, height * 0.5f, 0f), new Vector3(0.25f, height, 0.25f), mTrim, false);
        LocalBox("Post_R", a, new Vector3(half, height * 0.5f, 0f), new Vector3(0.25f, height, 0.25f), mTrim, false);
        LocalBox("Glow_L", a, new Vector3(-half + 0.14f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.1f), glow, false);
        LocalBox("Glow_R", a, new Vector3(half - 0.14f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.1f), glow, false);
        LocalBox("Glow_Bar", a, new Vector3(0f, height - 0.14f, 0f), new Vector3(width - 0.5f, 0.05f, 0.1f), glow, false);
    }

    // Orange wall-run panel on the inner face of a side wall (WallRunSurface).
    void WallRunPanel(string name, Transform parent, int side, float z0, float z1, float y0, float y1)
    {
        float len = z1 - z0;
        float h = y1 - y0;
        float x = side * (HalfW - 0.15f);

        GameObject panel = Box(name, parent, new Vector3(x, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, h, len), mWallRunPanel, true);
        NoShadows(panel);
        panel.AddComponent<WallRunSurface>();

        float face = side * (HalfW - 0.31f);
        Box(name + "_GlowTop", parent, new Vector3(face, y1 - 0.1f, (z0 + z1) * 0.5f), new Vector3(0.04f, 0.08f, len), mWallRunStrip, false);
        Box(name + "_GlowBottom", parent, new Vector3(face, y0 + 0.1f, (z0 + z1) * 0.5f), new Vector3(0.04f, 0.08f, len), mWallRunStrip, false);

        // Diagonal speed stripes so the panel reads at a glance.
        for (float z = z0 + 1f; z < z1 - 0.5f; z += 2f)
        {
            GameObject stripe = Box(name + "_Stripe", parent, new Vector3(face, (y0 + y1) * 0.5f, z), new Vector3(0.03f, h * 0.8f, 0.12f), mWallRunStrip, false);
            stripe.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
        }
    }

    // Full-width wall with a 1.1 m gap at the bottom: slide under it.
    void SlideBar(string name, Transform parent, float z, float floorY)
    {
        float bottom = floorY + 1.1f;
        float h = WallTop - bottom;

        GameObject bar = Box(name, parent, new Vector3(0f, bottom + h * 0.5f, z), new Vector3(HalfW * 2f, h, 1f), mSurfaceAlt, true);
        NoShadows(bar);
        Box(name + "_Glow", parent, new Vector3(0f, bottom - 0.02f, z - 0.5f), new Vector3(HalfW * 2f, 0.06f, 0.06f), mRoute, false);
        Box(name + "_Glow2", parent, new Vector3(0f, bottom + 0.4f, z - 0.51f), new Vector3(HalfW * 2f, 0.04f, 0.04f), mLaser, false);
    }

    void VaultBox(string name, Transform parent, Vector3 floorPos, float height)
    {
        Platform(name, parent, floorPos + Vector3.up * height, new Vector3(HalfW * 2f, height, 0.8f), mSurfaceAlt, true, false);
    }

    // A walkable block. 'top' is the centre of its TOP surface, so heights
    // in the layout code are the heights you actually stand at.
    // Returns the parent (unscaled) so children don't inherit the block scale.
    Transform Platform(string name, Transform parent, Vector3 top, Vector3 size,
                       Material mat = null, bool strips = true, bool support = true)
    {
        Transform p = NewChild(name, parent);
        p.position = top;

        LocalBox("Block", p, new Vector3(0f, -size.y * 0.5f, 0f), size, mat != null ? mat : mSurface, true);

        if (strips)
            EdgeStrips(p, size, mRoute);

        if (support)
        {
            float h = top.y - size.y - PitY;
            if (h > 0.5f)
            {
                float w = Mathf.Min(size.x, size.z) * 0.35f;
                LocalBox("Support", p, new Vector3(0f, -size.y - h * 0.5f, 0f), new Vector3(w, h, Mathf.Min(w, size.z * 0.6f)), mTrim, false);
            }
        }

        return p;
    }

    // Glowing strips around the top edges of a box whose top is at local y=0.
    void EdgeStrips(Transform p, Vector3 size, Material mat)
    {
        float y = -Strip * 0.5f + 0.005f;
        float hx = size.x * 0.5f;
        float hz = size.z * 0.5f;

        LocalBox("Edge_F", p, new Vector3(0f, y, hz), new Vector3(size.x + Strip, Strip, Strip), mat, false);
        LocalBox("Edge_B", p, new Vector3(0f, y, -hz), new Vector3(size.x + Strip, Strip, Strip), mat, false);
        LocalBox("Edge_L", p, new Vector3(-hx, y, 0f), new Vector3(Strip, Strip, size.z + Strip), mat, false);
        LocalBox("Edge_R", p, new Vector3(hx, y, 0f), new Vector3(Strip, Strip, size.z + Strip), mat, false);
    }

    // A ramp whose top surface runs from 'from' to 'to' (centre line).
    Transform Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width, float thickness = 0.5f)
    {
        Vector3 d = to - from;
        float len = d.magnitude;

        Transform p = NewChild(name, parent);
        p.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(d / len, Vector3.up));

        LocalBox("Block", p, new Vector3(0f, -thickness * 0.5f, 0f), new Vector3(width, thickness, len), mSurfaceAlt, true);

        float y = -Strip * 0.5f + 0.005f;
        LocalBox("Edge_L", p, new Vector3(-width * 0.5f, y, 0f), new Vector3(Strip, Strip, len), mRoute, false);
        LocalBox("Edge_R", p, new Vector3(width * 0.5f, y, 0f), new Vector3(Strip, Strip, len), mRoute, false);

        return p;
    }

    void Lamp(string name, Vector3 pos, Color color, float intensity, float range)
    {
        Light l = AddLight(lightsRoot, name, Vector3.zero, LightType.Point, color, intensity, range, 0f, false);
        l.transform.position = pos;
    }

    // Distant towers with neon strips: a skyline above the open roof.
    void BuildScenery()
    {
        Transform sc = NewChild("Scenery", root);
        System.Random rng = new System.Random(scenerySeed);
        Material[] glows = { mRoute, mRoute, mWallRunStrip, mSentry };

        for (float z = -20f; z < 280f; z += 11f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (16f + (float)rng.NextDouble() * 18f);
                float w = 3f + (float)rng.NextDouble() * 5f;
                float h = 20f + (float)rng.NextDouble() * 30f;
                float zz = z + (float)rng.NextDouble() * 6f;

                Transform t = NewChild("Tower", sc);
                t.position = new Vector3(x, SceneryFloorY, zz);

                LocalBox("Body", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, w), rng.NextDouble() < 0.5 ? mTrim : mSurfaceAlt, false);

                Material glow = glows[rng.Next(glows.Length)];
                LocalBox("Strip", t, new Vector3(-side * (w * 0.5f + 0.02f), h * 0.5f, 0f), new Vector3(0.06f, h, 0.18f), glow, false);
                LocalBox("Cap", t, new Vector3(0f, h + 0.03f, 0f), new Vector3(w + 0.05f, 0.06f, w + 0.05f), glow, false);
            }
        }
    }

    // ==================================================================
    // PLAYER + GAME
    // ==================================================================

    void BuildPlayer(int playerLayer)
    {
        GameObject player = new GameObject(PlayerName);
        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        player.transform.SetParent(transform);
        player.transform.SetPositionAndRotation(playerStart, Quaternion.identity);
        player.layer = playerLayer;

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.05f;
        cc.minMoveDistance = 0f; // >0 breaks isGrounded at low speeds

        GameObject cameraRoot = new GameObject("CameraRoot");
        Undo.RegisterCreatedObjectUndo(cameraRoot, "Create CameraRoot");
        cameraRoot.transform.SetParent(player.transform, false);
        cameraRoot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

        // Re-use the scene's Main Camera if there is one.
        Camera cam = Camera.main;

        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        else
        {
            // Remove old controller scripts from other versions of the level.
            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "CameraFollow")
                    Undo.DestroyObjectImmediate(mb);
        }

        Undo.SetTransformParent(cam.transform, cameraRoot.transform, "Parent Camera");
        Undo.RecordObject(cam, "Configure Camera");
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        cam.fieldOfView = 78f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = ParkourPalette.Sky;

        UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(camData, "Configure Camera Data");
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // UV flashlight: a violet spot on the camera.
        Light uv = AddLight(cam.transform, "UVLight", new Vector3(0.15f, -0.1f, 0f), LightType.Spot,
            new Color(0.62f, 0.4f, 1f), 14f, 16f, 50f, false);
        uv.innerSpotAngle = 30f;

        UVFlashlight fl = cam.gameObject.GetComponent<UVFlashlight>();
        if (fl == null) fl = Undo.AddComponent<UVFlashlight>(cam.gameObject);
        fl.spotLight = uv;

        ParkourPlayer motor = player.AddComponent<ParkourPlayer>();
        motor.cameraRoot = cameraRoot.transform;

        ParkourCamera look = player.AddComponent<ParkourCamera>();
        look.player = motor;
        look.cameraRoot = cameraRoot.transform;
        look.cam = cam;

        EditorUtility.SetDirty(player);
    }

    void BuildGame()
    {
        GameObject go = new GameObject(GameName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + GameName);
        go.transform.SetParent(transform);

        go.AddComponent<ParkourGameManager>();
        go.AddComponent<ParkourDebugDraw>();
        ParkourHUD hud = go.AddComponent<ParkourHUD>();
        if (TmpReady)
            hud.font = BuildHudFont();
    }

    // ==================================================================
    // LIGHTING + POST
    // ==================================================================

    void BuildLighting()
    {
        // Key light: re-use the scene's directional light if it has one.
        Light sun = null;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }

        if (sun == null)
        {
            GameObject go = new GameObject(SunName);
            Undo.RegisterCreatedObjectUndo(go, "Create Sun");
            go.transform.SetParent(transform);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        Undo.RecordObject(sun, "Configure Sun");
        Undo.RecordObject(sun.transform, "Configure Sun");
        sun.color = new Color(1f, 0.95f, 0.9f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        // Gradient ambient: cool sky, neutral sides, dark underneath, so the
        // tops of platforms read brighter than their sides (depth cue for jumps).
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.5f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.11f);
        RenderSettings.reflectionIntensity = 0.5f;

        // Light navy fog = distance reads as depth without hiding the route.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = ParkourPalette.Sky;
        RenderSettings.fogDensity = 0.01f;
        RenderSettings.skybox = null;
    }

    void BuildPostProcessing()
    {
        string path = GeneratedFolder + "/PostFX_Profile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(true);

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        Tonemapping tone = GetOrAdd<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(0.25f);
        color.contrast.Override(12f);
        color.saturation.Override(8f);
        color.colorFilter.Override(new Color(0.96f, 0.99f, 1f));

        EditorUtility.SetDirty(profile);

        // Other global volumes (e.g. from the scene template) would mix in
        // their own settings - switch them off so the look is predictable.
        foreach (Volume other in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (other.isGlobal && other.enabled)
            {
                Undo.RecordObject(other, "Disable Volume");
                other.enabled = false;
                Debug.Log("Disabled existing global Volume '" + other.name + "' so PostFX controls the look.");
            }
        }

        GameObject go = new GameObject(PostName);
        Undo.RegisterCreatedObjectUndo(go, "Create PostFX");
        go.transform.SetParent(transform);

        Volume v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 10f;
        v.sharedProfile = profile;
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T existing))
            return existing;

        T c = profile.Add<T>(true);
        c.name = typeof(T).Name;

        // Volume components are sub-assets of the profile; without this they
        // vanish when the project reloads.
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    // ==================================================================
    // MATERIALS
    // ==================================================================

    void CreateMaterials()
    {
        mSurface = MatAsset("Surface", ParkourPalette.Surface, 0f, 0.35f);
        mSurfaceAlt = MatAsset("SurfaceAlt", ParkourPalette.SurfaceAlt, 0f, 0.3f);
        mTrim = MatAsset("Trim", ParkourPalette.Trim, 0f, 0.6f);
        mVoid = MatAsset("Void", new Color(0.03f, 0.04f, 0.07f), 0f, 0.85f);

        mRoute = MatAsset("RouteGlow", ParkourPalette.Route, 4f, 0.5f);
        mWallRunPanel = MatAsset("WallRunPanel", ParkourPalette.WallRun, 0.35f, 0.4f);
        mWallRunStrip = MatAsset("WallRunGlow", ParkourPalette.WallRun, 4f, 0.5f);
        mFinish = MatAsset("Finish", ParkourPalette.Finish, 3f, 0.5f);
        mLaser = MatAsset("Laser", ParkourPalette.Laser, 6f, 0.5f);
        mPitGlow = MatAsset("PitGlow", ParkourPalette.Laser, 2.5f, 0.5f);
        mSentry = MatAsset("Sentry", ParkourPalette.Sentry, 5f, 0.5f);
        mUV = MatAsset("UVGlow", ParkourPalette.UV, 5f, 0.5f);

        mLaserHalo = TransparentMat("LaserHalo", ParkourPalette.Laser, 0.22f, 1.5f);
        mUVHalo = TransparentMat("UVHalo", ParkourPalette.UV, 0.22f, 1.5f);
    }

    static Material MatAsset(string name, Color color, float emission, float smoothness)
    {
        Material m = LoadOrCreateMat(name);

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        SetEmission(m, color, emission);

        EditorUtility.SetDirty(m);
        return m;
    }

    // Soft glowing halo (alpha-blended, no depth write) around lasers.
    static Material TransparentMat(string name, Color color, float alpha, float emission)
    {
        Material m = LoadOrCreateMat(name);

        m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
        m.SetFloat("_Smoothness", 0.2f);
        m.SetFloat("_Surface", 1f); // Transparent
        m.SetFloat("_Blend", 0f);   // Alpha
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        SetEmission(m, color, emission);

        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreateMat(string name)
    {
        string path = GeneratedFolder + "/M_" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }

        m.shader = shader;
        return m;
    }

    static void SetEmission(Material m, Color color, float emission)
    {
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
    }

    // ==================================================================
    // LOW-LEVEL HELPERS
    // ==================================================================

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("Couldn't open TagManager. Add a layer named '" + layerName + "' manually.");
            return 0;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 6; i < 32; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);

            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log("Created layer '" + layerName + "' at index " + i + ".");
                return i;
            }
        }

        Debug.LogError("No free layer slot for '" + layerName + "'.");
        return 0;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void NoShadows(GameObject go)
    {
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    Transform NewChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // World-space box. No collider = decor (no shadow either).
    GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;

        if (!collider)
        {
            DestroyImmediate(go.GetComponent<Collider>());
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        return go;
    }

    // Local-space box.
    GameObject LocalBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
    {
        return LocalPrim(PrimitiveType.Cube, name, parent, localPos, scale, mat, Quaternion.identity, collider);
    }

    GameObject LocalPrim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
                         Material mat, Quaternion localRot, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;

        if (!collider)
        {
            DestroyImmediate(go.GetComponent<Collider>());
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        return go;
    }

    Light AddLight(Transform parent, string name, Vector3 localPos, LightType type, Color color,
                   float intensity, float range, float spotAngle, bool shadows)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        Light l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        if (type == LightType.Spot) l.spotAngle = spotAngle;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    // ==================================================================
    // CLEAR
    // ==================================================================

    public void ClearGeneratedLevel()
    {
        // Keep the camera: un-parent it from the player before deleting.
        Transform oldPlayer = transform.Find(PlayerName);

        if (oldPlayer != null)
        {
            Camera cam = oldPlayer.GetComponentInChildren<Camera>(true);
            if (cam != null)
                Undo.SetTransformParent(cam.transform, null, "Unparent Camera");
        }

        foreach (string n in new[] { RootName, PlayerName, GameName, PostName })
        {
            Transform t = transform.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // The UV light lived on the camera; remove it so it isn't duplicated.
        if (Camera.main != null)
        {
            Transform beam = Camera.main.transform.Find("UVLight");
            if (beam != null) Undo.DestroyObjectImmediate(beam.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

#endif
}

// ======================================================================
// CUSTOM INSPECTOR
// ======================================================================

#if UNITY_EDITOR

[CustomEditor(typeof(ParkourCourseBuilder))]
public class ParkourCourseBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(15);

        ParkourCourseBuilder builder = (ParkourCourseBuilder)target;

        GUI.backgroundColor = new Color(0.3f, 0.95f, 1f);
        if (GUILayout.Button("BUILD COURSE", GUILayout.Height(45)))
            builder.BuildCourse();

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("CLEAR GENERATED COURSE", GUILayout.Height(30)))
            builder.ClearGeneratedLevel();

        GUI.backgroundColor = Color.white;
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Builds the neon laser corridor as normal scene objects. " +
            "Creates the 'Player' layer if needed, and saves materials + the " +
            "post-processing profile in Assets/Parkour/Generated. Save the scene afterwards.",
            MessageType.Info);
    }
}

#endif
