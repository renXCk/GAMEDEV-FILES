using UnityEngine;
using UnityEngine.InputSystem;

// Mouse look + camera feel for the parkour player.
//
//   Update      (runs BEFORE ParkourPlayer): mouse look. Yaw turns the body,
//               pitch turns the camera root, so movement uses this frame's aim.
//   LateUpdate  (after the player moved): head bob, FOV kick, wall-run tilt,
//               landing dip. These only touch the CAMERA's local transform, so
//               they never affect movement or collisions.
//
// Hierarchy:  Player (yaw) -> CameraRoot (pitch, eye height) -> Camera (effects)
//   Esc unlocks the cursor, clicking the Game view locks it again.
[DefaultExecutionOrder(-100)]
public class ParkourCamera : MonoBehaviour
{
    [Header("References")]
    public ParkourPlayer player;
    [Tooltip("Pitch pivot at eye height (child of the player).")]
    public Transform cameraRoot;
    public Camera cam;

    [Header("Look")]
    public float sensitivity = 0.08f;
    public float pitchLimit = 88f;

    [Header("FOV kick")]
    public float baseFov = 78f;
    [Tooltip("Extra FOV at top speed.")]
    public float speedFovKick = 10f;
    public float slideFovKick = 6f;
    public float fovSmoothing = 6f;

    [Header("Head bob")]
    public float bobAmplitude = 0.04f;
    [Tooltip("Metres travelled per footstep.")]
    public float stepLength = 2.1f;

    [Header("Tilt")]
    public float wallRunTilt = 12f;
    public float strafeTilt = 1.2f;
    public float tiltSmoothing = 9f;

    [Header("Landing dip")]
    [Tooltip("Downward camera velocity per m/s of landing impact.")]
    public float dipPerImpact = 0.35f;
    public float maxDipVelocity = 4f;
    public float dipSpring = 150f;
    public float dipDamping = 20f;
    [Tooltip("Degrees of pitch nod per metre of dip.")]
    public float dipPitch = 18f;

    private float yaw;
    private float pitch;

    private float bobPhase;
    private float bobWeight;
    private float roll;
    private float dip;
    private float dipVelocity;

    void Awake()
    {
        if (player == null) player = GetComponent<ParkourPlayer>();
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cameraRoot == null && cam != null) cameraRoot = cam.transform.parent;

        yaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        if (player != null) player.Landed += OnLanded;
    }

    void OnDisable()
    {
        if (player != null) player.Landed -= OnLanded;
    }

    void Start()
    {
        LockCursor(true);
        if (cam != null) cam.fieldOfView = baseFov;
    }

    // ------------------------------------------------------------------
    // LOOK (Update, before the player moves)
    // ------------------------------------------------------------------

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);

        bool canLook = mouse != null &&
                       Cursor.lockState == CursorLockMode.Locked &&
                       (player == null || player.InputEnabled);

        if (canLook)
        {
            // Mouse delta is already "per frame" - never multiply by deltaTime.
            Vector2 delta = mouse.delta.ReadValue() * sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ------------------------------------------------------------------
    // FEEL (LateUpdate, after the player moved)
    // ------------------------------------------------------------------

    void LateUpdate()
    {
        if (cam == null || player == null)
            return;

        float dt = Time.deltaTime;
        float speed = player.Speed;
        bool sliding = player.State == ParkourPlayer.MoveState.Slide;

        // ---- FOV kick: scales with speed above a normal run ----------
        float speed01 = Mathf.InverseLerp(player.runSpeed, player.sprintSpeed + 3f, speed);
        float targetFov = baseFov + speed01 * speedFovKick + (sliding ? slideFovKick : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));

        // ---- Head bob: phase follows distance travelled, not time ----
        bool bobbing = player.IsGrounded && !sliding && speed > 0.5f;
        float targetWeight = bobbing ? Mathf.Clamp01(speed / player.sprintSpeed) : 0f;
        bobWeight = Mathf.Lerp(bobWeight, targetWeight, 1f - Mathf.Exp(-10f * dt));

        bobPhase += speed / Mathf.Max(0.1f, stepLength) * Mathf.PI * dt;

        Vector3 bob = new Vector3(
            Mathf.Cos(bobPhase) * bobAmplitude * 0.5f,
            Mathf.Sin(bobPhase * 2f) * bobAmplitude,
            0f) * bobWeight;

        // ---- Tilt: away from the wall while wall-running, tiny strafe lean
        // (+Z roll tilts the view to the right in Unity's left-handed space).
        float targetRoll = -player.WallSide * wallRunTilt +
                           (player.IsGrounded ? player.MoveInput.x * strafeTilt : 0f);
        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-tiltSmoothing * dt));

        // ---- Landing dip: damped spring back to 0 --------------------
        float accel = -dipSpring * dip - dipDamping * dipVelocity;
        dipVelocity += accel * dt;
        dip += dipVelocity * dt;

        cam.transform.localPosition = bob + Vector3.up * dip;
        cam.transform.localRotation = Quaternion.Euler(-dip * dipPitch, 0f, roll);
    }

    void OnLanded(float impactSpeed)
    {
        dipVelocity -= Mathf.Min(impactSpeed * dipPerImpact, maxDipVelocity);
    }

    // ------------------------------------------------------------------
    // API
    // ------------------------------------------------------------------

    // Called after a teleport: face the player's new yaw, level the view.
    public void ResetLook()
    {
        yaw = transform.eulerAngles.y;
        pitch = 0f;
        roll = 0f;
        dip = dipVelocity = 0f;
        bobWeight = 0f;

        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.identity;

        if (cam != null)
        {
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = baseFov;
        }
    }

    public static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
