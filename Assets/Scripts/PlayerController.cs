using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 12f;

    [Header("Jump")]
    public float jumpHeight = 2.5f;
    public float gravity = -20f;

    [Header("Jump Feel")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    private CharacterController controller;
    private Transform cameraTransform;
    private float verticalVelocity;
    private float coyoteTimer;
    private float jumpBufferTimer;

    private float speedBoostMultiplier = 1f;
    private float speedBoostTimer;

    public float SpeedBoostTimeLeft => Mathf.Max(0f, speedBoostTimer);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (speedBoostTimer > 0f)
            speedBoostTimer -= dt;

        // Read grounded BEFORE any Move() this frame.
        bool grounded = controller.isGrounded;

        if (grounded)
        {
            coyoteTimer = coyoteTime;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }
        else
        {
            coyoteTimer -= dt;
        }

        if (kb.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        verticalVelocity += gravity * dt;

        // Input
        Vector2 input = Vector2.zero;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.wKey.isPressed) input.y += 1f;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 direction = forward * input.y + right * input.x;
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        float speed = moveSpeed * (speedBoostTimer > 0f ? speedBoostMultiplier : 1f);

        // ONE Move() call.
        Vector3 motion = direction * speed;
        motion.y = verticalVelocity;
        controller.Move(motion * dt);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * dt
            );
        }
    }

    // ---- Used by the game manager / bonus panels ----

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedBoostMultiplier = multiplier;
        speedBoostTimer = duration;
    }

    public void ResetState()
    {
        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;
        verticalVelocity = 0f;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    public void Teleport(Vector3 position)
    {
        // CharacterController must be disabled or it overrides the new position.
        controller.enabled = false;
        transform.position = position;
        transform.rotation = Quaternion.identity;
        controller.enabled = true;
        verticalVelocity = 0f;
    }
}
