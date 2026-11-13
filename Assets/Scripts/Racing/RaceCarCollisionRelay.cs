using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public class RaceCarCollisionRelay : MonoBehaviour
{
    private Rigidbody body;
    private RaceSessionManager session;
    private DriftScorer scorer;

    public void Bind(RaceSessionManager raceSession, DriftScorer driftScorer)
    {
        body = GetComponent<Rigidbody>();
        session = raceSession;
        scorer = driftScorer;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled || body == null || session == null || scorer == null
            || !session.isActiveAndEnabled || !scorer.isActiveAndEnabled || session.CarBody != body
            || collision == null || collision.rigidbody == body) return;

        float deltaSpeed = collision.impulse.magnitude / Mathf.Max(0.01f, body.mass);
        for (int i = 0; i < collision.contactCount; i++)
        {
            // GetContact avoids allocating collision.contacts on every hit.
            if (scorer.NotifyCollision(deltaSpeed, collision.GetContact(i).normal)) break;
        }
    }
}
