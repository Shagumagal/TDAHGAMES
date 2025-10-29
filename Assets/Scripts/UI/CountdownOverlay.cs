using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Overlay de cuenta regresiva reutilizable. Construye su UI en el Canvas,
/// muestra un número grande 5→0 con fade/escala y opcionalmente reproduce sonidos.
/// Uso: yield return CountdownOverlay.Instance.Run(5, "¡Prepárate!", tick, final);
/// </summary>
public class CountdownOverlay : MonoBehaviour
{
    public static CountdownOverlay Instance { get; private set; }

    [Header("Refs autogeneradas")]
    public Canvas targetCanvas;
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleTMP;   // texto arriba (ej. "¡Prepárate!")
    public TextMeshProUGUI countTMP;   // número grande
    public Image ringImage;            // opcional: anillo de progreso (fill radial)

    [Header("Estilo")]
    public Color overlayColor = new Color(0, 0, 0, 0.45f);
    public Color numberColor  = new Color32(255, 255, 255, 255);
    public float fadeIn = 0.2f, fadeOut = 0.2f;
    public float pulseScale = 1.15f;   // pequeño “pop” al cambiar de número
    public float pulseTime  = 0.15f;

    [Header("Audio (opcional)")]
    public AudioSource sfxSource;      // asigna uno global o se crea temporal
    public AudioClip tickClip;         // sonido por segundo
    public AudioClip finalClip;        // sonido al terminar

    bool built;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (!Application.isPlaying) return;
        if (!built || !canvasGroup) BuildInScene();
        HideImmediate();
    }

    // ===== API principal =====
    public IEnumerator Run(int seconds = 5, string title = "", AudioClip tick = null, AudioClip final = null)
{
    if (!canvasGroup) BuildInScene();
    if (!canvasGroup) yield break;

    if (tick)  tickClip  = tick;
    if (final) finalClip = final;
    if (titleTMP) titleTMP.text = title ?? "";

    // (opcional) pre-setea el número para evitar “flash” de otro valor
    int secs = Mathf.Max(1, seconds);
    SetNumber(secs);
    yield return null;

    // Fade-in
    canvasGroup.blocksRaycasts = true;
    canvasGroup.interactable   = true;
    yield return Fade(canvasGroup, 1f, fadeIn);

    /* ====== AQUÍ VA EL AJUSTE ====== */
    // Si tu "Final Clip" es el audio completo "3-2-1", tócalo una sola vez:
    if (finalClip)
    {
        Play(finalClip);   // suena 3-2-1 completo una sola vez
        tickClip = null;   // desactiva el tick por segundo
        finalClip = null;  // evita que vuelva a sonar al llegar a 0
    }
    /* =============================== */

    // Bucle de conteo en pantalla (ya sin ticks por segundo)
    float one = 1f;
    for (int t = secs; t > 0; t--)
    {
        SetNumber(t);
        if (tickClip) Play(tickClip); // no se ejecutará si lo pusimos null arriba
        yield return Pulse(countTMP.rectTransform, pulseScale, pulseTime);
        yield return new WaitForSeconds(Mathf.Max(0f, one - pulseTime));
    }

    // Cero final (ya no suena nada porque finalClip quedó en null)
    SetNumber(0);
    yield return Pulse(countTMP.rectTransform, pulseScale + 0.05f, pulseTime);
    yield return new WaitForSeconds(0.05f);

    yield return Fade(canvasGroup, 0f, fadeOut);
    canvasGroup.blocksRaycasts = false;
    canvasGroup.interactable   = false;
}


    public static IEnumerator ShowAndWait(int seconds = 5, string title = "", AudioClip tick = null, AudioClip final = null)
    {
        if (!Instance)
        {
            var go = new GameObject("CountdownOverlay_Runtime");
            var inst = go.AddComponent<CountdownOverlay>();
            inst.BuildInScene();
        }
        yield return Instance.Run(seconds, title, tick, final);
    }

    
    [ContextMenu("Build Countdown Overlay (EditMode)")]
    public void BuildInScene()
    {
        // Canvas + EventSystem
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
        if (!FindObjectOfType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            es.transform.SetParent(targetCanvas.transform.parent, false);
        }

       
        var root = new GameObject("CountdownOverlay", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(targetCanvas.transform, false);
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false;

        // Overlay
        var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        var rto = overlay.GetComponent<RectTransform>();
        rto.anchorMin = Vector2.zero; rto.anchorMax = Vector2.one;
        rto.offsetMin = Vector2.zero; rto.offsetMax = Vector2.zero;
        var imgOverlay = overlay.GetComponent<Image>();
        imgOverlay.color = overlayColor;
        imgOverlay.raycastTarget = true; // bloquea clics mientras cuenta

        // Contenedor central
        var center = new GameObject("Center", typeof(RectTransform));
        center.transform.SetParent(root.transform, false);
        var rtc = center.GetComponent<RectTransform>();
        rtc.anchorMin = rtc.anchorMax = new Vector2(0.5f, 0.5f);
        rtc.anchoredPosition = Vector2.zero;
        rtc.sizeDelta = new Vector2(500, 300);

        // Título
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(center.transform, false);
        var rtTitle = titleGO.GetComponent<RectTransform>();
        rtTitle.anchorMin = rtTitle.anchorMax = new Vector2(0.5f, 1f);
        rtTitle.pivot = new Vector2(0.5f, 1f);
        rtTitle.anchoredPosition = new Vector2(0, -10);
        rtTitle.sizeDelta = new Vector2(900, 120);
        titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
        titleTMP.text = "¡Prepárate!";
        titleTMP.enableAutoSizing = true; titleTMP.fontSizeMin = 26; titleTMP.fontSizeMax = 42;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color32(229,231,235,255); // gris claro
        titleTMP.raycastTarget = false;

        // Número grande
        var numGO = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        numGO.transform.SetParent(center.transform, false);
        var rtNum = numGO.GetComponent<RectTransform>();
        rtNum.anchorMin = rtNum.anchorMax = new Vector2(0.5f, 0.5f);
        rtNum.anchoredPosition = Vector2.zero;
        rtNum.sizeDelta = new Vector2(700, 240);
        countTMP = numGO.GetComponent<TextMeshProUGUI>();
        countTMP.text = "";
        countTMP.enableAutoSizing = true; countTMP.fontSizeMin = 64; countTMP.fontSizeMax = 220;
        countTMP.alignment = TextAlignmentOptions.Center;
        countTMP.color = numberColor;
        countTMP.raycastTarget = false;

        // Anillo radial (opcional)
        var ringGO = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGO.transform.SetParent(center.transform, false);
        var rtRing = ringGO.GetComponent<RectTransform>();
        rtRing.anchorMin = rtRing.anchorMax = new Vector2(0.5f, 0.5f);
        rtRing.anchoredPosition = Vector2.zero;
        rtRing.sizeDelta = new Vector2(280, 280);
        ringImage = ringGO.GetComponent<Image>();
        ringImage.type = Image.Type.Filled;
        ringImage.fillMethod = Image.FillMethod.Radial360;
        ringImage.fillOrigin = 2;
        ringImage.fillClockwise = false;
        ringImage.fillAmount = 1f;
        ringImage.color = new Color(1,1,1,0.45f);

        // AudioSource
        if (!sfxSource)
        {
            var sfxGO = new GameObject("SFX");
            sfxGO.transform.SetParent(root.transform, false);
            sfxSource = sfxGO.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        built = true;
    }

    // ===== Internos =====
    void SetNumber(int n)
    {
        if (countTMP) countTMP.text = n.ToString();
        if (ringImage) ringImage.fillAmount = 1f; // reinicio del anillo para el próximo segundo
    }

    void HideImmediate()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    IEnumerator Fade(CanvasGroup cg, float target, float dur)
    {
        float start = cg.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        cg.alpha = target;
    }

    IEnumerator Pulse(RectTransform rt, float scale, float dur)
    {
        if (!rt) yield break;
        float t = 0f;
        Vector3 s0 = Vector3.one;
        Vector3 s1 = Vector3.one * scale;
        // animación de anillo (llenado 1→0 en el segundo)
        StartCoroutine(FillRing(1f, 0f, 1f));
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            rt.localScale = Vector3.Lerp(s0, s1, k);
            yield return null;
        }
        // volver a 1 rápidamente
        t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(s1, s0, t / 0.08f);
            yield return null;
        }
        rt.localScale = s0;
    }

    IEnumerator FillRing(float from, float to, float dur)
    {
        if (!ringImage) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            ringImage.fillAmount = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        ringImage.fillAmount = to;
    }

    void Play(AudioClip clip)
    {
        if (!clip) return;
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        sfxSource.PlayOneShot(clip);
    }
}
