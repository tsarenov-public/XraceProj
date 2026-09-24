using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class VehicleHud : MonoBehaviour
{
    private const float DisplayMaximumSpeedKph = 200f;

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

        rpmText.text = $"{displayedRpm / 1000f:0.0}";
        float redlineAmount = displayedRpm / Mathf.Max(1f, controller.redlineRpm);
        rpmText.color = redlineAmount >= 0.96f
            ? new Color(1f, 0.22f, 0.14f)
            : redlineAmount >= 0.82f
                ? new Color(1f, 0.67f, 0.16f)
                : new Color(0.9f, 0.94f, 0.96f);

        float tachometerMaximum = Mathf.Max(1000f, Mathf.Ceil(controller.redlineRpm / 1000f) * 1000f);
        gauge.SetTelemetry(
            displayedSpeed / DisplayMaximumSpeedKph,
            displayedRpm / tachometerMaximum,
            controller.redlineRpm / tachometerMaximum);
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
            "Beam Style Speedometer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(SpeedometerGaugeGraphic));
        gaugeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(1f, 0f);
        gaugeRect.anchorMax = new Vector2(1f, 0f);
        gaugeRect.pivot = new Vector2(1f, 0f);
        gaugeRect.anchoredPosition = new Vector2(-34f, 26f);
        gaugeRect.sizeDelta = new Vector2(380f, 380f);

        gauge = gaugeObject.GetComponent<SpeedometerGaugeGraphic>();
        gauge.raycastTarget = false;

        CreateSpeedLabels(gaugeRect, font);

        Text gearCaption = CreateText(gaugeRect, "Gear Caption", font, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearCaption.rectTransform, new Vector2(0f, 91f), new Vector2(72f, 18f));
        gearCaption.text = "GEAR";
        gearCaption.color = new Color(0.48f, 0.58f, 0.62f);

        gearText = CreateText(gaugeRect, "Gear", font, 48, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearText.rectTransform, new Vector2(0f, 62f), new Vector2(92f, 62f));
        gearText.text = "1";
        AddShadow(gearText, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

        speedText = CreateText(gaugeRect, "Speed", font, 82, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedText.rectTransform, new Vector2(0f, -8f), new Vector2(210f, 96f));
        speedText.text = "0";
        AddShadow(speedText, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));

        Text speedUnit = CreateText(gaugeRect, "Speed Unit", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedUnit.rectTransform, new Vector2(0f, -57f), new Vector2(90f, 24f));
        speedUnit.text = "KM/H";
        speedUnit.color = new Color(0.56f, 0.66f, 0.7f);

        rpmText = CreateText(gaugeRect, "Rpm", font, 25, FontStyle.Bold, TextAnchor.MiddleRight);
        SetRect(rpmText.rectTransform, new Vector2(-35f, -101f), new Vector2(64f, 32f));
        rpmText.text = "0.9";

        Text rpmUnit = CreateText(gaugeRect, "Rpm Unit", font, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(rpmUnit.rectTransform, new Vector2(42f, -101f), new Vector2(90f, 28f));
        rpmUnit.text = "×1000 RPM";
        rpmUnit.color = new Color(0.48f, 0.58f, 0.62f);

        Text brand = CreateText(gaugeRect, "Brand", font, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(brand.rectTransform, new Vector2(0f, -134f), new Vector2(100f, 18f));
        brand.text = "XRACE";
        brand.color = new Color(0.18f, 0.68f, 0.86f, 0.85f);
    }

    private static void CreateSpeedLabels(RectTransform parent, Font font)
    {
        Vector2 center = Vector2.zero;
        const float labelRadius = 133f;

        for (int speed = 0; speed <= (int)DisplayMaximumSpeedKph; speed += 40)
        {
            float t = speed / DisplayMaximumSpeedKph;
            float angle = Mathf.Lerp(SpeedometerGaugeGraphic.StartAngle, SpeedometerGaugeGraphic.EndAngle, t);
            Vector2 position = center + SpeedometerGaugeGraphic.Direction(angle) * labelRadius;
            Text label = CreateText(parent, "Speed " + speed, font, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, position, new Vector2(44f, 22f));
            label.text = speed.ToString();
            label.color = new Color(0.68f, 0.75f, 0.78f);
            AddShadow(label, new Color(0f, 0f, 0f, 0.75f), new Vector2(1f, -1f));
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
    private const int RpmSegments = 30;

    public const float StartAngle = 220f;
    public const float EndAngle = -40f;

    private float speed01;
    private float rpm01;
    private float redline01 = 0.78f;

    public void SetTelemetry(float speed, float rpm, float redline)
    {
        float nextSpeed = Mathf.Clamp01(speed);
        float nextRpm = Mathf.Clamp01(rpm);
        float nextRedline = Mathf.Clamp01(redline);
        if (Mathf.Abs(speed01 - nextSpeed) < 0.001f
            && Mathf.Abs(rpm01 - nextRpm) < 0.001f
            && Mathf.Abs(redline01 - nextRedline) < 0.001f)
            return;

        speed01 = nextSpeed;
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

        AddDisc(helper, center + new Vector2(4f, -6f), radius + 2f, new Color32(0, 0, 0, 90));
        AddDisc(helper, center, radius, new Color32(5, 9, 13, 226));
        AddDisc(helper, center, radius - 8f, new Color32(10, 15, 20, 242));
        AddArc(helper, center, radius - 3f, 2f, 0f, 1f, new Color32(105, 126, 135, 110));

        DrawSpeedScale(helper, center, radius);
        DrawRpmSegments(helper, center, radius - 29f);
        DrawSpeedNeedle(helper, center, radius);

        AddDisc(helper, center, 15f, new Color32(12, 19, 24, 255), 24);
        AddDisc(helper, center, 7f, new Color32(238, 245, 247, 255), 20);
        AddDisc(helper, center, 3f, new Color32(31, 173, 220, 255), 16);
    }

    private void DrawSpeedScale(VertexHelper helper, Vector2 center, float radius)
    {
        AddArc(helper, center, radius - 13f, 5f, 0f, 1f, new Color32(43, 55, 62, 235));
        if (speed01 > 0f)
            AddArc(helper, center, radius - 13f, 5f, 0f, speed01, new Color32(41, 181, 224, 255));

        const int tickCount = 25;
        for (int i = 0; i <= tickCount; i++)
        {
            float t = i / (float)tickCount;
            bool major = i % 5 == 0;
            AddRadialLine(
                helper,
                center,
                radius - (major ? 31f : 25f),
                radius - 18f,
                Mathf.Lerp(StartAngle, EndAngle, t),
                major ? 2.5f : 1.25f,
                major ? new Color32(218, 229, 232, 230) : new Color32(126, 143, 150, 145));
        }
    }

    private void DrawRpmSegments(VertexHelper helper, Vector2 center, float radius)
    {
        for (int i = 0; i < RpmSegments; i++)
        {
            float from = i / (float)RpmSegments + 0.004f;
            float to = (i + 1) / (float)RpmSegments - 0.004f;
            float segmentCenter = (from + to) * 0.5f;
            bool active = segmentCenter <= rpm01;
            bool redline = segmentCenter >= redline01;

            Color32 color;
            if (!active)
                color = redline ? new Color32(76, 32, 29, 150) : new Color32(36, 48, 54, 180);
            else if (redline)
                color = new Color32(255, 58, 43, 255);
            else if (segmentCenter >= redline01 - 0.16f)
                color = new Color32(255, 172, 42, 255);
            else
                color = new Color32(222, 235, 238, 255);

            AddArc(helper, center, radius, 9f, from, to, color);
        }
    }

    private void DrawSpeedNeedle(VertexHelper helper, Vector2 center, float radius)
    {
        float angle = Mathf.Lerp(StartAngle, EndAngle, speed01);
        Vector2 direction = Direction(angle);
        Vector2 side = new Vector2(-direction.y, direction.x);
        AddQuad(
            helper,
            center - direction * 18f - side * 3f,
            center - direction * 18f + side * 3f,
            center + direction * (radius - 38f) + side * 1.25f,
            center + direction * (radius - 38f) - side * 1.25f,
            new Color32(242, 248, 250, 255));
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
