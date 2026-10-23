using System;
using UnityEngine;
using UnityEngine.InputSystem;

// First-person parkour motor (CharacterController + new Input System).
//
//   WASD         move (momentum based)
//   Shift        sprint (or just hold W for autoSprintDelay seconds)
//   Space        jump / double jump / wall-jump / mantle a high ledge
//   Ctrl / C     crouch; while running fast = SLIDE (keeps momentum)
//
// Moves:
//   Coyote time + jump buffer, variable jump height, air control.
//   Double jump  - once per airtime, refreshed on ground / walls / ledges.
//   Slide        - crouch above slideMinSpeed: speed boost, slow friction,
//                  accelerates down ramps. Fits under 1.1 m gaps.
//   Wall-run     - only on walls with a WallRunSurface (orange panels).
//                  Hold W, run along it with low gravity for up to
//                  wallRunMaxTime. Space = wall-jump off it.
//   Vault/mantle - waist-high ledges are vaulted automatically,
//                  chest-high ledges are climbed when you press/hold Space.
//
// Smoothness rules:
//   - ONE controller.Move() per frame, in Update, using Time.deltaTime.
//   - The camera is a child of the player, so it moves in the same frame.
//     There is no Rigidbody, so there is nothing to interpolate.
//   - Mouse look runs in ParkourCamera.Update BEFORE this script
//     (DefaultExecutionOrder), so the move direction uses this frame's aim.
//   - Camera effects (bob, FOV, tilt, landing dip) run in ParkourCamera.LateUpdate.
[RequireComponent(typeof(CharacterController))]
public class ParkourPlayer : MonoBehaviour
{
    public enum MoveState { Grounded, Air, Slide, WallRun, Mantle }
    public enum JumpKind { Ground, Slide, Double, Wall }

    [Header("References")]
    [Tooltip("Eye pivot (pitch). Holds the camera.")]
    public Transform cameraRoot;

    [Header("Speed")]
    public float runSpeed = 7f;
    public float sprintSpeed = 9.5f;
    public float backSpeed = 4.5f;
    public float crouchSpeed = 3.5f;
    [Tooltip("Holding W this long switches to sprint without Shift.")]
    public float autoSprintDelay = 0.6f;

    [Header("Ground")]
    public float groundAccel = 55f;
    public float groundDecel = 45f;
    [Tooltip("How fast speed ABOVE the target bleeds off on the ground. " +
             "Low = you keep momentum from slides and jumps.")]
    public float overspeedDecay = 6f;
    [Tooltip("Turn rate (1/s) when moving faster than the target speed.")]
    public float overspeedSteer = 8f;

    [Header("Air")]
    [Range(0f, 1f)] public float airControl = 0.35f;
    public float airAccel = 40f;

    [Header("Jump")]
    public float jumpHeight = 1.35f;
    public float gravity = 26f;
    public float fallGravityMultiplier = 1.35f;
    [Tooltip("Extra gravity while rising with Space released = short hop.")]
    public float jumpCutGravityMultiplier = 2f;
    [Tooltip("You can still jump this long after running off a ledge.")]
    public float coyoteTime = 0.12f;
    [Tooltip("A jump pressed this long before landing still happens.")]
    public float jumpBufferTime = 0.15f;
    public float maxFallSpeed = 30f;

    [Header("Double jump")]
    public bool allowDoubleJump = true;
    public float doubleJumpHeight = 1.1f;

    [Header("Crouch / Slide")]
    public float crouchHeight = 0.95f;
    public float crouchEyeHeight = 0.8f;
    [Tooltip("Metres per second the capsule grows/shrinks.")]
    public float heightChangeSpeed = 7f;
    public float slideMinSpeed = 5.5f;
    public float slideBoost = 2.5f;
    public float slideMaxSpeed = 13f;
    public float slideFriction = 4.5f;
    [Tooltip("Acceleration down slopes while sliding.")]
    public float slideSlopeAccel = 20f;
    [Tooltip("Radians per second you can steer a slide.")]
    public float slideSteer = 1.5f;
    public float slideStopSpeed = 3f;

    [Header("Wall-run (WallRunSurface only)")]
    public float wallRunMinSpeed = 5f;
    public float wallRunSpeed = 9.5f;
    public float wallRunMaxTime = 1.5f;
    public float wallRunGravity = 6f;
    [Tooltip("Upward speed when you hit the wall.")]
    public float wallRunEntryUp = 2.5f;
    [Tooltip("Side ray length beyond the capsule radius.")]
    public float wallCheckDistance = 0.6f;
    [Tooltip("Must be at least this high above the ground to start.")]
    public float wallRunMinHeight = 0.8f;
    public float wallJumpPush = 7f;
    public float wallJumpHeight = 1.3f;
    [Tooltip("Can't re-grab the same wall for this long after leaving it.")]
    public float wallReattachDelay = 0.4f;

    [Header("Vault / Mantle")]
    public float mantleMinHeight = 0.35f;
    [Tooltip("Ledges up to this height are vaulted automatically.")]
    public float vaultMaxHeight = 1.1f;
    [Tooltip("Ledges up to this height can be climbed with Space.")]
    public float mantleMaxHeight = 1.75f;
    public float ledgeReach = 0.7f;
    public float vaultDuration = 0.2f;
    public float mantleDuration = 0.32f;

    [Header("Body")]
    public float standingHeight = 1.8f;
    public float eyeHeight = 1.62f;

    // ---- Public state (camera, HUD, game manager) --------------------
    public bool InputEnabled { get; set; } = true;
    public MoveState State { get; private set; } = MoveState.Grounded;
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouched { get; private set; }
    public Vector2 MoveInput { get; private set; }
    public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
    public float Speed => planarVelocity.magnitude;
    // -1 wall on the left, +1 wall on the right, 0 none.
    public int WallSide { get; private set; }
    // A ledge in front of you can be vaulted/mantled right now (HUD crosshair).
    public bool LedgeAvailable { get; private set; }
    public float WallRun01 => State == MoveState.WallRun ? wallRunTimer / wallRunMaxTime : 0f;

    public string StateName
    {
        get
        {
            switch (State)
            {
                case MoveState.Slide: return "SLIDE";
                case MoveState.WallRun: return "WALL-RUN";
                case MoveState.Mantle: return mantleIsVault ? "VAULT" : "MANTLE";
                case MoveState.Air: return "AIR";
                default:
                    if (IsCrouched) return "CROUCH";
                    if (Speed < 0.3f) return "IDLE";
                    return IsSprinting ? "SPRINT" : "RUN";
            }
        }
    }

    // impact speed (m/s, positive) when touching down after being airborne.
    public event Action<float> Landed;
    public event Action<JumpKind> Jumped;

    // ---- Internals ---------------------------------------------------
    private CharacterController controller;
    private ParkourCamera look;

    private Vector3 planarVelocity;
    private float verticalVelocity;
    private Vector3 groundNormal = Vector3.up;

    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private float forwardHeldTime;
    private float airTime;
    private bool justLanded;
    private bool doubleJumpUsed;

    private bool jumpPressed;
    private bool jumpHeld;
    private bool sprintHeld;
    private bool crouchHeld;
    private bool crouchPressed;

    // wall-run
    private Vector3 wallNormal;
    private float wallRunTimer;
    private Collider lastWall;
    private float lastWallBlockedUntil;
    private float anyWallBlockedUntil;

    // mantle
    private struct Ledge
    {
        public Vector3 top;
        public Vector3 inward;
        public float height;
    }

    private Vector3 mantleStart;
    private Vector3 mantleEnd;
    private Vector3 mantleDir;
    private float mantleT;
    private float mantleTime;
    private float mantleExitSpeed;
    private bool mantleIsVault;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        look = GetComponent<ParkourCamera>();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform.parent;

        ApplyHeight(standingHeight, eyeHeight);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        ReadInput(dt);

        if (State == MoveState.Mantle)
        {
            UpdateMantle(dt);
            return;
        }

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            doubleJumpUsed = false;
        }

        Vector3 wish = WishDirection();

        // Ledges first: a vault/mantle replaces normal movement.
        LedgeAvailable = FindLedge(out Ledge ledge);

        if (LedgeAvailable && ShouldMantle(ledge))
        {
            BeginMantle(ledge);
            return;
        }

        if (State == MoveState.WallRun)
            WallRunMove(dt);
        else if (State == MoveState.Slide)
            SlideMove(wish, dt);
        else if (IsGrounded)
            GroundMove(wish, dt);
        else
        {
            AirMove(wish, dt);
            TryStartWallRun();
        }

        HandleJump();
        ApplyGravity(dt);
        UpdateHeight(dt);

        // ---- The ONE Move() of this frame ---------------------------
        bool wasGrounded = IsGrounded;
        float vyBeforeMove = verticalVelocity;

        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(motion * dt);

        IsGrounded = controller.isGrounded;
        AfterMove(flags, wasGrounded, vyBeforeMove, dt);
    }

    // ------------------------------------------------------------------
    // INPUT
    // ------------------------------------------------------------------

    void ReadInput(float dt)
    {
        Keyboard kb = Keyboard.current;

        if (!InputEnabled || kb == null)
        {
            MoveInput = Vector2.zero;
            jumpPressed = jumpHeld = sprintHeld = crouchHeld = crouchPressed = false;
            forwardHeldTime = 0f;
            IsSprinting = false;
            return;
        }

        Vector2 input = Vector2.zero;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.wKey.isPressed) input.y += 1f;
        MoveInput = Vector2.ClampMagnitude(input, 1f);

        // One-shot press -> buffered; held state for variable jump height.
        jumpPressed = kb.spaceKey.wasPressedThisFrame;
        if (jumpPressed)
            lastJumpPressedTime = Time.time;

        jumpHeld = kb.spaceKey.isPressed;
        sprintHeld = kb.leftShiftKey.isPressed;
        crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
        crouchPressed = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;

        if (MoveInput.y > 0.1f)
            forwardHeldTime += dt;
        else
            forwardHeldTime = 0f;

        // Sprint state only changes on the ground; you keep it through jumps.
        if (IsGrounded)
            IsSprinting = MoveInput.y > 0.1f && !IsCrouched &&
                          (sprintHeld || forwardHeldTime >= autoSprintDelay);
        else if (MoveInput.y <= 0.1f)
            IsSprinting = false;
    }

    bool JumpBuffered => Time.time - lastJumpPressedTime <= jumpBufferTime;

    void ConsumeJump()
    {
        lastJumpPressedTime = -999f;
        jumpPressed = false;
    }

    Vector3 WishDirection()
    {
        Vector3 wish = transform.forward * MoveInput.y + transform.right * MoveInput.x;
        return Vector3.ClampMagnitude(wish, 1f);
    }

    float TargetSpeed()
    {
        float magnitude = MoveInput.magnitude;

        if (IsCrouched)
            return crouchSpeed * magnitude;

        if (MoveInput.y > 0.1f)
            return (IsSprinting ? sprintSpeed : runSpeed) * magnitude;

        if (MoveInput.y < -0.1f)
            return backSpeed * magnitude;

        return runSpeed * 0.85f * magnitude;
    }

    // ------------------------------------------------------------------
    // GROUND / AIR
    // ------------------------------------------------------------------

    void GroundMove(Vector3 wish, float dt)
    {
        State = MoveState.Grounded;

        // Crouch while fast (or land with crouch held) = slide.
        if (crouchHeld && (crouchPressed || justLanded) && Speed >= slideMinSpeed)
        {
            BeginSlide();
            SlideMove(wish, dt);
            return;
        }

        float target = TargetSpeed();
        float speed = planarVelocity.magnitude;

        if (wish.sqrMagnitude < 0.0001f)
        {
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, groundDecel * dt);
        }
        else if (speed > target + 0.1f)
        {
            // Faster than we "should" be (slide exit, fast landing...):
            // steer toward the input but bleed the extra speed slowly = momentum.
            Vector3 dir = Vector3.Slerp(planarVelocity / speed, wish.normalized, 1f - Mathf.Exp(-overspeedSteer * dt));
            float newSpeed = Mathf.MoveTowards(speed, target, overspeedDecay * dt);
            planarVelocity = dir.normalized * newSpeed;
        }
        else
        {
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish.normalized * target, groundAccel * dt);
        }
    }

    void AirMove(Vector3 wish, float dt)
    {
        State = MoveState.Air;

        if (wish.sqrMagnitude < 0.0001f)
            return; // no input: keep all momentum

        // Air control can steer, but never adds speed beyond what you had
        // (or your normal run speed, whichever is bigger).
        float cap = Mathf.Max(planarVelocity.magnitude, TargetSpeed());
        Vector3 target = wish.normalized * cap;

        planarVelocity = Vector3.MoveTowards(planarVelocity, target, airAccel * airControl * dt);
    }

    // ------------------------------------------------------------------
    // SLIDE
    // ------------------------------------------------------------------

    void BeginSlide()
    {
        State = MoveState.Slide;
        float speed = Mathf.Min(Speed + slideBoost, slideMaxSpeed);
        planarVelocity = planarVelocity.normalized * speed;
        IsSprinting = false;
    }

    void SlideMove(Vector3 wish, float dt)
    {
        if (!IsGrounded)
        {
            // Slid off a ledge: keep the momentum, become airborne.
            State = MoveState.Air;
            return;
        }

        State = MoveState.Slide;

        // Slopes: accelerate downhill (and slow down uphill).
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
        Vector3 v = planarVelocity + new Vector3(downhill.x, 0f, downhill.z) * slideSlopeAccel * dt;

        float speed = Mathf.Max(0f, v.magnitude - slideFriction * dt);
        Vector3 dir = v.sqrMagnitude > 0.0001f ? v.normalized : transform.forward;

        if (wish.sqrMagnitude > 0.0001f)
            dir = Vector3.RotateTowards(dir, wish.normalized, slideSteer * dt, 0f);

        planarVelocity = dir * Mathf.Min(speed, slideMaxSpeed);

        bool released = !crouchHeld && CanStandUp();

        if (released || speed < slideStopSpeed)
            State = MoveState.Grounded; // UpdateHeight keeps you crouched if needed
    }

    // ------------------------------------------------------------------
    // WALL-RUN
    // ------------------------------------------------------------------

    Vector3 ChestPoint => transform.position + Vector3.up * (controller.height * 0.6f);

    bool RaycastWallRunSurface(Vector3 dir, float extra, out RaycastHit hit)
    {
        bool found = Physics.Raycast(
            ChestPoint, dir, out hit,
            controller.radius + extra,
            ParkourLayers.ObstacleMask,
            QueryTriggerInteraction.Ignore);

        return found &&
               Mathf.Abs(hit.normal.y) < 0.25f &&
               hit.collider.GetComponentInParent<WallRunSurface>() != null;
    }

    // Side rays (straight and 45 deg forward) for a wall-run wall on 'side'.
    bool FindWallRunWall(int side, out RaycastHit hit)
    {
        Vector3 right = transform.right * side;

        if (RaycastWallRunSurface(right, wallCheckDistance, out hit))
            return true;

        return RaycastWallRunSurface((right + transform.forward).normalized, wallCheckDistance + 0.2f, out hit);
    }

    void TryStartWallRun()
    {
        if (Time.time < anyWallBlockedUntil || MoveInput.y <= 0.1f || Speed < wallRunMinSpeed)
            return;

        if (verticalVelocity < -8f)
            return; // falling too fast to catch the wall

        // Too close to the ground - just run normally.
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, wallRunMinHeight,
                            ParkourLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            return;

        for (int side = -1; side <= 1; side += 2)
        {
            if (!FindWallRunWall(side, out RaycastHit hit))
                continue;

            if (hit.collider == lastWall && Time.time < lastWallBlockedUntil)
                continue;

            Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;

            // Must be running ALONG the wall, not straight into it.
            Vector3 along = Vector3.ProjectOnPlane(planarVelocity, n);
            if (along.magnitude < Speed * 0.5f)
                continue;

            BeginWallRun(side, n, hit.collider);
            return;
        }
    }

    void BeginWallRun(int side, Vector3 normal, Collider wall)
    {
        State = MoveState.WallRun;
        WallSide = side;
        wallNormal = normal;
        wallRunTimer = 0f;
        lastWall = wall;
        doubleJumpUsed = false;
        verticalVelocity = Mathf.Clamp(verticalVelocity, wallRunEntryUp, wallRunEntryUp + 2f);
    }

    void WallRunMove(float dt)
    {
        wallRunTimer += dt;

        bool stillOnWall = RaycastWallRunSurface(-wallNormal, wallCheckDistance + 0.2f, out RaycastHit hit);

        if (!stillOnWall || IsGrounded || MoveInput.y <= 0.1f || wallRunTimer >= wallRunMaxTime)
        {
            ExitWallRun(1.5f);
            return;
        }

        wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;

        Vector3 along = Vector3.ProjectOnPlane(planarVelocity, wallNormal);
        if (along.sqrMagnitude < 0.01f)
            along = Vector3.ProjectOnPlane(transform.forward, wallNormal);

        // Hold speed, plus a small push into the wall so we stay attached.
        along = along.normalized * Mathf.Max(along.magnitude, wallRunSpeed);
        planarVelocity = along - wallNormal * 1.5f;
    }

    void ExitWallRun(float push)
    {
        State = MoveState.Air;
        WallSide = 0;
        planarVelocity += wallNormal * push;
        lastWallBlockedUntil = Time.time + wallReattachDelay;
        anyWallBlockedUntil = Time.time + 0.15f;
    }

    void WallJump(Vector3 normal, Collider wall)
    {
        Vector3 along = Vector3.ProjectOnPlane(planarVelocity, normal);
        planarVelocity = along + normal * wallJumpPush;
        verticalVelocity = Mathf.Sqrt(2f * gravity * wallJumpHeight);

        State = MoveState.Air;
        WallSide = 0;
        wallNormal = normal;
        lastWall = wall;
        lastWallBlockedUntil = Time.time + wallReattachDelay;
        anyWallBlockedUntil = Time.time + 0.2f;
        doubleJumpUsed = false;

        ConsumeJump();
        Jumped?.Invoke(JumpKind.Wall);
    }

    // ------------------------------------------------------------------
    // JUMPS (ground with coyote + buffer, slide, wall, double)
    // ------------------------------------------------------------------

    void HandleJump()
    {
        if (!JumpBuffered)
            return;

        if (State == MoveState.WallRun)
        {
            WallJump(wallNormal, lastWall);
            return;
        }

        bool coyote = Time.time - lastGroundedTime <= coyoteTime;

        if (coyote)
        {
            if (IsCrouched && !CanStandUp())
                return; // no room above

            JumpKind kind = State == MoveState.Slide ? JumpKind.Slide : JumpKind.Ground;
            State = MoveState.Air;
            DoJump(jumpHeight, kind);
            return;
        }

        // Airborne extras only react to a FRESH press (so a buffered press
        // can still turn into a ground jump when you land).
        if (!jumpPressed)
            return;

        for (int side = -1; side <= 1; side += 2)
        {
            if (FindWallRunWall(side, out RaycastHit hit))
            {
                WallJump(new Vector3(hit.normal.x, 0f, hit.normal.z).normalized, hit.collider);
                return;
            }
        }

        if (allowDoubleJump && !doubleJumpUsed)
        {
            doubleJumpUsed = true;

            // Let the double jump redirect you a bit toward your input.
            Vector3 wish = WishDirection();
            if (wish.sqrMagnitude > 0.01f)
                planarVelocity = Vector3.Lerp(planarVelocity, wish.normalized * planarVelocity.magnitude, 0.6f);

            DoJump(doubleJumpHeight, JumpKind.Double);
        }
    }

    void DoJump(float height, JumpKind kind)
    {
        verticalVelocity = Mathf.Sqrt(2f * gravity * height);

        // Consume both timers so one press = one jump.
        ConsumeJump();
        lastGroundedTime = -999f;
        IsGrounded = false;

        Jumped?.Invoke(kind);
    }

    void ApplyGravity(float dt)
    {
        if (State == MoveState.WallRun)
        {
            // Low gravity, getting heavier near the end of the run.
            float g = wallRunGravity * (wallRunTimer > wallRunMaxTime * 0.6f ? 2.5f : 1f);
            verticalVelocity = Mathf.Max(verticalVelocity - g * dt, -4f);
            return;
        }

        if (IsGrounded && verticalVelocity <= 0f)
        {
            // Stick to the ground so running DOWN ramps doesn't turn into
            // tiny falls (isGrounded flicker = jittery bob and no jumps).
            verticalVelocity = -Mathf.Max(3f, planarVelocity.magnitude * 0.6f);
            return;
        }

        float gravityNow = gravity;

        if (verticalVelocity < 0f)
            gravityNow *= fallGravityMultiplier;
        else if (!jumpHeld)
            gravityNow *= jumpCutGravityMultiplier;

        verticalVelocity = Mathf.Max(verticalVelocity - gravityNow * dt, -maxFallSpeed);
    }

    void AfterMove(CollisionFlags flags, bool wasGrounded, float vyBeforeMove, float dt)
    {
        // Bonk the ceiling -> stop rising.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        justLanded = false;

        if (IsGrounded)
        {
            if (State == MoveState.WallRun)
                ExitWallRun(0f);

            // Only count it as a landing after real airtime (not ramp flicker).
            if (!wasGrounded && airTime > 0.12f)
            {
                justLanded = true;
                Landed?.Invoke(Mathf.Max(0f, -vyBeforeMove));
            }

            airTime = 0f;
        }
        else
        {
            // Walked off a ledge: drop the ground-stick velocity so the fall
            // starts smoothly from zero instead of with a sudden yank.
            if (wasGrounded && verticalVelocity < 0f && State != MoveState.WallRun)
                verticalVelocity = 0f;

            airTime += dt;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.5f)
        {
            groundNormal = hit.normal;
            return;
        }

        if (Mathf.Abs(hit.normal.y) > 0.3f)
            return;

        // Slide along walls instead of keeping velocity pointed into them.
        Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
        float into = Vector3.Dot(planarVelocity, n);

        if (into < 0f)
            planarVelocity -= n * into;
    }

    // ------------------------------------------------------------------
    // VAULT / MANTLE
    // ------------------------------------------------------------------

    bool FindLedge(out Ledge ledge)
    {
        ledge = default;

        if (MoveInput.y <= 0.1f || State == MoveState.Slide || State == MoveState.WallRun)
            return false;

        Vector3 fwd = transform.forward;
        Vector3 feet = transform.position;
        float r = controller.radius;
        int mask = ParkourLayers.ObstacleMask;
        var q = QueryTriggerInteraction.Ignore;

        // 1. Is there a wall in front of the knees (or chest, for air grabs)?
        if (!Physics.Raycast(feet + Vector3.up * 0.45f, fwd, out RaycastHit wall, r + ledgeReach, mask, q) &&
            !Physics.Raycast(feet + Vector3.up * 1.1f, fwd, out wall, r + ledgeReach, mask, q))
            return false;

        if (Mathf.Abs(wall.normal.y) > 0.3f || Vector3.Dot(fwd, -wall.normal) < 0.5f)
            return false;

        Vector3 inward = -new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;

        // 2. Find the top: cast DOWN from above the max climb height, just
        //    past the wall face. Starting inside geometry = wall too tall.
        Vector3 origin = wall.point + inward * 0.4f;
        origin.y = feet.y + mantleMaxHeight + 0.1f;

        if (Physics.CheckSphere(origin, 0.05f, mask, q))
            return false;

        float downLength = origin.y - (feet.y + mantleMinHeight);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit top, downLength, mask, q) || top.normal.y < 0.7f)
            return false;

        // 3. Is there room to stand up there? (CheckCapsule)
        Vector3 stand = top.point + inward * 0.1f + Vector3.up * 0.03f;
        float cr = r * 0.95f;

        if (Physics.CheckCapsule(stand + Vector3.up * (cr + 0.05f), stand + Vector3.up * (standingHeight - cr), cr, mask, q))
            return false;

        ledge.top = stand;
        ledge.inward = inward;
        ledge.height = top.point.y - feet.y;
        return true;
    }

    bool ShouldMantle(Ledge ledge)
    {
        // Waist high: vault automatically while moving.
        if (ledge.height <= vaultMaxHeight)
            return Speed > 2.5f || !IsGrounded;

        // Chest high: needs Space (pressed on the ground, or held in the air).
        return JumpBuffered || (!IsGrounded && jumpHeld);
    }

    void BeginMantle(Ledge ledge)
    {
        mantleIsVault = ledge.height <= vaultMaxHeight;
        mantleStart = transform.position;
        mantleEnd = ledge.top;
        mantleDir = ledge.inward;
        mantleT = 0f;
        mantleTime = mantleIsVault ? vaultDuration : mantleDuration;
        mantleExitSpeed = mantleIsVault ? Mathf.Max(Speed, runSpeed) : Mathf.Max(Speed * 0.6f, 4f);

        State = MoveState.Mantle;
        WallSide = 0;
        verticalVelocity = 0f;
        doubleJumpUsed = false;
        ConsumeJump();
    }

    void UpdateMantle(float dt)
    {
        mantleT += dt / Mathf.Max(0.01f, mantleTime);
        float t = Mathf.Clamp01(mantleT);

        // Rise first (ease out), then move forward over the edge (smoothstep).
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.65f), 2f);
        float forward = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.75f));

        transform.position = new Vector3(
            Mathf.Lerp(mantleStart.x, mantleEnd.x, forward),
            Mathf.Lerp(mantleStart.y, mantleEnd.y, rise),
            Mathf.Lerp(mantleStart.z, mantleEnd.z, forward));

        // Tell PhysX the capsule moved (hazard checks see the new position,
        // and the next controller.Move() starts from here).
        Physics.SyncTransforms();

        UpdateHeight(dt);

        if (t >= 1f)
        {
            State = MoveState.Grounded;
            planarVelocity = mantleDir * mantleExitSpeed;
            verticalVelocity = -1f;
            IsGrounded = true;
            Landed?.Invoke(mantleIsVault ? 2f : 4f);
        }
    }

    // ------------------------------------------------------------------
    // BODY HEIGHT
    // ------------------------------------------------------------------

    // CheckCapsule against level geometry: is there room above us to stand?
    bool CanStandUp()
    {
        float r = controller.radius * 0.95f;
        Vector3 bottom = transform.position + Vector3.up * (r + 0.05f);
        Vector3 top = transform.position + Vector3.up * (standingHeight - r);

        return !Physics.CheckCapsule(bottom, top, r, ParkourLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
    }

    void UpdateHeight(float dt)
    {
        bool wantLow = State == MoveState.Slide || (crouchHeld && State != MoveState.Mantle);

        if (!wantLow && IsCrouched && !CanStandUp())
            wantLow = true; // stuck under something: stay crouched

        IsCrouched = wantLow;

        float targetHeight = wantLow ? crouchHeight : standingHeight;
        float targetEye = wantLow ? crouchEyeHeight : eyeHeight;

        float h = Mathf.MoveTowards(controller.height, targetHeight, heightChangeSpeed * dt);
        float t = Mathf.InverseLerp(standingHeight, crouchHeight, h);

        ApplyHeight(h, Mathf.Lerp(eyeHeight, crouchEyeHeight, t));
    }

    void ApplyHeight(float height, float eye)
    {
        controller.height = height;
        controller.center = new Vector3(0f, height * 0.5f, 0f);

        if (cameraRoot != null && cameraRoot != transform)
        {
            Vector3 p = cameraRoot.localPosition;
            p.y = eye;
            cameraRoot.localPosition = p;
        }
    }

    // ------------------------------------------------------------------
    // PUBLIC API (game manager)
    // ------------------------------------------------------------------

    public void Teleport(Vector3 position, float yaw)
    {
        // CharacterController overrides transform changes while enabled.
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;

        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        lastGroundedTime = lastJumpPressedTime = -999f;
        forwardHeldTime = airTime = 0f;
        IsSprinting = false;
        IsGrounded = false;
        IsCrouched = false;
        WallSide = 0;
        LedgeAvailable = false;
        State = MoveState.Air;

        ApplyHeight(standingHeight, eyeHeight);

        if (look != null)
            look.ResetLook();
    }
}
