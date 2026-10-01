using UnityEngine;

[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public class CarSpawner : MonoBehaviour
{
    [SerializeField] private Transform startSpawn;
    [SerializeField] private Rigidbody fallbackCar;
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
        if (selected != null && selected.carPrefab != null)
        {
            foreach (var existing in sceneVehicleObjects) if (existing != null) existing.SetActive(false);
            var car = Instantiate(selected.carPrefab, startSpawn.position, startSpawn.rotation);
            spawnedBody = car.GetComponentInChildren<Rigidbody>();
            if (spawnedBody == null) { Destroy(car); Debug.LogError("Selected car prefab has no Rigidbody.", this); return null; }
            car.AddComponent<SpawnedRaceCar>();
        }
        else
        {
            spawnedBody = fallbackCar;
            if (spawnedBody == null) { Debug.LogError("Choose a car in Garage or assign the fallback car.", this); return null; }
            spawnedBody.gameObject.SetActive(true);
            spawnedBody.position = startSpawn.position;
            spawnedBody.rotation = startSpawn.rotation;
            if (spawnedBody.GetComponent<SpawnedRaceCar>() == null) spawnedBody.gameObject.AddComponent<SpawnedRaceCar>();
        }
        if (!spawnedBody.isKinematic) { spawnedBody.linearVelocity = Vector3.zero; spawnedBody.angularVelocity = Vector3.zero; }
        session.BindCar(spawnedBody);
        return spawnedBody;
    }
}
