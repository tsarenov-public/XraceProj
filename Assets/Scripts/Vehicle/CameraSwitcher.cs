using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject[] cameras;  // 0 = задняя, 1 = кокпит
    [SerializeField] private KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2 };

    private void Start() => Select(0);

    private void Update()
    {
        if (Time.timeScale <= 0f || keys == null || cameras == null) return;
        for (int i = 0; i < keys.Length && i < cameras.Length; i++)
            if (Input.GetKeyDown(keys[i]))
                Select(i);
    }

    private void Select(int i)
    {
        if (cameras == null) return;
        for (int j = 0; j < cameras.Length; j++)
            if (cameras[j] != null) cameras[j].SetActive(j == i);   // выключает и камеру, и её скрипт, и её Audio Listener
    }
}
