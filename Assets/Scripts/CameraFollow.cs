using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    public Vector3 offset =
        new Vector3(0f, 6.5f, -9f);

    public float followSpeed = 8f;

    void Start()
    {
        // Safety: if the camera is parented to the player it will inherit
        // the player's rotation and break camera-relative movement.
        if (transform.parent != null)
            transform.SetParent(null);

        // Set the rotation ONCE so it never changes while following.
        // Same angle LookAt would give from the offset, just not per-frame.
        transform.rotation =
            Quaternion.LookRotation(Vector3.up - offset);

        if (target != null)
            transform.position = target.position + offset;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition =
            target.position + offset;

        transform.position =
            Vector3.Lerp(
                transform.position,
                desiredPosition,
                followSpeed * Time.deltaTime
            );
    }
}
