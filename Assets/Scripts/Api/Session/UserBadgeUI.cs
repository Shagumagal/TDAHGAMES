using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Crea un Canvas overlay y muestra el nombre del usuario abajo-derecha.
/// Arrástralo a un GameObject de la PRIMERA escena (junto a GameAuth).
public class UserBadgeUI : MonoBehaviour
{
    [Header("Estilo")]
    public Vector2  padding    = new Vector2(12, 6);
    public int      fontSize   = 18;
    public Color    textColor  = Color.white;
    public Color    bgColor    = new Color(0f, 0f, 0f, 0.45f);

    private RectTransform panelRt;
    private TMP_Text      label;

    void Awake()
    {
        BuildUI();
        // Si el usuario ya está, pinta; si no, espera evento
        if (GameAuth.CurrentUser != null) SetName(GameAuth.CurrentUser.nombre);
        GameAuth.OnUserReady += OnUserReady;
        DontDestroyOnLoad(transform.root.gameObject);
    }

    void OnDestroy()
    {
        GameAuth.OnUserReady -= OnUserReady;
    }

    void OnUserReady(UserInfo u)
    {
        SetName(u?.nombre ?? "—");
    }

    void SetName(string nombre)
    {
        if (label != null)
        {
            label.text = nombre ?? "—";
            Refit();
        }
    }

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("UserBadgeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas   = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        DontDestroyOnLoad(canvasGO);

        // Panel
        var panelGO = new GameObject("UserBadgePanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f); // bottom-right
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot     = new Vector2(1f, 0f);
        panelRt.anchoredPosition = new Vector2(-16f, 16f); // margen bordes

        var img = panelGO.GetComponent<Image>();
        img.color = bgColor; // fondo semitransparente

        // Label
        var textGO = new GameObject("UserBadgeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;

        label = textGO.GetComponent<TextMeshProUGUI>();
        label.text = "—";
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.enableWordWrapping = false;
        label.margin = new Vector4(padding.x, padding.y, padding.x, padding.y);

        Refit();
    }

    void Refit()
    {
        if (label == null || panelRt == null) return;
        // Calcula tamaño aproximado (ancho texto + padding)
        var preferred = label.GetPreferredValues(label.text, 2000, 100);
        var w = preferred.x + padding.x * 2f;
        var h = Mathf.Max(preferred.y + padding.y * 2f, 32f);
        panelRt.sizeDelta = new Vector2(w, h);
    }
}
