using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class VehicleHud : MonoBehaviour
{
    private const float GaugeMaximumRpm = 10000f;

    public WheelController controller;

    private Canvas canvas;
    private Text speedText;
    private Text gearText;
    private Text rpmText;
    private SpeedometerGaugeGraphic gauge;
    private float displayedSpeed;
    private float displayedRpm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForActiveVehicle()
    {
        WheelController[] controllers = FindObjectsByType<WheelController>();
        foreach (WheelController candidate in controllers)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (candidate.GetComponent<VehicleHud>() == null)
                candidate.gameObject.AddComponent<VehicleHud>();
            break;
        }
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<WheelController>();

        BuildInterface();
    }

    private void Update()
    {
        if (controller == null || speedText == null)
            return;

        float smoothing = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        displayedSpeed = Mathf.Lerp(displayedSpeed, controller.SpeedKph, smoothing);
        displayedRpm = Mathf.Lerp(displayedRpm, controller.EngineRpm, smoothing);

        speedText.text = Mathf.RoundToInt(Mathf.Max(0f, displayedSpeed)).ToString("0");
        gearText.text = controller.GearLabel;
        gearText.color = controller.CurrentGear < 0
            ? new Color(1f, 0.3f, 0.24f)
            : controller.CurrentGear == 0
                ? new Color(0.62f, 0.7f, 0.76f)
                : Color.white;
        rpmText.text = $"{Mathf.RoundToInt(displayedRpm):0000}  RPM";

        float normalizedRpm = displayedRpm / GaugeMaximumRpm;
        float normalizedRedline = controller.redlineRpm / GaugeMaximumRpm;
        gauge.SetRpm(normalizedRpm, normalizedRedline);
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void BuildInterface()
    {
        if (controller == null || canvas != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("Vehicle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.55f;

        GameObject gaugeObject = new GameObject("Speedometer", typeof(RectTransform), typeof(SpeedometerGaugeGraphic));
        gaugeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(1f, 0f);
        gaugeRect.anchorMax = new Vector2(1f, 0f);
        gaugeRect.pivot = new Vector2(1f, 0f);
        gaugeRect.anchoredPosition = new Vector2(-32f, 24f);
        gaugeRect.sizeDelta = new Vector2(330f, 300f);
        gauge = gaugeObject.GetComponent<SpeedometerGaugeGraphic>();
        gauge.raycastTarget = false;

        CreateRpmLabels(gaugeRect, font);

        gearText = CreateText(gaugeRect, "Gear", font, 104, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearText.rectTransform, new Vector2(0f, 16f), new Vector2(130f, 125f));
        gearText.text = "1";
        AddOutline(gearText, new Color(0f, 0f, 0f, 0.85f), new Vector2(3f, -3f));

        speedText = CreateText(gaugeRect, "Speed", font, 58, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedText.rectTransform, new Vector2(73f, -88f), new Vector2(135f, 70f));
        speedText.text = "0";
        AddOutline(speedText, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -2f));

        Text unitText = CreateText(gaugeRect, "Units", font, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(unitText.rectTransform, new Vector2(73f, -124f), new Vector2(90f, 22f));
        unitText.text = "KM/H";
        unitText.color = new Color(0.72f, 0.76f, 0.78f);

        rpmText = CreateText(gaugeRect, "Rpm", font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmText.rectTransform, new Vector2(0f, -51f), new Vector2(150f, 26f));
        rpmText.text = "0900  RPM";
        rpmText.color = new Color(0.78f, 0.82f, 0.84f);

        Text rpmScaleText = CreateText(gaugeRect, "Rpm Scale", font, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmScaleText.rectTransform, new Vector2(0f, -69f), new Vector2(100f, 18f));
        rpmScaleText.text = "RPM ×1000";
        rpmScaleText.color = new Color(0.48f, 0.52f, 0.54f);

        Text gameMark = CreateText(gaugeRect, "Game Mark", font, 17, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
        SetRect(gameMark.rectTransform, new Vector2(132f, -110f), new Vector2(58f, 25f));
        gameMark.text = "XR";
        gameMark.color = new Color(0.94f, 0.08f, 0.28f);
    }

    private static void CreateRpmLabels(RectTransform parent, Font font)
    {
        Vector2 center = new Vector2(0f, -7f);
        const float labelRadius = 104f;
        for (int value = 0; value <= 10; value++)
        {
            float t = value / 10f;
            float angle = Mathf.Lerp(SpeedometerGaugeGraphic.StartAngle, SpeedometerGaugeGraphic.EndAngle, t);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * labelRadius;
            Text label = CreateText(parent, "RPM " + value, font, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, position, new Vector2(28f, 22f));
            label.text = value.ToString();
            label.color = t >= 0.78f
                ? new Color(1f, 0.25f, 0.2f)
                : new Color(0.92f, 0.94f, 0.95f);
            AddOutline(label, new Color(0f, 0f, 0f, 0.8f), new Vector2(1f, -1f));
        }
    }

    private static Text CreateText(
        RectTransform parent,
        string name,
        Font font,
        int fontSize,
        FontStyle style,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AddOutline(Text text, Color color, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }
}

internal sealed class SpeedometerGaugeGraphic : MaskableGraphic
{
    private const int ArcSegments = 64;
    public const float StartAngle = 250f;
    public const float EndAngle = -20f;

    private float rpm01;
    private float redline01 = 0.78f;

    public void SetRpm(float value, float redline)
    {
        float clamped = Mathf.Clamp01(value);
        float clampedRedline = Mathf.Clamp01(redline);
        if (Mathf.Abs(rpm01 - clamped) < 0.001f && Mathf.Abs(redline01 - clampedRedline) < 0.001f)
            return;

        rpm01 = clamped;
        redline01 = clampedRedline;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center + new Vector2(0f, -7f);
        float radius = Mathf.Min(rect.width, rect.height) * 0.46f;

        AddDisc(vertexHelper, center, radius, new Color32(5, 8, 12, 205));
        AddArc(vertexHelper, center, radius - 5f, 12f, 0f, 1f, new Color32(56, 60, 64, 225));
        float normalRpm = Mathf.Min(rpm01, redline01);
        if (normalRpm > 0f)
            AddArc(vertexHelper, center, radius - 5f, 8f, 0f, normalRpm, new Color32(238, 242, 244, 255));
        if (rpm01 > redline01)
            AddArc(vertexHelper, center, radius - 5f, 8f, redline01, rpm01, new Color32(255, 49, 42, 255));

        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f;
            bool major = i % 2 == 0;
            Color32 tickColor = t >= redline01
                ? new Color32(255, 74, 58, 245)
                : new Color32(196, 213, 220, major ? (byte)235 : (byte)130);
            AddRadialLine(
                vertexHelper,
                center,
                radius - (major ? 28f : 23f),
                radius - 15f,
                Mathf.Lerp(StartAngle, EndAngle, t),
                major ? 3f : 1.5f,
                tickColor);
        }

        float needleAngle = Mathf.Lerp(StartAngle, EndAngle, rpm01);
        Vector2 direction = Direction(needleAngle);
        Vector2 side = new Vector2(-direction.y, direction.x);
        AddTriangle(
            vertexHelper,
            center - direction * 13f + side * 5f,
            center - direction * 13f - side * 5f,
            center + direction * (radius - 35f),
            new Color32(255, 88, 62, 255));
        AddDisc(vertexHelper, center, 8f, new Color32(225, 235, 238, 255), 20);
    }

    private static void AddDisc(VertexHelper helper, Vector2 center, float radius, Color32 color, int segments = 64)
    {
        int centerIndex = AddVertex(helper, center, color);
        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments;
            float t1 = (i + 1) / (float)segments;
            int a = AddVertex(helper, center + Direction(t0 * 360f) * radius, color);
            int b = AddVertex(helper, center + Direction(t1 * 360f) * radius, color);
            helper.AddTriangle(centerIndex, a, b);
        }
    }

    private static void AddArc(
        VertexHelper helper,
        Vector2 center,
        float radius,
        float thickness,
        float from,
        float to,
        Color32 color)
    {
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(ArcSegments * Mathf.Max(0.001f, to - from)));
        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = Mathf.Lerp(from, to, i / (float)segmentCount);
            float t1 = Mathf.Lerp(from, to, (i + 1) / (float)segmentCount);
            Vector2 d0 = Direction(Mathf.Lerp(StartAngle, EndAngle, t0));
            Vector2 d1 = Direction(Mathf.Lerp(StartAngle, EndAngle, t1));
            AddQuad(
                helper,
                center + d0 * (radius - thickness),
                center + d0 * radius,
                center + d1 * radius,
                center + d1 * (radius - thickness),
                color);
        }
    }

    private static void AddRadialLine(
        VertexHelper helper,
        Vector2 center,
        float innerRadius,
        float outerRadius,
        float angle,
        float width,
        Color32 color)
    {
        Vector2 direction = Direction(angle);
        Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
        AddQuad(
            helper,
            center + direction * innerRadius - side,
            center + direction * innerRadius + side,
            center + direction * outerRadius + side,
            center + direction * outerRadius - side,
            color);
    }

    private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        int index = helper.currentVertCount;
        AddVertex(helper, a, color);
        AddVertex(helper, b, color);
        AddVertex(helper, c, color);
        AddVertex(helper, d, color);
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        int index = helper.currentVertCount;
        AddVertex(helper, a, color);
        AddVertex(helper, b, color);
        AddVertex(helper, c, color);
        helper.AddTriangle(index, index + 1, index + 2);
    }

    private static int AddVertex(VertexHelper helper, Vector2 position, Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        helper.AddVert(vertex);
        return helper.currentVertCount - 1;
    }

    private static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
