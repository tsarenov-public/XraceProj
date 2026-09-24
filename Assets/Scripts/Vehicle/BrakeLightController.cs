using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrakeLightController : MonoBehaviour
{
    [Header("Источники состояния")]
    [SerializeField] private PopupHeadlights headlights;
    [SerializeField] private WheelController wheels;

    [Header("Материал рассеивателя")]
    [Tooltip("Материал используется только для поиска нужных слотов рендереров. Сам asset материала не изменяется.")]
    [SerializeField] private Material taillightLens;

    [Header("Свечение")]
    [ColorUsage(true, true)] public Color tailColor = new Color(0.65f, 0.003f, 0.001f, 1f);
    [ColorUsage(true, true)] public Color brakeColor = new Color(4f, 0.012f, 0.003f, 1f);

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private const float BrakeThreshold = 0.05f;

    private readonly List<LampBinding> lamps = new List<LampBinding>();
    private LampState currentState = LampState.Unknown;
    private bool initialized;

    private enum LampState
    {
        Unknown,
        Off,
        Tail,
        Brake
    }

    private sealed class LampBinding
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color OriginalEmission;
        public MaterialPropertyBlock PropertyBlock;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        currentState = LampState.Unknown;
        RefreshLights();
    }

    private void Update()
    {
        RefreshLights();
    }

    private void OnDisable()
    {
        RestoreOriginalEmission();
    }

    private void OnDestroy()
    {
        RestoreOriginalEmission();
    }

    private void Initialize()
    {
        if (initialized) return;

        if (wheels == null) wheels = GetComponentInParent<WheelController>();
        if (headlights == null) headlights = GetComponentInParent<PopupHeadlights>();

        FindLampRenderers();
        initialized = true;

        if (wheels == null)
        {
            Debug.LogWarning($"{nameof(BrakeLightController)} on '{name}' could not find a {nameof(WheelController)}.", this);
        }

        if (lamps.Count == 0)
        {
            Debug.LogWarning($"{nameof(BrakeLightController)} on '{name}' could not find a renderer using the taillight lens material.", this);
        }
    }

    private void FindLampRenderers()
    {
        lamps.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                Material sharedMaterial = sharedMaterials[materialIndex];
                if (!IsTaillightMaterial(sharedMaterial)) continue;

                Color originalEmission = Color.black;
                if (sharedMaterial != null && sharedMaterial.HasProperty(EmissionColor))
                {
                    originalEmission = sharedMaterial.GetColor(EmissionColor);
                }

                lamps.Add(new LampBinding
                {
                    Renderer = targetRenderer,
                    MaterialIndex = materialIndex,
                    OriginalEmission = originalEmission,
                    PropertyBlock = new MaterialPropertyBlock()
                });
            }
        }
    }

    private bool IsTaillightMaterial(Material material)
    {
        if (material == null) return false;
        if (taillightLens != null) return material == taillightLens;

        return material.name.IndexOf("TaillightLens", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RefreshLights()
    {
        if (!initialized || lamps.Count == 0) return;

        bool braking = wheels != null && (wheels.BrakeInput > BrakeThreshold || wheels.IsHandbraking);
        bool tailLightsOn = headlights != null && headlights.IsRaised;
        LampState requestedState = braking ? LampState.Brake : tailLightsOn ? LampState.Tail : LampState.Off;

        if (requestedState == currentState) return;

        currentState = requestedState;
        ApplyEmission(requestedState);
    }

    private void ApplyEmission(LampState state)
    {
        foreach (LampBinding lamp in lamps)
        {
            if (lamp.Renderer == null) continue;

            Color emission = state switch
            {
                LampState.Brake => brakeColor,
                LampState.Tail => tailColor,
                _ => lamp.OriginalEmission
            };

            lamp.Renderer.GetPropertyBlock(lamp.PropertyBlock, lamp.MaterialIndex);
            lamp.PropertyBlock.SetColor(EmissionColor, emission);
            lamp.Renderer.SetPropertyBlock(lamp.PropertyBlock, lamp.MaterialIndex);
        }
    }

    private void RestoreOriginalEmission()
    {
        foreach (LampBinding lamp in lamps)
        {
            if (lamp.Renderer == null) continue;

            lamp.Renderer.GetPropertyBlock(lamp.PropertyBlock, lamp.MaterialIndex);
            lamp.PropertyBlock.SetColor(EmissionColor, lamp.OriginalEmission);
            lamp.Renderer.SetPropertyBlock(lamp.PropertyBlock, lamp.MaterialIndex);
        }

        currentState = LampState.Unknown;
    }
}
