using System.Collections.Generic;
using UnityEngine;

// Cameras and skidmark meshes use world space, but still belong to one car.
// Keep their lifetime tied to that car after they leave its hierarchy.
[DisallowMultipleComponent]
public sealed class VehicleRuntimeObjects : MonoBehaviour
{
    private sealed class OwnedObject
    {
        public GameObject value;
        public bool wasActive;
    }

    private readonly List<OwnedObject> objects = new List<OwnedObject>(3);

    public static void Register(GameObject owner, GameObject value)
    {
        if (owner == null || value == null || owner == value) return;
        VehicleRuntimeObjects lifetime = owner.GetComponent<VehicleRuntimeObjects>();
        if (lifetime == null) lifetime = owner.AddComponent<VehicleRuntimeObjects>();
        foreach (OwnedObject existing in lifetime.objects)
            if (existing.value == value) return;
        lifetime.objects.Add(new OwnedObject { value = value, wasActive = value.activeSelf });
        if (!owner.activeInHierarchy) value.SetActive(false);
    }

    private void OnDisable()
    {
        foreach (OwnedObject owned in objects)
        {
            if (owned.value == null) continue;
            owned.wasActive = owned.value.activeSelf;
            owned.value.SetActive(false);
        }
    }

    private void OnEnable()
    {
        foreach (OwnedObject owned in objects)
            if (owned.value != null) owned.value.SetActive(owned.wasActive);
    }

    private void OnDestroy()
    {
        foreach (OwnedObject owned in objects)
            if (owned.value != null) Destroy(owned.value);
        objects.Clear();
    }
}
