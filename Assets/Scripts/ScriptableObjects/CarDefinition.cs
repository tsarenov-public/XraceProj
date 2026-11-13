using UnityEngine;

[CreateAssetMenu(menuName = "Xrace/Car Definition")]
public class CarDefinition : ScriptableObject
{
    public string carId;
    public string displayName;
    public GameObject carPrefab;
    public Sprite previewImage;
    public float power;
    public float grip;
    public float weight;
    public float topSpeed;
}
