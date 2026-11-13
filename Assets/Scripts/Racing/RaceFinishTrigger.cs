using UnityEngine;
[RequireComponent(typeof(BoxCollider))]
public class RaceFinishTrigger : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    private bool finished;
    private void OnTriggerEnter(Collider other)
    {
        if (!finished && other.attachedRigidbody != null && other.attachedRigidbody == session.CarBody)
            finished = session.TryFinish();
    }
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
}
