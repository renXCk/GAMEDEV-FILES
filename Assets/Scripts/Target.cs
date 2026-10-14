using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Target : MonoBehaviour
{
    public float knockedAngle = 45f;
    public float belowHeight = -1.5f;
    public float hitThreshold = 2.2f;
    public bool physicsOnHit;
    public GameManager gameManager;

    Rigidbody body;
    bool knocked;

    public bool IsKnockedDown => knocked;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public virtual void Hit(Vector3 direction, float force, float impactSpeed)
    {
        if (knocked) return;
        if (physicsOnHit && impactSpeed < hitThreshold) return;
        MovingTarget moving = GetComponent<MovingTarget>();
        if (moving != null) moving.StopMovement();
        body.isKinematic = false;
        body.useGravity = true;
        body.AddForce(direction.normalized * force, ForceMode.Impulse);
        body.AddTorque(Random.insideUnitSphere * force * 0.35f, ForceMode.Impulse);
    }

    void Update()
    {
        if (knocked) return;
        float angle = Vector3.Angle(transform.up, Vector3.up);
        if (angle > knockedAngle || transform.position.y < belowHeight)
        {
            knocked = true;
            if (gameManager != null) gameManager.TargetKnockedDown(this);
        }
    }
}
