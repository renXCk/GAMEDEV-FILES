using UnityEngine;

public class PlatformRotator : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float speed = 60f;

    void Update()
    {
        transform.Rotate(
            axis.normalized,
            speed * Time.deltaTime,
            Space.Self
        );
    }
}