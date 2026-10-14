using UnityEngine;

/// <summary>Randomized sinusoidal motion; hit targets switch to Rigidbody physics.</summary>
public class MovingTarget : MonoBehaviour
{
    public enum Motion { SideToSide, UpAndDown }
    public Motion motion = Motion.SideToSide;
    public float distance = 1.2f;
    public float speed = 1.2f;
    public float phase;
    Vector3 origin;
    bool stopped;

    void Start()
    {
        origin = transform.position;
        if (phase == 0f) phase = Random.Range(0f, Mathf.PI * 2f);
        if (speed <= 0f) speed = Random.Range(0.7f, 1.8f);
        if (distance <= 0f) distance = Random.Range(0.6f, 1.5f);
    }

    void Update()
    {
        if (stopped) return;
        float offset = Mathf.Sin(Time.time * speed + phase) * distance;
        transform.position = origin + (motion == Motion.SideToSide ? Vector3.right * offset : Vector3.up * offset);
    }

    public void StopMovement() { stopped = true; }
}
