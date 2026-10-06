using UnityEngine;

public class StartingGate : MonoBehaviour
{
    public float delay = 5f;

    private Collider gateCollider;

    void Start()
    {
        gateCollider = GetComponent<Collider>();

        if (gateCollider != null)
        {
            gateCollider.enabled = true;
        }

        Invoke(nameof(OpenGate), delay);
    }

    void OpenGate()
    {
        if (gateCollider != null)
        {
            gateCollider.enabled = false;
        }

        Debug.Log("STARTING GATE OPEN!");
    }
}