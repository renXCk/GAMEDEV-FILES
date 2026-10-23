using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runtime flow for the parkour course.
//
//   Ready     -> timer starts the moment you move
//   Running   -> touching / being seen by a hazard = Fail()
//   Failed    -> short red fade, respawn at the last checkpoint, keep running
//   Finished  -> results screen, R = run again
//
//   R          respawn at last checkpoint (or restart after finishing)
//   Backspace  restart the whole run
//
// Hazards call Fail(this, "CheckCapsule", "...") so the HUD detection feed
// can name exactly which Physics check caught you.
public class ParkourGameManager : MonoBehaviour
{
    public static ParkourGameManager Instance { get; private set; }

    public enum RunState { Ready, Running, Failed, Finished }

    public struct FeedEntry
    {
        public string check;
        public string source;
        public string detail;
        public float time;
    }

    [Header("Flow")]
    [Tooltip("Falling below this height fails the run (the laser pit).")]
    public float killY = -2.5f;
    public float failFadeTime = 0.18f;
    public float failHoldTime = 0.5f;
    public float respawnFadeTime = 0.3f;
    [Tooltip("Hazards ignore you for this long after a respawn.")]
    public float respawnGrace = 0.6f;

    [Header("Keys")]
    public Key respawnKey = Key.R;
    public Key restartKey = Key.Backspace;

    const string BestTimeKey = "ParkourCourse_BestTime";

    // ---- Public state (HUD) ------------------------------------------
    public RunState State { get; private set; } = RunState.Ready;
    public bool IsRunning => State == RunState.Ready || State == RunState.Running;
    public bool CanBeHit => IsRunning && Time.time >= graceUntil;

    public float RunTime { get; private set; }
    public float BestTime { get; private set; }
    public bool NewRecord { get; private set; }
    public int Fails { get; private set; }

    public int CheckpointIndex => current != null ? current.index : 0;
    public int CheckpointCount { get; private set; }
    public int Collected { get; private set; }
    public int CollectibleCount { get; private set; }

    // 0 = clear, 1 = fully covered (the HUD draws the fade overlay).
    public float ScreenFade { get; private set; }
    public FeedEntry LastFail { get; private set; }

    public ParkourPlayer Player => player;

    // Sight targets for sentry raycasts (follow crouch/slide height).
    public Vector3 PlayerChest => body != null ? body.bounds.center : Vector3.zero;

    public Vector3 PlayerHead
    {
        get
        {
            if (body == null) return Vector3.zero;
            Bounds b = body.bounds;
            return new Vector3(b.center.x, b.max.y - 0.15f, b.center.z);
        }
    }
    public IReadOnlyList<FeedEntry> Feed => feed;

    public event Action<FeedEntry> FeedAdded;
    public event Action<string, Color> Toast;
    public event Action Failed;
    public event Action Respawned;
    public event Action Finished;

    // ---- Internals ---------------------------------------------------
    private ParkourPlayer player;
    private CharacterController body;
    private Checkpoint current;
    private Vector3 startPosition;
    private float startYaw;
    private float graceUntil;
    private bool busy;

    private readonly List<FeedEntry> feed = new List<FeedEntry>();

    void Awake()
    {
        Instance = this;

        player = FindFirstObjectByType<ParkourPlayer>();

        if (player == null)
        {
            Debug.LogError("ParkourGameManager: no ParkourPlayer found. Press BUILD COURSE on the ParkourCourseBuilder.");
            enabled = false;
            return;
        }

        body = player.GetComponent<CharacterController>();

        if (ParkourLayers.PlayerMask == 0)
            Debug.LogError("ParkourGameManager: the 'Player' layer doesn't exist. Press BUILD COURSE to create it.");
        else if (player.gameObject.layer != ParkourLayers.PlayerLayer)
            Debug.LogWarning("ParkourGameManager: the player is not on the 'Player' layer, so hazard checks won't find it.");

        startPosition = player.transform.position;
        startYaw = player.transform.eulerAngles.y;

        CheckpointCount = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length;
        CollectibleCount = FindObjectsByType<UVCollectible>(FindObjectsSortMode.None).Length;
        BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;

        if (kb != null)
        {
            if (kb[restartKey].wasPressedThisFrame)
            {
                RestartRun();
                return;
            }

            if (kb[respawnKey].wasPressedThisFrame)
            {
                if (State == RunState.Finished)
                    RestartRun();
                else if (IsRunning && !busy)
                    StartCoroutine(RespawnRoutine(false));
            }
        }

        // Timer starts on the first move.
        if (State == RunState.Ready && (player.MoveInput.sqrMagnitude > 0.01f || player.Speed > 0.1f))
            State = RunState.Running;

        if (State == RunState.Running || State == RunState.Failed)
            RunTime += Time.deltaTime;

        if (IsRunning && player.transform.position.y < killY)
            FailOther("Height check", "Laser Pit", "fell into the laser pit");
    }

    // ------------------------------------------------------------------
    // REPORTS
    // ------------------------------------------------------------------

    public void Fail(ParkourHazard source, string check, string detail)
    {
        FailOther(check, source != null ? source.hazardName : "?", detail);
    }

    public void FailOther(string check, string source, string detail)
    {
        if (!CanBeHit || busy)
            return;

        FeedEntry e = AddFeed(check, source, detail);
        LastFail = e;
        Fails++;

        StartCoroutine(RespawnRoutine(true));
    }

    FeedEntry AddFeed(string check, string source, string detail)
    {
        FeedEntry e = new FeedEntry { check = check, source = source, detail = detail, time = Time.time };

        feed.Add(e);
        if (feed.Count > 6)
            feed.RemoveAt(0);

        FeedAdded?.Invoke(e);
        return e;
    }

    public void ReachCheckpoint(Checkpoint cp)
    {
        if (current != null && cp.index <= current.index)
            return;

        current = cp;
        Toast?.Invoke("CHECKPOINT " + cp.index + " / " + CheckpointCount, ParkourPalette.Route);
    }

    public void Collect(UVCollectible c)
    {
        Collected++;
        Toast?.Invoke("SHARD  " + Collected + " / " + CollectibleCount, ParkourPalette.UV);
    }

    public void Finish()
    {
        if (!IsRunning)
            return;

        State = RunState.Finished;
        player.InputEnabled = false;

        NewRecord = BestTime <= 0f || RunTime < BestTime;

        if (NewRecord)
        {
            BestTime = RunTime;
            PlayerPrefs.SetFloat(BestTimeKey, BestTime);
            PlayerPrefs.Save();
        }

        Finished?.Invoke();
    }

    // ------------------------------------------------------------------
    // RESPAWN
    // ------------------------------------------------------------------

    IEnumerator RespawnRoutine(bool failed)
    {
        busy = true;
        RunState resumeState = State == RunState.Ready ? RunState.Ready : RunState.Running;

        if (failed)
        {
            State = RunState.Failed;
            Failed?.Invoke();
        }

        // Freeze the body where it got hit.
        player.InputEnabled = false;
        player.enabled = false;

        float fadeIn = failed ? failFadeTime : 0.12f;
        for (float t = 0f; t < fadeIn; t += Time.deltaTime)
        {
            ScreenFade = t / fadeIn;
            yield return null;
        }

        ScreenFade = 1f;
        yield return new WaitForSeconds(failed ? failHoldTime : 0.05f);

        Vector3 pos = current != null ? current.SpawnPosition : startPosition;
        float yaw = current != null ? current.SpawnYaw : startYaw;

        player.enabled = true;
        player.Teleport(pos, yaw);
        player.InputEnabled = true;

        graceUntil = Time.time + respawnGrace;
        State = resumeState;
        Respawned?.Invoke();

        for (float t = 0f; t < respawnFadeTime; t += Time.deltaTime)
        {
            ScreenFade = 1f - t / respawnFadeTime;
            yield return null;
        }

        ScreenFade = 0f;
        busy = false;
    }

    public void RestartRun()
    {
        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        // Works even if the scene isn't in the Build Profiles scene list.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }
}
