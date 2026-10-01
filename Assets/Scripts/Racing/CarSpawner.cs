using UnityEngine;

[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public class CarSpawner : MonoBehaviour
{
    [SerializeField] private Transform startSpawn;
    [SerializeField] private Rigidbody fallbackCar;
    [SerializeField] private Collider roadSurface;
    [SerializeField] private GameObject[] sceneVehicleObjects;
    [SerializeField] private RaceSessionManager session;
    private Rigidbody spawnedBody;
    private void Start() => Spawn();
    public Rigidbody Spawn()
    {
        if (spawnedBody != null) return spawnedBody;
        if (startSpawn == null || session == null) { Debug.LogError("CarSpawner: missing StartSpawn/session.", this); return null; }
        if (session.CarBody != null) return spawnedBody = session.CarBody;
        CarDefinition selected = SelectedCarService.Get();
        bool validPrefab = selected != null && selected.carPrefab != null
            && selected.carPrefab.GetComponentInChildren<Rigidbody>(true) != null;
        if (selected != null && selected.carPrefab != null && !validPrefab)
            Debug.LogError("Selected car prefab has no Rigidbody; using the assigned fallback car.", this);
        if (validPrefab)
        {
            if (sceneVehicleObjects != null)
                foreach (var existing in sceneVehicleObjects) if (existing != null) existing.SetActive(false);
            var car = Instantiate(selected.carPrefab, startSpawn.position, startSpawn.rotation);
            spawnedBody = car.GetComponentInChildren<Rigidbody>(true);
            if (spawnedBody == null) { Destroy(car); Debug.LogError("Selected car prefab has no Rigidbody.", this); return null; }
            if (car.GetComponent<SpawnedRaceCar>() == null) car.AddComponent<SpawnedRaceCar>();
        }
        else
        {
            // Result metadata must describe the fallback actually being driven.
            if (selected != null) SelectedCarService.Clear();
            spawnedBody = fallbackCar;
            if (spawnedBody == null) { Debug.LogError("Choose a car in Garage or assign the fallback car.", this); return null; }
            spawnedBody.gameObject.SetActive(true);
            spawnedBody.position = startSpawn.position;
            spawnedBody.rotation = startSpawn.rotation;
            if (spawnedBody.GetComponent<SpawnedRaceCar>() == null) spawnedBody.gameObject.AddComponent<SpawnedRaceCar>();
        }
        if (!spawnedBody.isKinematic) { spawnedBody.linearVelocity = Vector3.zero; spawnedBody.angularVelocity = Vector3.zero; }
        session.BindCar(spawnedBody);
        foreach (WheelSkid wheelSkid in spawnedBody.GetComponentsInChildren<WheelSkid>(true))
            wheelSkid.BindRoadSurface(roadSurface);
        var collisionRelay = spawnedBody.GetComponent<RaceCarCollisionRelay>();
        if (collisionRelay == null) collisionRelay = spawnedBody.gameObject.AddComponent<RaceCarCollisionRelay>();
        collisionRelay.Bind(session, session.GetComponent<DriftScorer>());
        // RuntimeInitializeOnLoadMethod runs only for the first scene. A car
        // created after leaving Garage needs its presentation installed here.
        if (spawnedBody.GetComponent<WheelController>() != null)
        {
            if (spawnedBody.GetComponent<VehiclePresentation>() == null) spawnedBody.gameObject.AddComponent<VehiclePresentation>();
            var vehicleHud = spawnedBody.GetComponent<VehicleHud>();
            if (vehicleHud == null) vehicleHud = spawnedBody.gameObject.AddComponent<VehicleHud>();
            vehicleHud.BindRaceSession(session);
        }
        return spawnedBody;
    }
}
