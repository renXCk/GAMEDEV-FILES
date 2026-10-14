using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Horizontal-only movement on a fixed rail, with constrained mouse look.</summary>
public class RailPlayerController : MonoBehaviour
{
    [Header("Rail movement")]
    public float railMinX = -3.5f;
    public float railMaxX = 3.5f;
    public float moveSpeed = 5f;
    public float acceleration = 18f;
    public float deceleration = 22f;

    [Header("Mouse look")]
    public Transform cameraPivot;
    public float mouseSensitivity = 0.12f;
    public float yawLimit = 60f;
    public float pitchLimit = 30f;

    float velocityX;
    float yaw;
    float pitch;
    bool cursorLocked = true;

    void Start()
    {
        LockCursor(true);
        if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
    }

    void Update()
    {
        HandleCursor();
        if (!cursorLocked || Keyboard.current == null) return;

        float input = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input += 1f;

        float targetVelocity = input * moveSpeed;
        velocityX = Mathf.MoveTowards(velocityX, targetVelocity, (Mathf.Abs(input) > 0.01f ? acceleration : deceleration) * Time.deltaTime);
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x + velocityX * Time.deltaTime, railMinX, railMaxX);
        if ((p.x <= railMinX && velocityX < 0) || (p.x >= railMaxX && velocityX > 0)) velocityX = 0;
        transform.position = p;

        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw = Mathf.Clamp(yaw + delta.x * mouseSensitivity, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -pitchLimit, pitchLimit);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !cursorLocked) LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
