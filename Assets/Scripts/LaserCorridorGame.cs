using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runtime game manager.
// The corridor, player, camera, and bonus panels are created permanently
// in the Unity Editor by LaserCorridorBuilder.
//
// This script is responsible for:
// - Connecting the scene objects together
// - Spawning lasers
// - Applying bonus effects
// - Health / game over
// - Win condition
// - Resetting the game
// - HUD
public class LaserCorridorGame : MonoBehaviour
{
    public enum State { Playing, GameOver, Won }

    [Header("Corridor")]
    public float corridorLength = 40f;
    public float corridorWidth = 8f;
    public float wallHeight = 4f;

    [Header("Lasers")]
    public float laserSpeed = 5f;
    public float laserDamage = 20f;
    public float gapWidth = 3f;

    [Tooltip("Z position where new lasers appear.")]
    public float laserSpawnZ = 39f;

    public Vector2 spawnIntervalRange =
    new Vector2(0.1f, 0.5f);

    [Header("Bonus Panels")]
    public float healAmount = 25f;
    public float speedMultiplier = 1.6f;
    public float speedDuration = 5f;
    public float shieldDuration = 5f;

    [Header("Game")]
    public float resetDelay = 2.5f;
    public float winDelay = 3f;

    private PlayerController player;
    private CharacterController body;
    private PlayerHealth health;

    private Transform lasersRoot;
    private readonly List<BonusPanel> panels = new List<BonusPanel>();

    private Vector3 startPos;
    private float goalLineZ;
    private float spawnTimer;

    private State state = State.Playing;

    private string toast = "";
    private float toastTimer;

    private GUIStyle labelStyle;
    private GUIStyle bigStyle;

    // ------------------------------------------------------------
    // SETUP
    // ------------------------------------------------------------

    void Awake()
    {
        // IMPORTANT:
        // We no longer build the corridor here.
        // The corridor already exists in the saved Scene.

        FindSceneObjects();
        SetupCamera();
        SetupPanels();

        lasersRoot = FindOrCreateLasersRoot();

        goalLineZ = corridorLength - 3f;
        spawnTimer = 0f;
    }

    void OnDestroy()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }

    void FindSceneObjects()
    {
        // Find the permanent Player GameObject created by the builder.
        player = FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            Debug.LogError(
                "LaserCorridorGame: No PlayerController found. " +
                "Select LevelBuilder and press BUILD LEVEL."
            );

            enabled = false;
            return;
        }

        body = player.GetComponent<CharacterController>();

        if (body == null)
        {
            Debug.LogError(
                "LaserCorridorGame: Player is missing CharacterController."
            );

            enabled = false;
            return;
        }

        health = player.GetComponent<PlayerHealth>();

        if (health == null)
            health = player.gameObject.AddComponent<PlayerHealth>();

        health.Died -= HandleDeath;
        health.Died += HandleDeath;

        // Starting position is based on the actual Player object
        // currently saved in the Scene.
        startPos = player.transform.position;

        // The level builder normally creates the player at this position.
        // Keeping it scene-based means YOU can move the player wherever
        // you want in the editor and that becomes the starting point.
    }

    void SetupCamera()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogWarning(
                "LaserCorridorGame: No Main Camera found."
            );

            return;
        }

        CameraFollow follow = cam.GetComponent<CameraFollow>();

        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        follow.target = player.transform;
    }

    void SetupPanels()
    {
        panels.Clear();

        BonusPanel[] scenePanels =
            FindObjectsByType<BonusPanel>(
                FindObjectsSortMode.None
            );

        foreach (BonusPanel panel in scenePanels)
        {
            // Initialize the panel with the actual permanent player.
            panel.Init(
                panel.type,
                panel.GetComponent<Renderer>().material.color,
                body,
                OnPanelCollected
            );

            panels.Add(panel);
        }

        Debug.Log(
            "LaserCorridorGame: Connected " +
            panels.Count +
            " bonus panels."
        );
    }

    Transform FindOrCreateLasersRoot()
    {
        GameObject existing = GameObject.Find("Lasers");

        if (existing != null)
            return existing.transform;

        GameObject go = new GameObject("Lasers");
        return go.transform;
    }

    // ------------------------------------------------------------
    // GAME LOOP
    // ------------------------------------------------------------

    void Update()
    {
        if (toastTimer > 0f)
            toastTimer -= Time.deltaTime;

        if (state != State.Playing)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnLaser();

            spawnTimer = Random.Range(
                spawnIntervalRange.x,
                spawnIntervalRange.y
            );
        }

        if (player != null &&
            player.transform.position.z >= goalLineZ)
        {
            HandleWin();
        }
    }

    // ------------------------------------------------------------
    // LASERS
    // ------------------------------------------------------------

    void SpawnLaser()
{
    if (player == null || health == null || health.IsDead)
        return;

    float half = corridorWidth * 0.5f;
    float z = laserSpawnZ;

    int pattern = Random.Range(0, 8);

    switch (pattern)
    {
        // -------------------------------------------------
        // 0. LOW HORIZONTAL LASER
        // Jump over it
        // -------------------------------------------------
        case 0:
        {
            GameObject laser = CreateLaser(
                new Vector3(0f, 0.9f, z),
                new Vector3(corridorWidth - 0.5f, 0.18f, 0.18f)
            );

            break;
        }

        // -------------------------------------------------
        // 1. HIGH HORIZONTAL LASER
        // Stay low / run underneath
        // -------------------------------------------------
        case 1:
        {
            GameObject laser = CreateLaser(
                new Vector3(0f, 2.4f, z),
                new Vector3(corridorWidth - 0.5f, 0.18f, 0.18f)
            );

            break;
        }

        // -------------------------------------------------
        // 2. LEFT LASER
        // Dodge right
        // -------------------------------------------------
        case 2:
        {
            GameObject laser = CreateLaser(
                new Vector3(-half + 1.0f, 1.2f, z),
                new Vector3(7f, 2.4f, 0.18f)
            );

            break;
        }

        // -------------------------------------------------
        // 3. RIGHT LASER
        // Dodge left
        // -------------------------------------------------
        case 3:
        {
            GameObject laser = CreateLaser(
                new Vector3(half - 1.0f, 1.2f, z),
                new Vector3(7f, 2.4f, 0.18f)
            );

            break;
        }

        // -------------------------------------------------
        // 4. TWO SIDE LASERS
        // Stay in the middle
        // -------------------------------------------------
        case 4:
        {
            CreateLaser(
                new Vector3(-half + 0.8f, 1.2f, z),
                new Vector3(4f, 2.4f, 0.18f)
            );

            CreateLaser(
                new Vector3(half - 0.8f, 1.2f, z),
                new Vector3(4f, 2.4f, 0.18f)
            );

            break;
        }

        // -------------------------------------------------
// 5. CENTER HORIZONTAL LASER
// Leaves left/right sides open
// -------------------------------------------------
case 5:
{
    CreateLaser(
        new Vector3(0f, 1.3f, z),
        new Vector3(3.5f, 0.18f, 0.18f)
    );

    break;
}


// -------------------------------------------------
// 6. THREE VERTICAL LASERS
// Left + center + right
// -------------------------------------------------
case 6:
{
    CreateLaser(
        new Vector3(-2.5f, 1.2f, z),
        new Vector3(0.18f, 2.4f, 0.18f)
    );

    CreateLaser(
        new Vector3(0f, 1.2f, z),
        new Vector3(0.18f, 2.4f, 0.18f)
    );

    CreateLaser(
        new Vector3(2.5f, 1.2f, z),
        new Vector3(0.18f, 2.4f, 0.18f)
    );

    break;
}


// -------------------------------------------------
// 7. LOW + HIGH COMBO
// Jump OR move under/around depending on height
// -------------------------------------------------
case 7:
{
    CreateLaser(
        new Vector3(-2.0f, 0.8f, z),
        new Vector3(3.5f, 0.18f, 0.18f)
    );

    CreateLaser(
        new Vector3(2.0f, 2.6f, z),
        new Vector3(3.5f, 0.18f, 0.18f)
    );

    break;
}
    }
}

GameObject CreateLaser(Vector3 position, Vector3 scale)
{
    GameObject laser = GameObject.CreatePrimitive(PrimitiveType.Cube);

    laser.name = "Laser";

    laser.transform.position = position;
    laser.transform.localScale = scale;

    Renderer rend = laser.GetComponent<Renderer>();

    if (rend != null)
    {
        rend.material = new Material(Shader.Find("Standard"));

        Color laserColor = Color.red;

        rend.material.color = laserColor;

        rend.material.EnableKeyword("_EMISSION");
        rend.material.SetColor("_EmissionColor", laserColor * 3f);
    }

    Collider col = laser.GetComponent<Collider>();

    if (col != null)
        Destroy(col);

    Laser laserScript = laser.AddComponent<Laser>();

    laserScript.Init(
        laserSpeed,
        laserDamage,
        5f,
        body,
        health
    );

    laser.transform.SetParent(lasersRoot);

    return laser;
}

    // ------------------------------------------------------------
    // BONUS PANELS
    // ------------------------------------------------------------

    void OnPanelCollected(BonusPanel panel)
    {
        if (state != State.Playing)
            return;

        switch (panel.type)
        {
            case BonusType.Heal:

                health.Heal(healAmount);

                Toast(
                    "+" +
                    healAmount +
                    " HEALTH"
                );

                break;

            case BonusType.Speed:

                player.ApplySpeedBoost(
                    speedMultiplier,
                    speedDuration
                );

                Toast("SPEED BOOST!");

                break;

            case BonusType.Shield:

                health.ApplyShield(
                    shieldDuration
                );

                Toast("SHIELD!");

                break;
        }
    }

    // ------------------------------------------------------------
    // GAME STATE
    // ------------------------------------------------------------

    void HandleDeath()
    {
        if (state != State.Playing)
            return;

        state = State.GameOver;

        player.enabled = false;

        StartCoroutine(
            ResetAfter(resetDelay)
        );
    }

    void HandleWin()
    {
        if (state != State.Playing)
            return;

        state = State.Won;

        player.enabled = false;

        StartCoroutine(
            ResetAfter(winDelay)
        );
    }

    IEnumerator ResetAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        ResetGame();
    }

    void ResetGame()
    {

        // Reset all permanent bonus panels.
        foreach (BonusPanel panel in panels)
        {
            if (panel != null)
                panel.ResetPanel();
        }

        health.ResetHealth();

        player.ResetState();

        player.Teleport(startPos);

        player.enabled = true;

        toast = "";
        toastTimer = 0f;

        spawnTimer = 0f;

        state = State.Playing;
    }

    void ClearLasers()
    {
        if (lasersRoot == null)
            return;

        for (
            int i = lasersRoot.childCount - 1;
            i >= 0;
            i--
        )
        {
            Destroy(
                lasersRoot.GetChild(i).gameObject
            );
        }
    }

    void Toast(string msg)
    {
        toast = msg;
        toastTimer = 2f;
    }

    // ------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------

    void OnGUI()
    {
        if (health == null || player == null)
            return;

        float s =
            Screen.height / 720f;

        if (labelStyle == null)
        {
            labelStyle =
                new GUIStyle(GUI.skin.label);

            bigStyle =
                new GUIStyle(GUI.skin.label);

            bigStyle.alignment =
                TextAnchor.MiddleCenter;

            bigStyle.fontStyle =
                FontStyle.Bold;
        }

        labelStyle.fontSize =
            Mathf.RoundToInt(20f * s);

        bigStyle.fontSize =
            Mathf.RoundToInt(56f * s);

        // --------------------------------------------------------
        // RED HIT FLASH
        // --------------------------------------------------------

        if (health.HitFlash > 0f)
        {
            GUI.color =
                new Color(
                    1f,
                    0f,
                    0f,
                    health.HitFlash * 0.35f
                );

            GUI.DrawTexture(
                new Rect(
                    0,
                    0,
                    Screen.width,
                    Screen.height
                ),
                Texture2D.whiteTexture
            );
        }

        // --------------------------------------------------------
        // HEALTH BAR
        // --------------------------------------------------------

        Rect bar =
            new Rect(
                20f * s,
                20f * s,
                320f * s,
                28f * s
            );

        GUI.color =
            new Color(
                0f,
                0f,
                0f,
                0.6f
            );

        GUI.DrawTexture(
            bar,
            Texture2D.whiteTexture
        );

        float pct =
            health.CurrentHealth /
            health.maxHealth;

        GUI.color =
            Color.Lerp(
                new Color(
                    0.9f,
                    0.1f,
                    0.1f
                ),
                new Color(
                    0.2f,
                    0.9f,
                    0.3f
                ),
                pct
            );

        GUI.DrawTexture(
            new Rect(
                bar.x + 2f,
                bar.y + 2f,
                (bar.width - 4f) * pct,
                bar.height - 4f
            ),
            Texture2D.whiteTexture
        );

        GUI.color = Color.white;

        DrawText(
            new Rect(
                bar.x + 8f * s,
                bar.y,
                bar.width,
                bar.height
            ),
            "HP " +
            Mathf.CeilToInt(
                health.CurrentHealth
            ) +
            " / " +
            Mathf.CeilToInt(
                health.maxHealth
            ),
            labelStyle,
            TextAnchor.MiddleLeft,
            Color.white
        );

        // --------------------------------------------------------
        // ACTIVE EFFECTS
        // --------------------------------------------------------

        float y =
            bar.yMax +
            6f * s;

        if (health.ShieldTimeLeft > 0f)
        {
            DrawText(
                new Rect(
                    bar.x,
                    y,
                    320f * s,
                    26f * s
                ),
                "SHIELD  " +
                health.ShieldTimeLeft
                    .ToString("0.0") +
                "s",
                labelStyle,
                TextAnchor.MiddleLeft,
                new Color(
                    1f,
                    0.9f,
                    0.2f
                )
            );

            y += 26f * s;
        }

        if (player.SpeedBoostTimeLeft > 0f)
        {
            DrawText(
                new Rect(
                    bar.x,
                    y,
                    320f * s,
                    26f * s
                ),
                "SPEED BOOST  " +
                player.SpeedBoostTimeLeft
                    .ToString("0.0") +
                "s",
                labelStyle,
                TextAnchor.MiddleLeft,
                new Color(
                    0.3f,
                    0.7f,
                    1f
                )
            );
        }

        // --------------------------------------------------------
        // DISTANCE
        // --------------------------------------------------------

        float dist =
            Mathf.Max(
                0f,
                goalLineZ -
                player.transform.position.z
            );

        DrawText(
            new Rect(
                Screen.width -
                340f * s,
                20f * s,
                320f * s,
                28f * s
            ),
            "Exit: " +
            Mathf.CeilToInt(dist) +
            " m",
            labelStyle,
            TextAnchor.MiddleRight,
            Color.white
        );

        // --------------------------------------------------------
        // CONTROLS
        // --------------------------------------------------------

        DrawText(
            new Rect(
                0f,
                Screen.height -
                40f * s,
                Screen.width,
                30f * s
            ),
            "WASD move   |   SPACE jump   |   Green: +HP   Blue: Speed   Yellow: Shield",
            labelStyle,
            TextAnchor.MiddleCenter,
            new Color(
                1f,
                1f,
                1f,
                0.8f
            )
        );

        // --------------------------------------------------------
        // TOAST
        // --------------------------------------------------------

        if (
            toastTimer > 0f &&
            state == State.Playing
        )
        {
            DrawText(
                new Rect(
                    0f,
                    Screen.height * 0.25f,
                    Screen.width,
                    60f * s
                ),
                toast,
                bigStyle,
                TextAnchor.MiddleCenter,
                new Color(
                    1f,
                    1f,
                    1f,
                    Mathf.Clamp01(
                        toastTimer
                    )
                )
            );
        }

        // --------------------------------------------------------
        // GAME OVER
        // --------------------------------------------------------

        if (state == State.GameOver)
        {
            DrawText(
                new Rect(
                    0f,
                    Screen.height * 0.35f,
                    Screen.width,
                    100f * s
                ),
                "GAME OVER",
                bigStyle,
                TextAnchor.MiddleCenter,
                new Color(
                    1f,
                    0.2f,
                    0.2f
                )
            );

            DrawText(
                new Rect(
                    0f,
                    Screen.height * 0.35f +
                    90f * s,
                    Screen.width,
                    40f * s
                ),
                "Resetting to start...",
                labelStyle,
                TextAnchor.MiddleCenter,
                Color.white
            );
        }

        // --------------------------------------------------------
        // WON
        // --------------------------------------------------------

        else if (state == State.Won)
        {
            DrawText(
                new Rect(
                    0f,
                    Screen.height * 0.35f,
                    Screen.width,
                    100f * s
                ),
                "YOU MADE IT!",
                bigStyle,
                TextAnchor.MiddleCenter,
                new Color(
                    0.3f,
                    1f,
                    0.5f
                )
            );

            DrawText(
                new Rect(
                    0f,
                    Screen.height * 0.35f +
                    90f * s,
                    Screen.width,
                    40f * s
                ),
                "Restarting...",
                labelStyle,
                TextAnchor.MiddleCenter,
                Color.white
            );
        }
    }

    void DrawText(
        Rect r,
        string text,
        GUIStyle style,
        TextAnchor align,
        Color color
    )
    {
        style.alignment = align;

        Color old =
            GUI.color;

        // Shadow
        GUI.color =
            new Color(
                0f,
                0f,
                0f,
                0.8f * color.a
            );

        GUI.Label(
            new Rect(
                r.x + 2f,
                r.y + 2f,
                r.width,
                r.height
            ),
            text,
            style
        );

        // Main text
        GUI.color = color;

        GUI.Label(
            r,
            text,
            style
        );

        GUI.color = old;
    }
}