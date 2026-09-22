using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class VehiclePaintController : MonoBehaviour
{
    [Header("Окраска")]
    public Renderer[] paintRenderers;
    public Color paintColor = new Color(0.78f, 0.035f, 0.025f, 1f);
    public Color accentColor = new Color(0.025f, 0.025f, 0.03f, 1f);

    [Header("Смена цвета")]
    public KeyCode cycleColorKey = KeyCode.P;
    public Color[] paintPalette =
    {
        new Color(0.78f, 0.035f, 0.025f, 1f),
        new Color(0.92f, 0.92f, 0.9f, 1f),
        new Color(0.03f, 0.12f, 0.42f, 1f),
        new Color(0.06f, 0.06f, 0.065f, 1f)
    };

    private int paletteIndex;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyPaint();
    }

    private void OnValidate()
    {
        ApplyPaint();
    }

    private void Update()
    {
        if (!Application.isPlaying || !Input.GetKeyDown(cycleColorKey) || paintPalette == null || paintPalette.Length == 0)
            return;

        paletteIndex = (paletteIndex + 1) % paintPalette.Length;
        paintColor = paintPalette[paletteIndex];
        ApplyPaint();
    }

    public void ApplyPaint()
    {
        if (paintRenderers == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer targetRenderer in paintRenderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                string materialName = material.name.ToLowerInvariant();
                bool isPrimary = materialName.Contains("primary");
                bool isSecondary = materialName.Contains("secondary");
                if (!isPrimary && !isSecondary)
                    continue;

                Color color = isPrimary ? paintColor : accentColor;
                targetRenderer.GetPropertyBlock(propertyBlock, i);
                if (material.HasProperty("_BaseColor"))
                    propertyBlock.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(propertyBlock, i);
                propertyBlock.Clear();
            }
        }
    }
}
