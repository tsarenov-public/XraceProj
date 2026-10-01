using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class VehicleHud : MonoBehaviour
{
    private const float TachometerMaximumRpm = 9000f;

    public WheelController controller;
    [SerializeField] private RaceSessionManager raceSession;

    private Canvas canvas;
    private Text speedText;
    private Text gearText;
    private Text rpmText;
    private MinimalSpeedometerGraphic gauge;
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
        if (!UpdateVisibility()) return;
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
                ? new Color(0.65f, 0.7f, 0.72f)
                : Color.white;

        rpmText.text = Mathf.RoundToInt(displayedRpm).ToString("0");
        float redlineAmount = displayedRpm / Mathf.Max(1f, controller.redlineRpm);
        rpmText.color = redlineAmount >= 0.96f
            ? new Color(1f, 0.26f, 0.18f)
            : redlineAmount >= 0.82f
                ? new Color(1f, 0.67f, 0.2f)
                : new Color(0.9f, 0.94f, 0.95f);

        gauge.SetRpm(
            displayedRpm / TachometerMaximumRpm,
            controller.redlineRpm / TachometerMaximumRpm);
    }

    public void BindRaceSession(RaceSessionManager session)
    {
        raceSession = session;
        UpdateVisibility();
    }

    private void OnEnable() => UpdateVisibility();

    private void OnDisable()
    {
        // This canvas is a scene root, so disabling a replaced fallback car
        // does not hide it through the car hierarchy automatically.
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    private bool UpdateVisibility()
    {
        bool visible = isActiveAndEnabled && (raceSession == null
            || !raceSession.IsPaused && raceSession.State != RaceSessionManager.RaceState.Finished);
        if (canvas != null && canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        return visible;
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
            "Minimal Speedometer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MinimalSpeedometerGraphic));
        gaugeObject.transform.SetParent(canvasObject.transform, false);

        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(1f, 0f);
        gaugeRect.anchorMax = new Vector2(1f, 0f);
        gaugeRect.pivot = new Vector2(1f, 0f);
        gaugeRect.anchoredPosition = new Vector2(-34f, 26f);
        gaugeRect.sizeDelta = new Vector2(310f, 310f);

        gauge = gaugeObject.GetComponent<MinimalSpeedometerGraphic>();
        gauge.raycastTarget = false;

        speedText = CreateText(gaugeRect, "Speed", font, 86, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedText.rectTransform, new Vector2(0f, 24f), new Vector2(220f, 104f));
        speedText.text = "0";
        AddShadow(speedText, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));

        Text speedUnit = CreateText(gaugeRect, "Speed Unit", font, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(speedUnit.rectTransform, new Vector2(0f, -27f), new Vector2(90f, 22f));
        speedUnit.text = "KM/H";
        speedUnit.color = new Color(0.62f, 0.7f, 0.73f);

        Text gearCaption = CreateText(gaugeRect, "Gear Caption", font, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearCaption.rectTransform, new Vector2(-69f, -45f), new Vector2(54f, 18f));
        gearCaption.text = "GEAR";
        gearCaption.color = new Color(0.65f, 0.7f, 0.72f);

        gearText = CreateText(gaugeRect, "Gear", font, 39, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(gearText.rectTransform, new Vector2(-69f, -82f), new Vector2(58f, 54f));
        gearText.text = "1";
        AddShadow(gearText, new Color(0f, 0f, 0f, 0.88f), new Vector2(2f, -2f));

        Text rpmCaption = CreateText(gaugeRect, "Rpm Caption", font, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmCaption.rectTransform, new Vector2(54f, -45f), new Vector2(66f, 18f));
        rpmCaption.text = "RPM";
        rpmCaption.color = new Color(0.65f, 0.7f, 0.72f);

        rpmText = CreateText(gaugeRect, "Rpm", font, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(rpmText.rectTransform, new Vector2(54f, -82f), new Vector2(92f, 34f));
        rpmText.text = "900";
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

internal sealed class MinimalSpeedometerGraphic : MaskableGraphic
{
    private const int ArcSegments = 72;
    private const float StartAngle = 215f;
    private const float EndAngle = -35f;

    private float rpm01;
    private float redline01 = 0.84f;

    public void SetRpm(float rpm, float redline)
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

        AddDisc(helper, center + new Vector2(4f, -5f), radius + 1f, new Color32(0, 0, 0, 64));
        AddDisc(helper, center, radius, new Color32(5, 9, 12, 150));
        AddCircleRing(helper, center, radius, 2f, new Color32(120, 139, 146, 105));
        AddCircleRing(helper, center, radius - 7f, 1f, new Color32(190, 207, 212, 55));

        DrawRpmArc(helper, center, radius - 11f);

        AddDisc(helper, center + new Vector2(-69f, -82f), 30f, new Color32(8, 12, 15, 188), 40);
        AddCircleRing(helper, center + new Vector2(-69f, -82f), 30f, 2f, new Color32(235, 74, 50, 205), 40);
        AddLine(
            helper,
            center + new Vector2(-7f, -54f),
            center + new Vector2(-7f, -109f),
            1.5f,
            new Color32(121, 139, 146, 105));
    }

    private void DrawRpmArc(VertexHelper helper, Vector2 center, float radius)
    {
        AddArc(helper, center, radius, 7f, 0f, 1f, new Color32(42, 53, 58, 205));

        float normalEnd = Mathf.Min(rpm01, Mathf.Max(0f, redline01 - 0.14f));
        if (normalEnd > 0f)
            AddArc(helper, center, radius, 7f, 0f, normalEnd, new Color32(226, 236, 239, 245));

        float warningStart = Mathf.Max(0f, redline01 - 0.14f);
        float warningEnd = Mathf.Min(rpm01, redline01);
        if (warningEnd > warningStart)
            AddArc(helper, center, radius, 7f, warningStart, warningEnd, new Color32(255, 166, 42, 255));

        if (rpm01 > redline01)
            AddArc(helper, center, radius, 7f, redline01, rpm01, new Color32(255, 55, 38, 255));

        AddArc(helper, center, radius, 7f, redline01, 1f, new Color32(105, 32, 27, 145));
        AddRadialLine(
            helper,
            center,
            radius - 11f,
            radius + 2f,
            Mathf.Lerp(StartAngle, EndAngle, redline01),
            2f,
            new Color32(255, 71, 48, 235));
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
            Vector2 direction0 = Direction(i / (float)segments * 360f);
            Vector2 direction1 = Direction((i + 1) / (float)segments * 360f);
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
            Vector2 direction0 = Direction(Mathf.Lerp(StartAngle, EndAngle, t0));
            Vector2 direction1 = Direction(Mathf.Lerp(StartAngle, EndAngle, t1));
            AddQuad(
                helper,
                center + direction0 * (radius - thickness),
                center + direction0 * radius,
                center + direction1 * radius,
                center + direction1 * (radius - thickness),
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

    private static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
