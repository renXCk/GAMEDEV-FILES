using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.Shapes;

public static class ShootingRangeSceneBuilder
{
    const string SceneFolder = "Assets/Scenes";
    static Material floorMat, wallMat, boothMat, targetMat, trimMat, ballMat, uiMat;
    const string SettingsFolder = "Assets/Settings";
    const string PipelinePath = "Assets/Settings/ShootingRangeURP.asset";
    const string RendererPath = "Assets/Settings/ShootingRangeRenderer.asset";

    [MenuItem("Tools/Build Shooting Range Scenes")]
    public static void BuildBoth()
    {
        EnsureFolder(SceneFolder);
        EnsureURPAsset();
        CreateMaterials();
        BuildScene("StaticTargets", false, 60f);
        BuildScene("MovingTargets", true, 90f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/StaticTargets.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MovingTargets.unity", true)
        };
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorUtility.DisplayDialog("Shooting Range", "Both scenes were generated and added to Build Settings.", "Great");
    }

    static void BuildScene(string sceneName, bool moving, float timeLimit)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = sceneName;

        // ProBuilder primitives for all architectural and target geometry.
        MakeCube("Range Floor", new Vector3(0, -0.25f, 15f), new Vector3(12f, 0.5f, 34f), floorMat);
        MakeCube("Back Wall", new Vector3(0, 3.2f, 32f), new Vector3(12f, 6.5f, 0.5f), wallMat);
        MakeCube("Left Wall", new Vector3(-6f, 3f, 15f), new Vector3(0.4f, 6f, 34f), wallMat);
        MakeCube("Right Wall", new Vector3(6f, 3f, 15f), new Vector3(0.4f, 6f, 34f), wallMat);
        MakeCube("Ceiling", new Vector3(0, 6.2f, 15f), new Vector3(12f, 0.3f, 34f), wallMat);
        MakeCube("Booth Counter", new Vector3(0, 1.05f, 1.6f), new Vector3(8f, 0.35f, 1.2f), boothMat);
        MakeCube("Booth Front", new Vector3(0, 0.45f, 0.8f), new Vector3(8f, 0.9f, 0.25f), boothMat);
        MakeCube("Booth Left Rail", new Vector3(-3.8f, 0.6f, 1.5f), new Vector3(0.12f, 1.2f, 1.4f), trimMat);
        MakeCube("Booth Right Rail", new Vector3(3.8f, 0.6f, 1.5f), new Vector3(0.12f, 1.2f, 1.4f), trimMat);

        // Lights and ambient illumination.
        GameObject lightObj = new GameObject("Range Key Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObj.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.7f, 1.1f);
        RailPlayerController controller = player.AddComponent<RailPlayerController>();
        controller.railMinX = -3.3f;
        controller.railMaxX = 3.3f;
        GameObject cameraObj = new GameObject("Aim Camera");
        cameraObj.tag = "MainCamera";
        cameraObj.transform.SetParent(player.transform, false);
        cameraObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.fieldOfView = 68f;
        cam.nearClipPlane = 0.05f;
        cameraObj.AddComponent<AudioListener>();
        controller.cameraPivot = cameraObj.transform;

        GameObject gmObj = new GameObject("GameManager");
        GameManager gm = gmObj.AddComponent<GameManager>();
        gm.timeLimit = timeLimit;
        gm.totalTargets = 8;

        UIManager ui = BuildUI();
        gm.uiManager = ui;
        BallShooter shooter = player.AddComponent<BallShooter>();
        shooter.aimCamera = cam;
        shooter.gameManager = gm;
        shooter.uiManager = ui;
        shooter.ballMaterial = ballMat;

        // Eight target stands and ProBuilder target plates.
        float[] zs = { 7f, 10.5f, 14f, 17.5f, 21f, 24.5f, 28f, 30f };
        float[] xs = { -2.8f, 2.6f, -1.7f, 1.9f, -3.0f, 2.7f, -1.0f, 1.1f };
        float[] ys = { 1.1f, 1.8f, 2.1f, 1.3f, 2.5f, 1.7f, 2.3f, 1.4f };
        for (int i = 0; i < 8; i++)
        {
            float standHeight = ys[i] - 0.45f;
            MakeCube($"Stand {i + 1}", new Vector3(xs[i], standHeight / 2f, zs[i]), new Vector3(0.7f, standHeight, 0.7f), trimMat);
            GameObject target = MakeCube($"Target {i + 1}", new Vector3(xs[i], ys[i], zs[i]), new Vector3(0.85f, 0.95f, 0.22f), targetMat);
            target.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Rigidbody rb = target.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            BoxCollider col = target.GetComponent<BoxCollider>();
            if (col == null) target.AddComponent<BoxCollider>();
            Target t = target.AddComponent<Target>();
            t.gameManager = gm;
            t.belowHeight = -0.5f;
            if (moving)
            {
                t.physicsOnHit = true;
                t.hitThreshold = 2.0f;
                MovingTarget mt = target.AddComponent<MovingTarget>();
                mt.motion = (i % 2 == 0) ? MovingTarget.Motion.SideToSide : MovingTarget.Motion.UpAndDown;
                mt.speed = Random.Range(0.7f, 1.7f);
                mt.distance = Random.Range(0.45f, 1.15f);
                mt.phase = Random.Range(0f, 6.28f);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{sceneName}.unity");
    }

    static GameObject MakeCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, scale);
        mesh.gameObject.name = name;
        mesh.transform.position = position;
        mesh.GetComponent<Renderer>().sharedMaterial = material;
        mesh.ToMesh();
        mesh.Refresh();
        return mesh.gameObject;
    }

    static UIManager BuildUI()
    {
        GameObject canvasObj = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        Text timer = MakeText(canvasObj.transform, "Timer", "TIME  60", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), 28, TextAnchor.UpperLeft);
        Text targets = MakeText(canvasObj.transform, "Targets", "TARGETS  0/8", new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -62), 22, TextAnchor.UpperLeft);
        Text shots = MakeText(canvasObj.transform, "Shots", "SHOTS  0", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), 22, TextAnchor.UpperRight);
        Text ammo = MakeText(canvasObj.transform, "Ammo", "AMMO  10 / 10", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 26), 24, TextAnchor.LowerRight);

        Text crosshair = MakeText(canvasObj.transform, "Crosshair", "+", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 34, TextAnchor.MiddleCenter);
        crosshair.color = new Color(0.7f, 1f, 0.85f);

        GameObject panel = new GameObject("Result Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(520, 270); pr.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.06f, 0.94f);
        Text title = MakeText(panel.transform, "Result Title", "RANGE CLEARED!", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -34), 32, TextAnchor.MiddleCenter);
        Text details = MakeText(panel.transform, "Result Details", "Press T to restart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 21, TextAnchor.MiddleCenter);
        panel.SetActive(false);

        UIManager ui = canvasObj.AddComponent<UIManager>();
        ui.timerText = timer; ui.targetsText = targets; ui.shotsText = shots; ui.ammoText = ammo;
        ui.resultPanel = panel; ui.resultTitle = title; ui.resultDetails = details;
        return ui;
    }

    static Text MakeText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, int fontSize, TextAnchor align)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x == 1f ? 1f : anchorMin.x, anchorMin.y == 1f ? 1f : anchorMin.y);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(360, 46);
        Text text = obj.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }

    // Creates (or repairs) a real URP asset. Do not use LoadBuiltinRendererData here:
    // that can return a renderer object that AssetDatabase refuses to create as a new asset.
    // Instead, create/load a persistent UniversalRendererData asset and wire it into the pipeline.
    static UniversalRenderPipelineAsset EnsureURPAsset()
    {
        EnsureFolder(SettingsFolder);

        UniversalRendererData rendererData = LoadOrCleanAsset<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "ShootingRangeRenderer";
            AssetDatabase.CreateAsset(rendererData, RendererPath);
            AssetDatabase.SaveAssets();
        }

        UniversalRenderPipelineAsset pipeline = LoadOrCleanAsset<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.name = "ShootingRangeURP";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        // Explicitly assign the renderer even when repairing an existing pipeline asset.
        SerializedObject serializedPipeline = new SerializedObject(pipeline);
        SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
        if (rendererList != null)
        {
            if (rendererList.arraySize < 1) rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
        }
        SerializedProperty defaultRendererIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex");
        if (defaultRendererIndex != null) defaultRendererIndex.intValue = 0;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        EditorUtility.SetDirty(rendererData);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        // Clear the active quality tier override so it inherits the project-wide URP asset.
        QualitySettings.renderPipeline = null;
        AssetDatabase.SaveAssets();
        return pipeline;
    }

    static T LoadOrCleanAsset<T>(string assetPath) where T : UnityEngine.Object
    {
        T typedAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (typedAsset != null) return typedAsset;

        // Recover from an orphan or wrong-type asset left by an earlier failed attempt.
        UnityEngine.Object otherAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (otherAsset != null || File.Exists(assetPath) || File.Exists(assetPath + ".meta"))
            AssetDatabase.DeleteAsset(assetPath);
        return null;
    }

    [MenuItem("Tools/Fix URP Rendering and Pink Materials")]
    public static void FixURPRenderingAndMaterials()
    {
        EnsureURPAsset();
        CreateMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("URP Materials", "URP is assigned and the shooting-range materials were reset to URP/Lit. Existing scenes should now render with their intended colors. If not, check Project Settings > Graphics and ensure the ShootingRangeURP asset is assigned.", "OK");
    }

    static void CreateMaterials()
    {
        floorMat = CreateMat("RangeFloor", new Color(0.13f, 0.16f, 0.18f));
        wallMat = CreateMat("RangeWall", new Color(0.055f, 0.075f, 0.09f));
        boothMat = CreateMat("BoothBlue", new Color(0.07f, 0.23f, 0.32f));
        targetMat = CreateMat("TargetOrange", new Color(1f, 0.36f, 0.08f));
        trimMat = CreateMat("SteelTrim", new Color(0.34f, 0.42f, 0.46f));
        ballMat = CreateMat("BallCyan", new Color(0.1f, 0.9f, 1f));
    }

    static Material CreateMat(string name, Color color)
    {
        string path = $"Assets/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new System.InvalidOperationException(
                "Shader 'Universal Render Pipeline/Lit' was not found. Check that com.unity.render-pipelines.universal is installed and has finished importing.");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            // Reassign old Standard/fallback shaders, which are a common cause of pink materials.
            mat.shader = shader;
        }

        mat.name = name;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
