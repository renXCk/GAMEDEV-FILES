using UnityEngine;

public class BouncingBall : MonoBehaviour
{
    public float lifetime = 8f;
    public float impactForce = 8f;

    void Start() { Destroy(gameObject, Mathf.Max(0.2f, lifetime)); }

    void OnCollisionEnter(Collision collision)
    {
        Target target = collision.collider.GetComponentInParent<Target>();
        if (target != null)
        {
            Vector3 direction = collision.contactCount > 0 ? collision.GetContact(0).normal * -1f : transform.forward;
            target.Hit(direction, impactForce, collision.relativeVelocity.magnitude);
        }
    }
}
