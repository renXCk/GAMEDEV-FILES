using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD built at runtime with UGUI + TextMeshPro (no prefabs to wire up).
// Minimal, game-style: no boxes, soft text shadows, one font.
//
//   top centre     run timer (big, monospaced digits) + best time
//   top left       checkpoint pips + shard count
//   centre         crosshair (turns cyan near a climbable ledge) + wall-run bar
//   bottom centre  controls (fades out after the start)
//   bottom right   UV light battery
//   full screen    fail flash / respawn fade / finish screen
//
// Technical info (which Physics check caught you, detection feed) only
// appears while the debug view is on (G) - for the demo video.
public class ParkourHUD : MonoBehaviour
{
    [Tooltip("HUD font. The course builder assigns Inter; empty = TMP default.")]
    public TMP_FontAsset font;

    const float Margin = 40f;

    private ParkourGameManager game;
    private Material shadowMaterial;

    private TextMeshProUGUI timerText, bestText;
    private Image[] pips;
    private TextMeshProUGUI shardText, hintText, feedText, toastText, debugBadge;
    private RectTransform batteryFill;
    private Image batteryFillImage;
    private Image crosshair;
    private RectTransform wallRunBar;
    private GameObject wallRunGroup;
    private CanvasGroup topGroup;

    private Image fadeOverlay;
    private CanvasGroup failGroup, finishGroup, toastGroup;
    private TextMeshProUGUI failTitle, failSub, failDebug;
    private TextMeshProUGUI finishTime, finishBest, finishStats;

    private float toastTimer;
    private bool showingFail;
    private float finishFade;
    private int lastCentis = -1;
    private float hintTimer = 10f;

    // ==================================================================
    // SETUP
    // ==================================================================

    void Start()
    {
        game = ParkourGameManager.Instance;

        if (font == null)
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("ParkourHUD: TextMeshPro essentials are missing. " +
                                 "Window > TextMeshPro > Import TMP Essential Resources, then press Play again.");
                enabled = false;
                return;
            }

            font = TMP_Settings.defaultFontAsset;
        }

        if (game == null)
        {
            enabled = false;
            return;
        }

        // One shared material with a soft drop shadow for every label.
        shadowMaterial = new Material(font.material);
        shadowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        shadowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
        shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
        shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
        shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.45f);

        Build();

        game.Toast += OnToast;
        game.Failed += OnFailed;
        game.Finished += OnFinished;
    }

    void OnDestroy()
    {
        if (shadowMaterial != null) Destroy(shadowMaterial);
        if (game == null) return;
        game.Toast -= OnToast;
        game.Failed -= OnFailed;
        game.Finished -= OnFinished;
    }

    void Build()
    {
        GameObject canvasGo = new GameObject("HUD_Canvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasGo.transform;

        // Everything that hides on the finish screen.
        RectTransform top = Stretch(root, "Gameplay", Color.clear).rectTransform;
        topGroup = top.gameObject.AddComponent<CanvasGroup>();

        // ---- Timer ---------------------------------------------------
        timerText = Text(top, "Timer", "", 64f, ParkourPalette.TextMain, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(500f, 80f), 0f);
        Box(top, "TimerAccent", ParkourPalette.Route, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(44f, 3f));
        bestText = Text(top, "Best", "", 15f, ParkourPalette.TextDim, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(500f, 22f), 6f);

        // ---- Checkpoint pips + shards (top left) ---------------------
        Text(top, "CPLabel", "CHECKPOINT", 14f, ParkourPalette.TextDim, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(300f, 20f), 6f);

        int count = Mathf.Max(1, game.CheckpointCount);
        pips = new Image[count];
        for (int i = 0; i < count; i++)
            pips[i] = Box(top, "Pip_" + i, Color.white, new Vector2(0f, 1f),
                new Vector2(Margin + i * 42f, -Margin - 30f), new Vector2(36f, 4f)).GetComponent<Image>();

        RectTransform diamond = Box(top, "ShardIcon", ParkourPalette.UV, new Vector2(0f, 1f), new Vector2(Margin + 3f, -Margin - 56f), new Vector2(11f, 11f));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        shardText = Text(top, "Shards", "", 22f, ParkourPalette.TextMain, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(Margin + 24f, -Margin - 46f), new Vector2(200f, 30f), 2f);

        // ---- Debug badge (top right, only with G) --------------------
        debugBadge = Text(top, "DebugBadge", "DEBUG VIEW", 14f, new Color(1f, 0.9f, 0.4f), TextAlignmentOptions.Right,
            new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(300f, 20f), 6f);

        // ---- Crosshair + wall-run bar --------------------------------
        crosshair = Box(top, "Crosshair", new Color(1f, 1f, 1f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f))
            .GetComponent<Image>();

        RectTransform wr = Box(top, "WallRunBar", new Color(1f, 1f, 1f, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(90f, 3f));
        wallRunBar = Box(wr, "Fill", ParkourPalette.WallRun, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(90f, 3f), new Vector2(0f, 0.5f));
        wallRunGroup = wr.gameObject;

        // ---- Controls (bottom centre, fades out) ---------------------
        hintText = Text(top, "Controls",
            "WASD  MOVE      SHIFT  SPRINT      SPACE  JUMP      CTRL  SLIDE      F  LIGHT      R  RESPAWN",
            14f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(1400f, 20f), 4f);

        // ---- UV battery (bottom right) -------------------------------
        Text(top, "UVLabel", "UV", 14f, ParkourPalette.TextDim, TextAlignmentOptions.Right,
            new Vector2(1f, 0f), new Vector2(-Margin, Margin + 12f), new Vector2(200f, 20f), 6f);
        RectTransform barBg = Box(top, "UVBar", new Color(1f, 1f, 1f, 0.15f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(180f, 4f));
        batteryFill = Box(barBg, "Fill", ParkourPalette.UV, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(180f, 4f), new Vector2(0f, 0.5f));
        batteryFillImage = batteryFill.GetComponent<Image>();

        // ---- Detection feed (bottom left, only with G) ---------------
        feedText = Text(top, "Feed", "", 16f, ParkourPalette.TextMain, TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(700f, 160f), 1f);

        // ---- Toast ---------------------------------------------------
        toastText = Text(top, "Toast", "", 22f, ParkourPalette.Route, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(900f, 34f), 10f);
        toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;

        // ---- Fade overlay --------------------------------------------
        fadeOverlay = Stretch(root, "Fade", new Color(0f, 0f, 0f, 0f));

        // ---- Fail screen ---------------------------------------------
        RectTransform fail = Stretch(root, "FailScreen", Color.clear).rectTransform;
        failGroup = fail.gameObject.AddComponent<CanvasGroup>();
        failGroup.alpha = 0f;

        failTitle = Text(fail, "Title", "", 84f, ParkourPalette.Laser, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 100f), 18f);
        failSub = Text(fail, "Sub", "", 15f, ParkourPalette.TextDim, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 22f), 6f);
        failDebug = Text(fail, "Debug", "", 20f, new Color(1f, 0.9f, 0.4f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1400f, 28f), 1f);

        // ---- Finish screen -------------------------------------------
        Image finishBg = Stretch(root, "FinishScreen", new Color(0.03f, 0.04f, 0.08f, 0.8f));
        RectTransform finish = finishBg.rectTransform;
        finishGroup = finish.gameObject.AddComponent<CanvasGroup>();
        finishGroup.alpha = 0f;

        Text(finish, "Title", "FINISHED", 26f, ParkourPalette.Finish, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 36f), 18f);
        finishTime = Text(finish, "Time", "", 120f, ParkourPalette.TextMain, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1200f, 140f), 0f);
        finishBest = Text(finish, "Best", "", 18f, ParkourPalette.Route, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1200f, 26f), 8f);
        finishStats = Text(finish, "Stats", "", 16f, ParkourPalette.TextDim, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1200f, 24f), 6f);
        Text(finish, "Again", "PRESS  R  TO RUN AGAIN", 15f, ParkourPalette.TextMain, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1200f, 22f), 8f);
    }

    // ==================================================================
    // UPDATE
    // ==================================================================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ParkourPlayer player = game.Player;
        bool debug = ParkourDebugDraw.Enabled;
        bool ready = game.State == ParkourGameManager.RunState.Ready;

        // ---- Timer (only rebuild the string when the value changes) ----
        int centis = Mathf.FloorToInt(game.RunTime * 100f);
        if (centis != lastCentis)
        {
            lastCentis = centis;
            timerText.text = TimerString(game.RunTime);
        }

        timerText.alpha = ready ? 0.5f : 1f;
        bestText.text = game.BestTime > 0f ? "BEST  " + PlainTime(game.BestTime) : "";

        // ---- Progress ------------------------------------------------
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < game.CheckpointIndex ? ParkourPalette.Route : new Color(1f, 1f, 1f, 0.2f);

        shardText.text = game.Collected + " <color=#8C95A6>/ " + game.CollectibleCount + "</color>";

        debugBadge.enabled = debug;

        // ---- Crosshair + wall-run ------------------------------------
        if (player != null)
        {
            bool ledge = player.LedgeAvailable;
            crosshair.color = ledge ? ParkourPalette.Route : new Color(1f, 1f, 1f, 0.85f);
            crosshair.rectTransform.sizeDelta = Vector2.one * (ledge ? 8f : 5f);

            bool wallRunning = player.State == ParkourPlayer.MoveState.WallRun;
            wallRunGroup.SetActive(wallRunning);
            if (wallRunning)
                wallRunBar.sizeDelta = new Vector2(90f * (1f - player.WallRun01), 3f);
        }

        // ---- Controls fade out once you get going ---------------------
        if (!ready)
            hintTimer -= dt;
        hintText.alpha = Mathf.Clamp01(hintTimer / 2f) * 0.7f;

        // ---- UV battery ----------------------------------------------
        UVFlashlight uv = UVFlashlight.Active;
        if (uv != null)
        {
            batteryFill.sizeDelta = new Vector2(180f * uv.Battery01, 4f);
            bool low = uv.Battery01 < uv.lowBatteryFraction;
            batteryFillImage.color = low ? ParkourPalette.Laser
                : uv.IsOn ? ParkourPalette.UV
                : new Color(0.6f, 0.36f, 1f, 0.4f);
        }

        // ---- Detection feed: debug view only --------------------------
        if (debug)
            UpdateFeed();
        else if (feedText.text.Length > 0)
            feedText.text = "";

        // ---- Toast ---------------------------------------------------
        if (toastTimer > 0f)
        {
            toastTimer -= dt;
            toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f) * Mathf.Clamp01((2f - toastTimer) / 0.15f);
        }
        else
        {
            toastGroup.alpha = 0f;
        }

        // ---- Fail / respawn fade -------------------------------------
        float fade = game.ScreenFade;
        Color fc = showingFail ? new Color(0.22f, 0.01f, 0.05f) : ParkourPalette.Sky;
        fc.a = fade * 0.85f;
        fadeOverlay.color = fc;

        failGroup.alpha = showingFail ? fade : 0f;
        if (showingFail && fade <= 0f && game.State != ParkourGameManager.RunState.Failed)
            showingFail = false;

        // ---- Finish --------------------------------------------------
        if (game.State == ParkourGameManager.RunState.Finished)
        {
            finishFade = Mathf.MoveTowards(finishFade, 1f, dt / 0.5f);
            finishGroup.alpha = finishFade;
            topGroup.alpha = 1f - finishFade;
        }
    }

    void UpdateFeed()
    {
        var feed = game.Feed;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < feed.Count; i++)
        {
            float age = Time.time - feed[i].time;
            if (age > 6f) continue;

            int alpha = Mathf.RoundToInt(Mathf.Clamp01(6f - age) * 255f);
            sb.Append("<alpha=#").Append(alpha.ToString("X2")).Append(">");
            sb.Append("<color=#FFE066>").Append(feed[i].check).Append("</color>");
            sb.Append("<color=#8C95A6>  -  </color>").Append(feed[i].source);
            sb.Append('\n');
        }

        if (sb.Length > 0)
            sb.Length--; // newest line sits on the bottom edge

        feedText.text = sb.ToString();
    }

    // ==================================================================
    // EVENTS
    // ==================================================================

    void OnToast(string msg, Color color)
    {
        toastText.text = msg;
        toastText.color = color;
        toastTimer = 2f;
    }

    void OnFailed()
    {
        ParkourGameManager.FeedEntry e = game.LastFail;
        showingFail = true;

        failTitle.text = e.check == "Raycast" ? "SPOTTED"
            : e.check == "Height check" ? "FELL"
            : "ZAPPED";

        failSub.text = game.CheckpointIndex > 0
            ? "BACK TO CHECKPOINT " + game.CheckpointIndex
            : "BACK TO START";

        // Which Physics check caught you - only in the debug view.
        failDebug.text = ParkourDebugDraw.Enabled ? e.check + "  -  " + e.source : "";
    }

    void OnFinished()
    {
        finishTime.text = TimerString(game.RunTime);
        finishBest.text = game.NewRecord ? "NEW BEST" : "BEST  " + PlainTime(game.BestTime);
        finishStats.text = "SHARDS  " + game.Collected + " / " + game.CollectibleCount +
                           "        FAILS  " + game.Fails;
    }

    // "01:23" big + ".45" small, with fixed-width digits so it doesn't wobble.
    static string TimerString(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t - minutes * 60f);
        int centis = (int)((t - Mathf.Floor(t)) * 100f);

        return "<mspace=0.62em>" + minutes.ToString("00") + ":" + seconds.ToString("00") + "</mspace>" +
               "<size=50%><color=#AEB6C4><mspace=0.62em>." + centis.ToString("00") + "</mspace></color></size>";
    }

    static string PlainTime(float t)
    {
        int minutes = (int)(t / 60f);
        float seconds = t - minutes * 60f;
        return minutes.ToString("00") + ":" + seconds.ToString("00.00");
    }

    // ==================================================================
    // UI HELPERS
    // ==================================================================

    TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Color color,
                         TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSharedMaterial = shadowMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    RectTransform Box(RectTransform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    Image Stretch(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
