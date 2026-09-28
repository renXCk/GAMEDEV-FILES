using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<TraversalPlayer>() != null)
        {
            LevelComplete.completed = true;
        }
    }
}