using UnityEngine;

// Moves toward the start of the corridor and damages the player on contact.
public class Laser : MonoBehaviour
{
    private float speed;
    private float damage;
    private float despawnZ;
    private CharacterController body;
    private PlayerHealth health;

    public void Init(float speed, float damage, float despawnZ,
                     CharacterController body, PlayerHealth health)
    {
        this.speed = speed;
        this.damage = damage;
        this.despawnZ = despawnZ;
        this.body = body;
        this.health = health;
    }

    void Update()
    {
        transform.position += Vector3.back * speed * Time.deltaTime;

        // Lasers are axis-aligned boxes, so a simple bounds check is exact.
        if (body != null && health != null)
        {
            Bounds laserBounds = new Bounds(transform.position, transform.localScale);
            if (laserBounds.Intersects(body.bounds))
                health.TakeDamage(damage);
        }

        if (transform.position.z < despawnZ)
            Destroy(gameObject);
    }
}
