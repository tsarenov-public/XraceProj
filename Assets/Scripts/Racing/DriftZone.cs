using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(BoxCollider))]
public class DriftZone : MonoBehaviour
{
    [SerializeField, Min(1)] private int zoneId = 1;
    [SerializeField, Min(0)] private float minDriftTime = 0.35f;
    [SerializeField, Min(0)] private float minSpeedKmh = 25;
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private DriftScorer scorer;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    public int ZoneId => zoneId;
    public float MinDriftTime => minDriftTime;
    public float MinSpeedKmh => minSpeedKmh;
    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null || other.attachedRigidbody != session.CarBody) return;
        if (contacts.Add(other) && contacts.Count == 1) scorer.EnterZone(this);
    }
    private void OnTriggerExit(Collider other)
    {
        if (contacts.Remove(other) && contacts.Count == 0) scorer.ExitZone(this);
    }
    private void OnDisable()
    {
        if (contacts.Count > 0 && scorer != null) scorer.ExitZone(this);
        contacts.Clear();
    }
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        Gizmos.color = new Color(0, 1, 1, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
