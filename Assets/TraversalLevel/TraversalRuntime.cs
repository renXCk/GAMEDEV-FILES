using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TraversalPlayer : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpHeight = 2.5f;
    public float gravity = -22f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 spawnPoint;
    private Transform cam;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        spawnPoint = transform.position;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void Update()
    {
        // Respawn if player falls off platforms
        if (transform.position.y < -10f)
        {
            Respawn();
            return;
        }

        // 1. Gather Inputs
        float h = 0f;
        float v = 0f;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (h == 0f && v == 0f)
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
        }
        if (!jumpPressed)
        {
            jumpPressed = Input.GetButtonDown("Jump");
        }
#endif

        // 2. Check Ground Status
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Downward force to stay snapped to ground
        }

        // 3. Jump Trigger
        if (isGrounded && jumpPressed)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 4. Direction Relative to Camera
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        Vector3 right = cam != null ? cam.right : Vector3.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * v + right * h;
        if (moveDir.magnitude > 1f)
            moveDir.Normalize();

        // 5. Apply Gravity
        velocity.y += gravity * Time.deltaTime;

        // 6. SINGLE Move Call (Combines Horizontal + Vertical Jump/Gravity)
        Vector3 finalMotion = (moveDir * moveSpeed + velocity) * Time.deltaTime;
        controller.Move(finalMotion);

        // 7. Player Character Facing Direction
        if (moveDir.sqrMagnitude > 0.01f)
        {
            transform.forward = Vector3.Lerp(
                transform.forward,
                moveDir,
                10f * Time.deltaTime
            );
        }
    }

    void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPoint;
        velocity = Vector3.zero;
        controller.enabled = true;
    }
}


public class LevelComplete : MonoBehaviour
{
    public static bool completed;

    void Start()
    {
        completed = false;
    }

    void OnGUI()
    {
        if (!completed)
            return;

        GUIStyle title = new GUIStyle(GUI.skin.label);

        title.fontSize = 48;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = Color.white;

        GUIStyle text = new GUIStyle(GUI.skin.label);

        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.normal.textColor = Color.white;

        GUI.Label(
            new Rect(0, Screen.height * 0.40f, Screen.width, 70),
            "LEVEL COMPLETE!",
            title
        );

        GUI.Label(
            new Rect(0, Screen.height * 0.52f, Screen.width, 40),
            "You reached the end platform.",
            text
        );
    }
}

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 8, -11);

    void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = Vector3.Lerp(
            transform.position,
            target.position + offset,
            6f * Time.deltaTime
        );

        transform.LookAt(target.position + Vector3.up);
    }
}