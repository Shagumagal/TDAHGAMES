using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SSTTimerHUD : MonoBehaviour
{
    public enum Mode { Countdown, Countup }

    [Header("Setup")]
    public Canvas targetCanvas;
    public TextMeshProUGUI label;
    public Image bg;
    public Mode mode = Mode.Countdown;

    [Header("Duración")]
    public float totalSeconds = 90f;        // si no usas el manager, puedes setearlo a mano
    public bool autoComputeFromManager = true;
    public SSTSemaforoManager manager;      // arrástralo o se busca solo en escena
    public bool includeCountdownSeconds = true; // sumar la cuenta atrás al estimado

    [Header("Estética")]
    public Vector2 offset = new Vector2(-24f, -24f);
    public Color bgColor = new Color(0f, 0f, 0f, 0.45f);
    public Color txtColor = new Color32(240, 244, 255, 255);
    public int fontMin = 20, fontMax = 34;
    public bool pulseOnFinish = true;

    // runtime
    float _startReal;
    bool _running;

    void Awake()
    {
        if (!Application.isPlaying) return;
        BuildIfNeeded();

        if (autoComputeFromManager)
            ComputeFromManager();

        UpdateLabel(0f); // estado inicial
    }

    void BuildIfNeeded()
    {
        if (!targetCanvas)
        {
            targetCanvas = FindObjectOfType<Canvas>();
            if (!targetCanvas)
            {
                var goCanvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = goCanvas.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = goCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        // raíz
        if (!bg)
        {
            var root = new GameObject("TimerHUD", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(targetCanvas.transform, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(156, 56);

            bg = root.GetComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = false;

            // label
            var lgo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lgo.transform.SetParent(root.transform, false);
            var lrt = lgo.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(140, 40);

            label = lgo.GetComponent<TextMeshProUGUI>();
            label.enableAutoSizing = true;
            label.fontSizeMin = fontMin;
            label.fontSizeMax = fontMax;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.color = txtColor;
            label.text = "00:00";
            label.raycastTarget = false;
        }
    }

    /// <summary>Calcula totalSeconds a partir de SSTSemaforoManager.</summary>
    public void ComputeFromManager()
    {
        if (!manager) manager = FindObjectOfType<SSTSemaforoManager>();
        if (!manager) return;

        int trials = manager.blocks * manager.trialsPerBlock;
        float itiAvg = (manager.itiMin + manager.itiMax) * 0.5f;
        float stim = manager.stimDurationMs / 1000f;

        totalSeconds = trials * (stim + itiAvg);
        if (includeCountdownSeconds) totalSeconds += Mathf.Max(0, manager.countdownSeconds);

        // pequeña corrección por fades/eventos (opcional)
        totalSeconds += 2f;
    }

    public void StartTimer()
    {
        _startReal = Time.realtimeSinceStartup;
        _running = true;
    }

    public void StopTimer()
    {
        _running = false;
    }

    void Update()
    {
        if (!_running || label == null) return;

        float elapsed = Time.realtimeSinceStartup - _startReal;
        float t = mode == Mode.Countdown ? Mathf.Max(0f, totalSeconds - elapsed) : elapsed;

        UpdateLabel(t);

        // color de alerta al final (cuenta regresiva)
        if (mode == Mode.Countdown)
        {
            if (t <= 10f) label.color = Color.Lerp(txtColor, new Color32(255, 80, 80, 255), 0.7f);
            else if (t <= 20f) label.color = Color.Lerp(txtColor, new Color32(255, 200, 0, 255), 0.5f);

            if (t <= 0.01f && pulseOnFinish)
            {
                // pequeño pulso visual
                var rt = bg.rectTransform;
                float s = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 8f);
                rt.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    void UpdateLabel(float seconds)
    {
        int mm = Mathf.FloorToInt(seconds / 60f);
        int ss = Mathf.FloorToInt(seconds % 60f);
        label.text = $"{mm:00}:{ss:00}";
    }
}
