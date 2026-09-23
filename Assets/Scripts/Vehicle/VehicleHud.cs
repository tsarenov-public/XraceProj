using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class VehicleHud : MonoBehaviour
{
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

        speedText.text = Mathf.RoundToInt(Mathf.Max(0f, displayedSpeed)).ToString("000");
        gearText.text = controller.GearLabel;
        gearText.color = controller.CurrentGear < 0
            ? new Color(1f, 0.3f, 0.24f)
            : controller.CurrentGear == 0
                ? new Color(0.62f, 0.7f, 0.76f)
                : Color.white;
        rpmText.text = $"RPM  {Mathf.RoundToInt(displayedRpm):N0}";

        float normalizedRpm = displayedRpm / Mathf.Max(1f, controller.redlineRpm);
        gauge.SetRpm(normalizedRpm);
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
        gaugeRect.anchoredPosition = new Vector2(-32f, 26f);
        gaugeRect.sizeDelta = new Vector2(330f, 270f);
        gauge = gaugeObject.GetComponent<SpeedometerGaugeGraphic>();
        gauge.raycastTarget = false;

        speedText = CreateText(gaugeRect, "Speed", font, 66, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedText.rectTransform, new Vector2(0f, -10f), new Vector2(205f, 80f));
        speedText.text = "000";
        AddOutline(speedText, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -2f));

        Text unitText = CreateText(gaugeRect, "Units", font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(unitText.rectTransform, new Vector2(0f, -58f), new Vector2(100f, 24f));
        unitText.text = "KM/H";
        unitText.color = new Color(0.45f, 0.84f, 0.9f);

        Text gearCaption = CreateText(gaugeRect, "Gear Caption", font, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearCaption.rectTransform, new Vector2(-83f, 61f), new Vector2(70f, 20f));
        gearCaption.text = "GEAR";
        gearCaption.color = new Color(0.45f, 0.84f, 0.9f);

        gearText = CreateText(gaugeRect, "Gear", font, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearText.rectTransform, new Vector2(-83f, 28f), new Vector2(72f, 52f));
        gearText.text = "1";
        AddOutline(gearText, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.5f, -1.5f));

        rpmText = CreateText(gaugeRect, "Rpm", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmText.rectTransform, new Vector2(67f, 43f), new Vector2(140f, 30f));
        rpmText.text = "RPM  900";
        rpmText.color = new Color(0.82f, 0.88f, 0.91f);

        Text shiftHint = CreateText(gaugeRect, "Shift Hint", font, 11, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(shiftHint.rectTransform, new Vector2(0f, -92f), new Vector2(190f, 22f));
        shiftHint.text = "Q  SHIFT  E";
        shiftHint.color = new Color(0.42f, 0.48f, 0.52f, 0.9f);
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
    private const float StartAngle = 210f;
    private const float EndAngle = -30f;

    private float rpm01;

    public void SetRpm(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Abs(rpm01 - clamped) < 0.001f)
            return;

        rpm01 = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center + new Vector2(0f, -7f);
        float radius = Mathf.Min(rect.width, rect.height) * 0.46f;

        AddDisc(vertexHelper, center, radius, new Color32(7, 11, 16, 220));
        AddArc(vertexHelper, center, radius - 5f, 11f, 0f, 1f, new Color32(45, 53, 61, 235));
        AddArc(vertexHelper, center, radius - 5f, 7f, 0f, rpm01, GaugeColor(rpm01));

        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f;
            bool major = i % 2 == 0;
            Color32 tickColor = t >= 0.86f
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

    private static Color32 GaugeColor(float value)
    {
        Color color = value < 0.82f
            ? Color.Lerp(new Color(0.18f, 0.72f, 0.82f), new Color(0.46f, 0.9f, 0.72f), value / 0.82f)
            : Color.Lerp(new Color(1f, 0.64f, 0.16f), new Color(1f, 0.2f, 0.12f), Mathf.InverseLerp(0.82f, 1f, value));
        return color;
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
