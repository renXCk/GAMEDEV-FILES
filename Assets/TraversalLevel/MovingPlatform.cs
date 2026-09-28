using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Vector3 movement = new Vector3(-14, 0, 0);
    public float speed = 0.2f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(startPosition, startPosition + movement, t);
    }
}