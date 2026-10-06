using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Editor-side level generator.
//
// This creates REAL GameObjects in the Scene.
// They are not temporary runtime objects.
//
// After pressing BUILD LEVEL:
// - Floor exists permanently
// - Walls exist permanently
// - Goal exists permanently
// - Bonus panels exist permanently
// - Player exists permanently
// - Camera exists permanently
//
// You can then edit everything normally in the Unity Editor.
public class LaserCorridorBuilder : MonoBehaviour
{
    [Header("Corridor")]
    public float corridorLength = 40f;
    public float corridorWidth = 8f;
    public float wallHeight = 4f;

    [Header("Bonus Panels")]
    public int panelCount = 6;

    [Header("Player")]
    public Vector3 playerStartPosition =
        new Vector3(0f, 1f, 2f);

    // ------------------------------------------------------------
    // BUILD
    // ------------------------------------------------------------

    [ContextMenu("BUILD LEVEL")]
    public void BuildLevel()
    {
#if UNITY_EDITOR

        ClearGeneratedLevel();

        // --------------------------------------------------------
        // ENVIRONMENT ROOT
        // --------------------------------------------------------

        GameObject environment =
            new GameObject("LaserCorridor");

        Undo.RegisterCreatedObjectUndo(
            environment,
            "Create Laser Corridor"
        );

        environment.transform.SetParent(
            transform
        );

        float half =
            corridorWidth * 0.5f;

        Color wallColor =
            new Color(
                0.22f,
                0.24f,
                0.3f
            );

        // --------------------------------------------------------
        // FLOOR
        // --------------------------------------------------------

        CreateCube(
            "Floor",
            new Vector3(
                0f,
                -0.25f,
                corridorLength * 0.5f
            ),
            new Vector3(
                corridorWidth,
                0.5f,
                corridorLength
            ),
            new Color(
                0.14f,
                0.14f,
                0.16f
            ),
            true,
            false,
            environment.transform
        );

        // --------------------------------------------------------
        // LEFT WALL
        // --------------------------------------------------------

        CreateCube(
            "Wall_Left",
            new Vector3(
                -half - 0.25f,
                wallHeight * 0.5f,
                corridorLength * 0.5f
            ),
            new Vector3(
                0.5f,
                wallHeight,
                corridorLength + 1f
            ),
            wallColor,
            true,
            false,
            environment.transform
        );

        // --------------------------------------------------------
        // RIGHT WALL
        // --------------------------------------------------------

        CreateCube(
            "Wall_Right",
            new Vector3(
                half + 0.25f,
                wallHeight * 0.5f,
                corridorLength * 0.5f
            ),
            new Vector3(
                0.5f,
                wallHeight,
                corridorLength + 1f
            ),
            wallColor,
            true,
            false,
            environment.transform
        );

        // --------------------------------------------------------
        // START WALL
        // --------------------------------------------------------

        CreateCube(
            "Wall_Start",
            new Vector3(
                0f,
                wallHeight * 0.5f,
                -0.25f
            ),
            new Vector3(
                corridorWidth + 1f,
                wallHeight,
                0.5f
            ),
            wallColor,
            true,
            false,
            environment.transform
        );

        // --------------------------------------------------------
        // END WALL
        // --------------------------------------------------------

        CreateCube(
            "Wall_End",
            new Vector3(
                0f,
                wallHeight * 0.5f,
                corridorLength + 0.25f
            ),
            new Vector3(
                corridorWidth + 1f,
                wallHeight,
                0.5f
            ),
            wallColor,
            true,
            false,
            environment.transform
        );

        // --------------------------------------------------------
        // GOAL PAD
        // --------------------------------------------------------

        GameObject goal =
            CreateCube(
                "GoalPad",
                new Vector3(
                    0f,
                    0.025f,
                    corridorLength - 1.5f
                ),
                new Vector3(
                    corridorWidth,
                    0.05f,
                    3f
                ),
                new Color(
                    0.2f,
                    1f,
                    0.4f
                ),
                false,
                true,
                environment.transform
            );

        // --------------------------------------------------------
        // BONUS PANELS
        // --------------------------------------------------------

        GameObject panelRoot =
            new GameObject("BonusPanels");

        Undo.RegisterCreatedObjectUndo(
            panelRoot,
            "Create Bonus Panels"
        );

        panelRoot.transform.SetParent(
            transform
        );

        for (int i = 0; i < panelCount; i++)
        {
            float t =
                (i + 0.5f) /
                Mathf.Max(1, panelCount);

            float z =
                Mathf.Lerp(
                    6f,
                    corridorLength - 8f,
                    t
                );

            float x =
                Random.Range(
                    -corridorWidth * 0.5f + 1.5f,
                    corridorWidth * 0.5f - 1.5f
                );

            BonusType type =
                (BonusType)(i % 3);

            Color color =
                GetBonusColor(type);

            GameObject panel =
                CreateCube(
                    "Panel_" + type,
                    new Vector3(
                        x,
                        0.025f,
                        z
                    ),
                    new Vector3(
                        2f,
                        0.05f,
                        2f
                    ),
                    color,
                    false,
                    true,
                    panelRoot.transform
                );

            // ----------------------------------------------------
            // BONUS PANEL COMPONENT
            // ----------------------------------------------------

            BonusPanel bonusPanel =
                panel.AddComponent<BonusPanel>();

            bonusPanel.type = type;
        }

        // --------------------------------------------------------
        // PLAYER
        // --------------------------------------------------------

        CreatePlayer();

        // --------------------------------------------------------
        // CAMERA
        // --------------------------------------------------------

        CreateCamera();

        // --------------------------------------------------------
        // FINISH
        // --------------------------------------------------------

        EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

        Selection.activeGameObject =
            environment;

        Debug.Log(
            "========================================\n" +
            "LASER CORRIDOR LEVEL BUILT!\n" +
            "========================================\n" +
            "The level is now made of real Scene objects.\n" +
            "You can edit the floor, walls and panels.\n" +
            "Remember to SAVE the Scene.\n" +
            "========================================"
        );

#endif
    }

    // ------------------------------------------------------------
    // PLAYER
    // ------------------------------------------------------------

#if UNITY_EDITOR

    void CreatePlayer()
{
    // Don't create another player if one already exists.
    PlayerController existing =
        FindFirstObjectByType<PlayerController>();

    if (existing != null)
    {
        Debug.Log(
            "Player already exists. " +
            "Using existing Player."
        );

        return;
    }

    // --------------------------------------------------------
    // PLAYER ROOT
    // --------------------------------------------------------

    GameObject player =
        new GameObject("Player");

    Undo.RegisterCreatedObjectUndo(
        player,
        "Create Player"
    );

    player.transform.SetParent(transform);

    player.transform.position =
        playerStartPosition;

    player.transform.rotation =
        Quaternion.identity;

    // --------------------------------------------------------
    // CHARACTER CONTROLLER
    // --------------------------------------------------------

    CharacterController controller =
        player.AddComponent<CharacterController>();

    controller.height = 2f;
    controller.radius = 0.5f;

    controller.center =
        new Vector3(
            0f,
            1f,
            0f
        );

    controller.stepOffset = 0.3f;
    controller.slopeLimit = 45f;

    // --------------------------------------------------------
    // PLAYER VISUAL
    // --------------------------------------------------------

    GameObject visual =
        GameObject.CreatePrimitive(
            PrimitiveType.Capsule
        );

    Undo.RegisterCreatedObjectUndo(
        visual,
        "Create Player Visual"
    );

    visual.name = "PlayerVisual";

    visual.transform.SetParent(
        player.transform
    );

    visual.transform.localPosition =
        new Vector3(
            0f,
            1f,
            0f
        );

    visual.transform.localRotation =
        Quaternion.identity;

    visual.transform.localScale =
        Vector3.one;

    // Remove the visual capsule's collider.
    CapsuleCollider capsuleCollider =
        visual.GetComponent<CapsuleCollider>();

    if (capsuleCollider != null)
    {
        DestroyImmediate(
            capsuleCollider
        );
    }

    // --------------------------------------------------------
    // PLAYER COLOR
    // --------------------------------------------------------

    Renderer renderer =
        visual.GetComponent<Renderer>();

    renderer.material.color =
        new Color(
            0.2f,
            0.5f,
            1f
        );

    // --------------------------------------------------------
    // PLAYER SCRIPTS
    // --------------------------------------------------------

    player.AddComponent<PlayerController>();
    player.AddComponent<PlayerHealth>();

    EditorUtility.SetDirty(player);
}

    // ------------------------------------------------------------
    // CAMERA
    // ------------------------------------------------------------

    void CreateCamera()
    {
        Camera existing =
            Camera.main;

        if (existing != null)
        {
            CameraFollow follow =
                existing.GetComponent<CameraFollow>();

            if (follow == null)
                follow =
                    existing.gameObject
                        .AddComponent<CameraFollow>();

            PlayerController player =
                FindFirstObjectByType<PlayerController>();

            if (player != null)
                follow.target =
                    player.transform;

            return;
        }

        GameObject cameraObject =
            new GameObject("Main Camera");

        Undo.RegisterCreatedObjectUndo(
            cameraObject,
            "Create Main Camera"
        );

        cameraObject.tag =
            "MainCamera";

        Camera camera =
            cameraObject.AddComponent<Camera>();

        cameraObject.AddComponent<AudioListener>();

        CameraFollow cameraFollow =
            cameraObject.AddComponent<CameraFollow>();

        PlayerController playerController =
            FindFirstObjectByType<PlayerController>();

        if (playerController != null)
        {
            cameraFollow.target =
                playerController.transform;

            cameraObject.transform.position =
                playerController.transform.position +
                cameraFollow.offset;

            cameraObject.transform.rotation =
                Quaternion.LookRotation(
                    Vector3.up -
                    cameraFollow.offset
                );
        }
    }

    // ------------------------------------------------------------
    // CREATE CUBE
    // ------------------------------------------------------------

    GameObject CreateCube(
        string objectName,
        Vector3 position,
        Vector3 scale,
        Color color,
        bool keepCollider,
        bool glow,
        Transform parent
    )
    {
        GameObject go =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create " + objectName
        );

        go.name =
            objectName;

        go.transform.SetParent(
            parent
        );

        go.transform.position =
            position;

        go.transform.localScale =
            scale;

        // --------------------------------------------------------
        // COLLIDER
        // --------------------------------------------------------

        if (!keepCollider)
        {
            Collider collider =
                go.GetComponent<Collider>();

            if (collider != null)
            {
                DestroyImmediate(
                    collider
                );
            }
        }

        // --------------------------------------------------------
        // MATERIAL
        // --------------------------------------------------------

        Renderer renderer =
            go.GetComponent<Renderer>();

        renderer.material.color =
            color;

        if (glow)
        {
            renderer.material.EnableKeyword(
                "_EMISSION"
            );

            renderer.material.SetColor(
                "_EmissionColor",
                color * 2f
            );
        }

        return go;
    }

    // ------------------------------------------------------------
    // COLORS
    // ------------------------------------------------------------

    Color GetBonusColor(
        BonusType type
    )
    {
        switch (type)
        {
            case BonusType.Heal:

                return new Color(
                    0.2f,
                    0.9f,
                    0.3f
                );

            case BonusType.Speed:

                return new Color(
                    0.2f,
                    0.6f,
                    1f
                );

            case BonusType.Shield:

                return new Color(
                    1f,
                    0.9f,
                    0.2f
                );

            default:

                return Color.white;
        }
    }

    // ------------------------------------------------------------
    // CLEAR
    // ------------------------------------------------------------

    public void ClearGeneratedLevel()
    {
#if UNITY_EDITOR

        Transform corridor =
            transform.Find(
                "LaserCorridor"
            );

        if (corridor != null)
        {
            Undo.DestroyObjectImmediate(
                corridor.gameObject
            );
        }

        Transform panels =
            transform.Find(
                "BonusPanels"
            );

        if (panels != null)
        {
            Undo.DestroyObjectImmediate(
                panels.gameObject
            );
        }

        // Player
        Transform player =
            transform.Find(
                "Player"
            );

        if (player != null)
        {
            Undo.DestroyObjectImmediate(
                player.gameObject
            );
        }

        // Camera is normally not parented to LevelBuilder,
        // so don't automatically delete it here.
        //
        // This means CLEAR LEVEL doesn't unexpectedly delete
        // a camera you may have manually configured.

        EditorSceneManager.MarkSceneDirty(
            gameObject.scene
        );

#endif
    }

#endif
}

// ================================================================
// CUSTOM INSPECTOR
// ================================================================

#if UNITY_EDITOR

[CustomEditor(typeof(LaserCorridorBuilder))]
public class LaserCorridorBuilderEditor
    : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(15);

        LaserCorridorBuilder builder =
            (LaserCorridorBuilder)target;

        // --------------------------------------------------------
        // BUILD
        // --------------------------------------------------------

        GUI.backgroundColor =
            new Color(
                0.3f,
                1f,
                0.4f
            );

        if (
            GUILayout.Button(
                "BUILD LEVEL",
                GUILayout.Height(45)
            )
        )
        {
            builder.BuildLevel();
        }

        // --------------------------------------------------------
        // CLEAR
        // --------------------------------------------------------

        GUI.backgroundColor =
            new Color(
                1f,
                0.4f,
                0.4f
            );

        if (
            GUILayout.Button(
                "CLEAR GENERATED LEVEL",
                GUILayout.Height(30)
            )
        )
        {
            builder.ClearGeneratedLevel();
        }

        GUI.backgroundColor =
            Color.white;

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "BUILD LEVEL creates permanent Scene objects. " +
            "After building, you can move, scale, rotate, " +
            "and edit the generated objects normally. " +
            "Remember to save your Scene.",
            MessageType.Info
        );
    }
}

#endif