using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class VehicleHud : MonoBehaviour
{
    private const float TachometerMaximumRpm = 9000f;

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
        foreach (WheelController candidate in FindObjectsByType<WheelController>())
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
            ? new Color(1f, 0.3f, 0.22f)
            : controller.CurrentGear == 0
                ? new Color(0.58f, 0.68f, 0.72f)
                : Color.white;

        rpmText.text = $"{Mathf.RoundToInt(displayedRpm):0000} RPM";
        float redlineAmount = displayedRpm / Mathf.Max(1f, controller.redlineRpm);
        rpmText.color = redlineAmount >= 0.96f
            ? new Color(1f, 0.22f, 0.14f)
            : redlineAmount >= 0.82f
                ? new Color(1f, 0.67f, 0.16f)
                : new Color(0.9f, 0.94f, 0.96f);

        gauge.SetTelemetry(
            displayedRpm / TachometerMaximumRpm,
            controller.redlineRpm / TachometerMaximumRpm);
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

        GameObject canvasObject = new GameObject(
            "Vehicle HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.55f;

        GameObject gaugeObject = new GameObject(
            "JDM Drift Tachometer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(SpeedometerGaugeGraphic));
        gaugeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(1f, 0f);
        gaugeRect.anchorMax = new Vector2(1f, 0f);
        gaugeRect.pivot = new Vector2(1f, 0f);
        gaugeRect.anchoredPosition = new Vector2(-34f, 24f);
        gaugeRect.sizeDelta = new Vector2(400f, 400f);

        gauge = gaugeObject.GetComponent<SpeedometerGaugeGraphic>();
        gauge.raycastTarget = false;

        CreateRpmLabels(gaugeRect, font);

        Text gearCaption = CreateText(gaugeRect, "Gear Caption", font, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearCaption.rectTransform, new Vector2(0f, 73f), new Vector2(72f, 18f));
        gearCaption.text = "GEAR";
        gearCaption.color = new Color(0.75f, 0.76f, 0.78f, 0.8f);

        gearText = CreateText(gaugeRect, "Gear", font, 112, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearText.rectTransform, new Vector2(0f, 15f), new Vector2(165f, 142f));
        gearText.text = "1";
        AddShadow(gearText, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

        speedText = CreateText(gaugeRect, "Speed", font, 66, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedText.rectTransform, new Vector2(0f, -88f), new Vector2(210f, 78f));
        speedText.text = "0";
        AddShadow(speedText, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));

        Text speedUnit = CreateText(gaugeRect, "Speed Unit", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedUnit.rectTransform, new Vector2(0f, -123f), new Vector2(90f, 22f));
        speedUnit.text = "KM/H";
        speedUnit.color = new Color(0.82f, 0.82f, 0.84f);

        rpmText = CreateText(gaugeRect, "Rpm", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmText.rectTransform, new Vector2(0f, -149f), new Vector2(190f, 30f));
        rpmText.text = "0900 RPM";
    }

    private static void CreateRpmLabels(RectTransform parent, Font font)
    {
        Vector2 center = Vector2.zero;
        const float labelRadius = 151f;

        for (int rpm = 0; rpm <= 9; rpm++)
        {
            float t = rpm / 9f;
            float angle = Mathf.Lerp(SpeedometerGaugeGraphic.StartAngle, SpeedometerGaugeGraphic.EndAngle, t);
            Vector2 position = center + SpeedometerGaugeGraphic.Direction(angle) * labelRadius;
            Text label = CreateText(parent, "RPM " + rpm, font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, position, new Vector2(34f, 28f));
            label.text = rpm.ToString();
            label.color = rpm >= 8
                ? new Color(1f, 0.38f, 0.25f)
                : new Color(0.94f, 0.95f, 0.96f);
            AddShadow(label, new Color(0f, 0f, 0f, 0.9f), new Vector2(1f, -1f));
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

    private static void AddShadow(Text text, Color color, Vector2 distance)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }
}

internal sealed class SpeedometerGaugeGraphic : MaskableGraphic
{
    private const int ArcSegments = 72;
    private const int RpmSegments = 36;

    public const float StartAngle = 215f;
    public const float EndAngle = -35f;

    private float rpm01;
    private float redline01 = 0.84f;

    public void SetTelemetry(float rpm, float redline)
    {
        float nextRpm = Mathf.Clamp01(rpm);
        float nextRedline = Mathf.Clamp01(redline);
        if (Mathf.Abs(rpm01 - nextRpm) < 0.001f
            && Mathf.Abs(redline01 - nextRedline) < 0.001f)
            return;

        rpm01 = nextRpm;
        redline01 = nextRedline;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.46f;

        AddDisc(helper, center + new Vector2(4f, -6f), radius + 2f, new Color32(0, 0, 0, 72));
        AddDisc(helper, center, radius, new Color32(3, 4, 6, 162));
        AddDisc(helper, center, radius - 8f, new Color32(8, 8, 11, 132));
        AddCircleRing(helper, center, radius - 2f, 3f, new Color32(18, 19, 22, 210));
        AddCircleRing(helper, center, radius - 7f, 1.5f, new Color32(255, 52, 32, 150));

        DrawRpmScale(helper, center, radius);
        DrawCenterDisplay(helper, center);
        DrawRpmNeedle(helper, center, radius);

        AddDisc(helper, center, 12f, new Color32(15, 15, 17, 240), 24);
        AddDisc(helper, center, 5f, new Color32(255, 239, 225, 255), 20);
    }

    private void DrawRpmScale(VertexHelper helper, Vector2 center, float radius)
    {
        AddArc(helper, center, radius - 12f, 9f, 0f, 1f, new Color32(45, 35, 35, 175));

        float warningStart = Mathf.Max(0f, redline01 - 0.22f);
        for (int i = 0; i < RpmSegments; i++)
        {
            float from = i / (float)RpmSegments + 0.0035f;
            float to = (i + 1) / (float)RpmSegments - 0.0035f;
            float segmentCenter = (from + to) * 0.5f;
            bool active = segmentCenter <= rpm01;

            Color32 color;
            if (!active)
                color = segmentCenter >= warningStart
                    ? new Color32(92, 28, 21, 135)
                    : new Color32(44, 42, 44, 112);
            else if (segmentCenter >= redline01)
                color = new Color32(255, 49, 30, 255);
            else if (segmentCenter >= warningStart)
                color = new Color32(255, 113, 32, 255);
            else
                color = new Color32(230, 232, 234, 245);

            AddArc(helper, center, radius - 11f, 10f, from, to, color);
        }

        const int tickCount = 45;
        for (int i = 0; i <= tickCount; i++)
        {
            float t = i / (float)tickCount;
            bool major = i % 5 == 0;
            AddRadialLine(
                helper,
                center,
                radius - (major ? 39f : 31f),
                radius - 22f,
                Mathf.Lerp(StartAngle, EndAngle, t),
                major ? 3f : 1.35f,
                t >= redline01
                    ? new Color32(255, 75, 50, major ? (byte)245 : (byte)170)
                    : new Color32(225, 227, 230, major ? (byte)245 : (byte)145));
        }
    }

    private static void DrawCenterDisplay(VertexHelper helper, Vector2 center)
    {
        AddDisc(helper, center, 87f, new Color32(4, 4, 6, 154), 64);
        AddCircleRing(helper, center, 88f, 3f, new Color32(58, 58, 62, 190));
        AddArc(helper, center, 84f, 5f, 0.12f, 0.22f, new Color32(255, 51, 31, 240));
        AddArc(helper, center, 84f, 5f, 0.78f, 0.88f, new Color32(255, 51, 31, 240));

        AddQuad(
            helper,
            center + new Vector2(-105f, -61f),
            center + new Vector2(105f, -61f),
            center + new Vector2(88f, -158f),
            center + new Vector2(-88f, -158f),
            new Color32(3, 4, 6, 194));
        AddLine(helper, center + new Vector2(-97f, -62f), center + new Vector2(97f, -62f), 2f, new Color32(84, 84, 88, 180));
        AddLine(helper, center + new Vector2(-74f, -137f), center + new Vector2(74f, -137f), 1.5f, new Color32(145, 145, 150, 175));
    }

    private void DrawRpmNeedle(VertexHelper helper, Vector2 center, float radius)
    {
        float angle = Mathf.Lerp(StartAngle, EndAngle, rpm01);
        Vector2 direction = Direction(angle);
        Vector2 side = new Vector2(-direction.y, direction.x);
        AddQuad(
            helper,
            center - direction * 13f - side * 4f,
            center - direction * 13f + side * 4f,
            center + direction * (radius - 46f) + side * 1.4f,
            center + direction * (radius - 46f) - side * 1.4f,
            new Color32(255, 76, 40, 255));
    }

    private static void AddDisc(VertexHelper helper, Vector2 center, float radius, Color32 color, int segments = 72)
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

    private static void AddCircleRing(
        VertexHelper helper,
        Vector2 center,
        float radius,
        float thickness,
        Color32 color,
        int segments = 72)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle0 = i / (float)segments * 360f;
            float angle1 = (i + 1) / (float)segments * 360f;
            Vector2 direction0 = Direction(angle0);
            Vector2 direction1 = Direction(angle1);
            AddQuad(
                helper,
                center + direction0 * (radius - thickness),
                center + direction0 * radius,
                center + direction1 * radius,
                center + direction1 * (radius - thickness),
                color);
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
        int count = Mathf.Max(1, Mathf.CeilToInt(ArcSegments * Mathf.Max(0.001f, to - from)));
        for (int i = 0; i < count; i++)
        {
            float t0 = Mathf.Lerp(from, to, i / (float)count);
            float t1 = Mathf.Lerp(from, to, (i + 1) / (float)count);
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

    private static void AddLine(
        VertexHelper helper,
        Vector2 start,
        Vector2 end,
        float width,
        Color32 color)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
        AddQuad(helper, start - side, start + side, end + side, end - side, color);
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

    private static int AddVertex(VertexHelper helper, Vector2 position, Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        helper.AddVert(vertex);
        return helper.currentVertCount - 1;
    }

    public static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
